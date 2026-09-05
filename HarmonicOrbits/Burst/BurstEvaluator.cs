using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Unity.Burst;

namespace HarmonicOrbits
{
    /// <summary>Optional Burst-compiled element evaluator.</summary>
    // 98% of a tick is sin/cos (measured: 9.388 of 9.558 us, 178 terms, net48).
    //
    // FunctionPointer, not IJob: scheduling costs 5-20 us against a ~30 us workload.
    //
    // Two kernels: Accurate forbids reassociation (blocks vectorisation); Fast allows it
    // but may use a sloppy range reduction, catastrophic at ~9,500 rad. Both accuracy-gated.
    //
    // [BurstCompile] on the type: AOT skips function-pointer methods without it.
    // Opt-in is [KSPAssemblyDependency("KSPBurst")] in the csproj.
    [BurstCompile]
    public static unsafe class BurstEvaluator
    {
        // SuppressUnmanagedCodeSecurity: called once per driven body per tick, and without it
        // every call pays a security stack walk.
        [SuppressUnmanagedCodeSecurity]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void EvaluateSignature(double t, double* coef, int* meta, double* result);

        // offset, secular length, term count -- per series.
        private const int MetaPerSeries = 3;
        private const int MetaPerBody = BodyModel.ElementCount * MetaPerSeries;

        /// <summary>Maximum compiled-vs-managed displacement, km.</summary>
        // Kilometre budget, not relative: lambda reaches 486,499 deg unwrapped, where 1e-9
        // relative is 1,270 km — well above the model's own error. Each element is converted
        // to displacement first.
        private const double ToleranceKm = 0.1;

        // A kernel must beat managed by this ratio; below it the measurement is noise.
        private const double MinimumSpeedup = 1.15;

        private const int BenchmarkIterations = 200;

        private static EvaluateSignature _evaluate;
        private static double* _coef;
        private static int* _meta;
        private static Dictionary<BodyModel, int> _slots;

        /// <summary>True when a compiled path is in use.</summary>
        public static bool Active { get; private set; }

        /// <summary>Which kernel is in use, and how every candidate measured.</summary>
        public static string Status { get; private set; }

        /// <summary>Arms the fastest compiled kernel that matches the managed evaluator.</summary>
        public static bool Build(ICollection<BodyModel> models)
        {
            using (HarmonicOrbitsProfiler.BurstCompile.Sample())
            {
                Release();
                if (models == null || models.Count == 0)
                {
                    Status = "no driven bodies";
                    return false;
                }

                Pack(models);

                var report = new List<string>();
                double managed = TimeManaged(models);
                report.Add(string.Format("managed {0:F1}us", managed));

                EvaluateSignature best = null;
                string bestName = null;
                double bestTime = double.MaxValue;

                foreach (KeyValuePair<string, EvaluateSignature> candidate in Compile(report))
                {
                    _evaluate = candidate.Value;
                    double error = Disagreement(models);
                    double time = TimeCompiled(models);
                    report.Add(string.Format("{0} {1:F1}us err {2:E2}km",
                        candidate.Key, time, error));

                    if (error <= ToleranceKm && time < bestTime)
                    {
                        best = candidate.Value;
                        bestName = candidate.Key;
                        bestTime = time;
                    }
                }

                _evaluate = best;
                if (best == null || bestTime * MinimumSpeedup > managed)
                {
                    Status = string.Join(" | ", report.ToArray())
                        + " -> managed (no kernel was both accurate and faster)";
                    Release();
                    return false;
                }

                Active = true;
                Status = string.Join(" | ", report.ToArray())
                    + string.Format(" -> {0}, {1:F1}x", bestName, managed / bestTime);
                return true;
            }
        }

        /// <summary>Elements at the given model time; managed path when not armed.</summary>
        public static EquinoctialElements Evaluate(BodyModel model, double t)
        {
            int slot;
            if (!Active || !_slots.TryGetValue(model, out slot))
            {
                return model.Evaluate(t);
            }

            double* r = stackalloc double[BodyModel.ElementCount];
            _evaluate(t, _coef, _meta + slot * MetaPerBody, r);

            EquinoctialElements e;
            e.A = r[BodyModel.IndexA];
            e.H = r[BodyModel.IndexH];
            e.K = r[BodyModel.IndexK];
            e.P = r[BodyModel.IndexP];
            e.Q = r[BodyModel.IndexQ];
            e.Lambda = r[BodyModel.IndexLambda];
            return e;
        }

        /// <summary>Returns to the managed path and records why.</summary>
        public static void Disable(string reason)
        {
            Release();
            Status = reason;
        }

        /// <summary>Frees the packed coefficients and returns to the managed path.</summary>
        public static void Release()
        {
            Active = false;
            _evaluate = null;
            _slots = null;
            if (_coef != null)
            {
                Marshal.FreeHGlobal((IntPtr)_coef);
                _coef = null;
            }
            if (_meta != null)
            {
                Marshal.FreeHGlobal((IntPtr)_meta);
                _meta = null;
            }
        }

        #region Kernels

        // The two differ only in their attributes. Both split the accumulator four ways, which
        // gives the vectoriser four independent dependency chains without needing permission
        // to reassociate -- and unlike reassociation the order is fixed, so a given build
        // always returns the same bits.

        /// <summary>Reference float behaviour: no reassociation, accurate transcendentals.</summary>
        [BurstCompile(CompileSynchronously = true,
            FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.High)]
        private static void EvaluateAccurate(double t, double* coef, int* meta, double* result)
        {
            Kernel(t, coef, meta, result);
        }

        /// <summary>Reassociation and cheaper math allowed; gated by the accuracy check.</summary>
        [BurstCompile(CompileSynchronously = true,
            FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static void EvaluateFast(double t, double* coef, int* meta, double* result)
        {
            Kernel(t, coef, meta, result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Kernel(double t, double* coef, int* meta, double* result)
        {
            for (int s = 0; s < BodyModel.ElementCount; s++)
            {
                int at = meta[s * MetaPerSeries];
                int secularLength = meta[s * MetaPerSeries + 1];
                int terms = meta[s * MetaPerSeries + 2];

                double v = 0.0;
                for (int i = 0; i < secularLength; i++)
                {
                    v = v * t + coef[at + i];
                }
                v += coef[at + secularLength];

                int omega = at + secularLength + 1;
                int cos = omega + terms;
                int sin = cos + terms;

                double s0 = 0.0, s1 = 0.0, s2 = 0.0, s3 = 0.0;
                int j = 0;
                for (; j + 3 < terms; j += 4)
                {
                    double w0 = coef[omega + j] * t;
                    double w1 = coef[omega + j + 1] * t;
                    double w2 = coef[omega + j + 2] * t;
                    double w3 = coef[omega + j + 3] * t;
                    s0 += coef[cos + j] * Math.Cos(w0) + coef[sin + j] * Math.Sin(w0);
                    s1 += coef[cos + j + 1] * Math.Cos(w1) + coef[sin + j + 1] * Math.Sin(w1);
                    s2 += coef[cos + j + 2] * Math.Cos(w2) + coef[sin + j + 2] * Math.Sin(w2);
                    s3 += coef[cos + j + 3] * Math.Cos(w3) + coef[sin + j + 3] * Math.Sin(w3);
                }
                for (; j < terms; j++)
                {
                    double w = coef[omega + j] * t;
                    s0 += coef[cos + j] * Math.Cos(w) + coef[sin + j] * Math.Sin(w);
                }
                result[s] = v + ((s0 + s1) + (s2 + s3));
            }
        }

        #endregion

        /// <summary>Compiles both kernels; the only methods that touch a Unity.Burst type.</summary>
        // NoInlining confines Burst references here so a missing KSPBurst throws at JIT of
        // these methods, where it is caught, not in the caller.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static List<KeyValuePair<string, EvaluateSignature>> Compile(List<string> report)
        {
            var compiled = new List<KeyValuePair<string, EvaluateSignature>>(2);
            Add(compiled, report, "accurate", EvaluateAccurate);
            Add(compiled, report, "fast", EvaluateFast);
            return compiled;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Add(List<KeyValuePair<string, EvaluateSignature>> into,
            List<string> report, string name, EvaluateSignature kernel)
        {
            try
            {
                if (!BurstCompiler.IsEnabled)
                {
                    report.Add(name + " unavailable (Burst disabled)");
                    return;
                }

                FunctionPointer<EvaluateSignature> fp =
                    BurstCompiler.CompileFunctionPointer<EvaluateSignature>(kernel);
                if (fp.Value == IntPtr.Zero)
                {
                    report.Add(name + " unavailable (null pointer)");
                    return;
                }

                into.Add(new KeyValuePair<string, EvaluateSignature>(name,
                    (EvaluateSignature)Marshal.GetDelegateForFunctionPointer(
                        fp.Value, typeof(EvaluateSignature))));
            }
            catch (Exception ex)
            {
                report.Add(name + " unavailable (" + ex.GetType().Name + ": " + ex.Message + ")");
            }
        }

        /// <summary>Worst displacement between the compiled and managed paths, km.</summary>
        // Golden vectors run on net8.0 with no Burst, so compiled code is only tested on
        // the player's machine at load.
        private static double Disagreement(ICollection<BodyModel> models)
        {
            const int Steps = 16;
            double* r = stackalloc double[BodyModel.ElementCount];
            double worst = 0.0;

            try
            {
                foreach (BodyModel m in models)
                {
                    double t0 = ModelEpoch.ToModelTime(ValidityWindow.StartUt, m.EpochJd);
                    double t1 = ModelEpoch.ToModelTime(ValidityWindow.EndUt, m.EpochJd);
                    for (int k = 0; k <= Steps; k++)
                    {
                        double t = t0 + (t1 - t0) * k / Steps;
                        EquinoctialElements e = m.Evaluate(t);
                        _evaluate(t, _coef, _meta + _slots[m] * MetaPerBody, r);

                        // Each element converted to the displacement it causes at this orbit.
                        double a = Math.Abs(e.A);
                        worst = Math.Max(worst, Math.Abs(e.A - r[BodyModel.IndexA]));
                        worst = Math.Max(worst, Math.Abs(e.H - r[BodyModel.IndexH]) * a);
                        worst = Math.Max(worst, Math.Abs(e.K - r[BodyModel.IndexK]) * a);
                        worst = Math.Max(worst, Math.Abs(e.P - r[BodyModel.IndexP]) * a * 2.0);
                        worst = Math.Max(worst, Math.Abs(e.Q - r[BodyModel.IndexQ]) * a * 2.0);
                        worst = Math.Max(worst, Math.Abs(e.Lambda - r[BodyModel.IndexLambda])
                            * (Math.PI / 180.0) * a);
                    }
                }
            }
            catch (Exception)
            {
                return double.MaxValue;
            }

            return worst;
        }

        private static Dictionary<BodyModel, double> SampleTimes(ICollection<BodyModel> models)
        {
            var times = new Dictionary<BodyModel, double>(models.Count);
            foreach (BodyModel m in models)
            {
                double t0 = ModelEpoch.ToModelTime(ValidityWindow.StartUt, m.EpochJd);
                double t1 = ModelEpoch.ToModelTime(ValidityWindow.EndUt, m.EpochJd);
                times[m] = 0.5 * (t0 + t1);
            }
            return times;
        }

        /// <summary>One tick of managed evaluation, microseconds.</summary>
        private static double TimeManaged(ICollection<BodyModel> models)
        {
            Dictionary<BodyModel, double> times = SampleTimes(models);
            double sink = 0.0;
            foreach (BodyModel m in models)
            {
                sink += m.Evaluate(times[m]).A;
            }

            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                foreach (BodyModel m in models)
                {
                    sink += m.Evaluate(times[m] + i).A;
                }
            }
            watch.Stop();
            GC.KeepAlive(sink);
            return watch.Elapsed.TotalMilliseconds * 1000.0 / BenchmarkIterations;
        }

        /// <summary>One tick of compiled evaluation, microseconds.</summary>
        private static double TimeCompiled(ICollection<BodyModel> models)
        {
            Dictionary<BodyModel, double> times = SampleTimes(models);
            double* r = stackalloc double[BodyModel.ElementCount];
            double sink = 0.0;
            foreach (BodyModel m in models)
            {
                _evaluate(times[m], _coef, _meta + _slots[m] * MetaPerBody, r);
                sink += r[0];
            }

            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                foreach (BodyModel m in models)
                {
                    _evaluate(times[m] + i, _coef, _meta + _slots[m] * MetaPerBody, r);
                    sink += r[0];
                }
            }
            watch.Stop();
            GC.KeepAlive(sink);
            return watch.Elapsed.TotalMilliseconds * 1000.0 / BenchmarkIterations;
        }

        private static void Pack(ICollection<BodyModel> models)
        {
            int coefficients = 0;
            foreach (BodyModel m in models)
            {
                coefficients += m.CoefficientCount;
            }

            var staging = new double[coefficients];
            var meta = new int[models.Count * MetaPerBody];
            _slots = new Dictionary<BodyModel, int>(models.Count);

            int at = 0;
            int slot = 0;
            foreach (BodyModel m in models)
            {
                _slots[m] = slot;
                for (int s = 0; s < BodyModel.ElementCount; s++)
                {
                    ElementSeries series = m.Series(s);
                    int b = slot * MetaPerBody + s * MetaPerSeries;
                    meta[b] = at;
                    meta[b + 1] = series.SecularDegree + 1;
                    meta[b + 2] = series.TermCount;
                    at += series.CopyTo(staging, at);
                }
                slot++;
            }

            _coef = (double*)Marshal.AllocHGlobal(coefficients * sizeof(double));
            _meta = (int*)Marshal.AllocHGlobal(meta.Length * sizeof(int));
            Marshal.Copy(staging, 0, (IntPtr)_coef, staging.Length);
            Marshal.Copy(meta, 0, (IntPtr)_meta, meta.Length);
        }
    }
}
