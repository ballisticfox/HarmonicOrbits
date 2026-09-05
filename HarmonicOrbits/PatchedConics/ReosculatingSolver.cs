using System;
using System.Collections.Generic;

namespace HarmonicOrbits
{
    /// <summary>Re-solves each patch with the encountered body osculating at the crossing.</summary>
    // KSP predicts from the conic osculated *now*; error grows as ~0.5 a_pert dt^2. At a
    // 5-day lunar lead that reached 1,805 km of periapsis error. Osculating at the crossing
    // instead brings it to 0.073 km.
    //
    // Sampling the model inside the solver loop instead was measured at 35-70% of a frame in
    // the Jupiter system. Do not.
    public static class ReosculatingSolver
    {
        // Two, measured: the first crossing estimate is wrong by up to two hours, so one
        // correction still leaves 9.3 km of periapsis error against 0.073 km for two. A third
        // buys 0.001 km and is not worth another solve.
        private const int Corrections = 2;
        private const double CrossingToleranceSeconds = 1.0;

        private sealed class Crossing
        {
            public CelestialBody Body;
            public double Ut;
        }

        private static readonly Dictionary<Orbit, Crossing> Warm =
            new Dictionary<Orbit, Crossing>();

        private static PatchedConics.CalculatePatchDelegate _stock;

        public static bool Installed => _stock != null;

        public static void Install()
        {
            if (_stock != null)
            {
                return;
            }
            _stock = PatchedConics.CalculatePatch;
            PatchedConics.CalculatePatch = CalculatePatch;
        }

        public static void Remove()
        {
            if (_stock == null)
            {
                return;
            }
            PatchedConics.CalculatePatch = _stock;
            _stock = null;
            Warm.Clear();
        }

        /// <summary>Drops the warm-start cache. Call when the body set changes.</summary>
        public static void Forget()
        {
            Warm.Clear();
        }

        private static bool CalculatePatch(Orbit p, Orbit nextPatch, double startEpoch,
            PatchedConics.SolverParameters pars, CelestialBody targetBody)
        {
            using (HarmonicOrbitsProfiler.CalculatePatch.Sample())
            {
                return Solve(p, nextPatch, startEpoch, pars, targetBody);
            }
        }

        private static bool Solve(Orbit p, Orbit nextPatch, double startEpoch,
            PatchedConics.SolverParameters pars, CelestialBody targetBody)
        {
            // A driven body matters when arriving at one orbiting this parent, or leaving
            // one. Skip everything else.
            if (!BodyOrbitUpdater.HasDrivenChildren(p.referenceBody)
                && !BodyOrbitUpdater.IsDriven(p.referenceBody))
            {
                return _stock(p, nextPatch, startEpoch, pars, targetBody);
            }

            double now = Planetarium.fetch == null ? startEpoch : Planetarium.fetch.time;
            if (!TryEstimateCrossing(p, startEpoch, pars, targetBody, now, out CelestialBody entered,
                    out double crossing))
            {
                Warm.Remove(p);
                return _stock(p, nextPatch, startEpoch, pars, targetBody);
            }

            try
            {
                BodyOrbitUpdater.Apply(entered, crossing);
                bool more;
                using (HarmonicOrbitsProfiler.CalculatePatchFinal.Sample())
                {
                    more = _stock(p, nextPatch, startEpoch, pars, targetBody);
                }
                Warm[p] = new Crossing { Body = entered, Ut = p.UTsoi };
                return more;
            }
            finally
            {
                // The solver reads from the live OrbitDriver, which also renders the body.
                // Leaving it osculated at the crossing would displace it until FixedUpdate.
                BodyOrbitUpdater.Apply(entered, now);
            }
        }

        private static bool TryEstimateCrossing(Orbit p, double startEpoch,
            PatchedConics.SolverParameters pars, CelestialBody targetBody, double now,
            out CelestialBody entered, out double crossing)
        {
            entered = null;
            crossing = 0.0;

            // Last frame's crossing usually confirms in one probe; a stale one falls through
            // to the cold path.
            if (Warm.TryGetValue(p, out Crossing warm) && BodyOrbitUpdater.IsDriven(warm.Body)
                && warm.Ut > now)
            {
                BodyOrbitUpdater.Apply(warm.Body, warm.Ut);
                bool confirmed = Probe(p, startEpoch, pars, targetBody, out CelestialBody body, out double next)
                    && ReferenceEquals(body, warm.Body) && next > now
                    && Math.Abs(next - warm.Ut) < CrossingToleranceSeconds;
                if (confirmed)
                {
                    entered = warm.Body;
                    crossing = next;
                    return true;
                }
                BodyOrbitUpdater.Apply(warm.Body, now);
                Warm.Remove(p);
            }

            for (int i = 0; i < Corrections; i++)
            {
                if (!Probe(p, startEpoch, pars, targetBody, out CelestialBody body, out double next) || next <= now)
                {
                    if (entered != null)
                    {
                        BodyOrbitUpdater.Apply(entered, now);
                    }
                    entered = null;
                    return false;
                }

                bool converged = entered != null
                    && Math.Abs(next - crossing) < CrossingToleranceSeconds;
                entered = body;
                crossing = next;
                if (converged)
                {
                    break;
                }
                if (i + 1 < Corrections)
                {
                    BodyOrbitUpdater.Apply(body, crossing);
                }
            }

            return entered != null;
        }

        /// <summary>Solves a copy to learn where and when the encounter happens.</summary>
        // Must copy: _CalculatePatch narrows EndUT and seeds ClEctr/FEV/SEV, so a second
        // solve on the same instance degrades the answer or loses the encounter.
        private static bool Probe(Orbit p, double startEpoch,
            PatchedConics.SolverParameters pars, CelestialBody targetBody,
            out CelestialBody entered, out double crossing)
        {
            entered = null;
            crossing = 0.0;

            Orbit probe = new Orbit(p) { StartUT = p.StartUT, EndUT = p.EndUT };
            Orbit probeNext = new Orbit();
            bool solved;
            using (HarmonicOrbitsProfiler.CalculatePatchProbe.Sample())
            {
                solved = _stock(probe, probeNext, startEpoch, pars, targetBody);
            }
            if (!solved)
            {
                return false;
            }

            CelestialBody body;
            switch (probe.patchEndTransition)
            {
                // nextPatch.referenceBody rather than closestEncounterBody: the latter tracks
                // the nearest approach of any body, which need not be the one entered.
                case Orbit.PatchTransitionType.ENCOUNTER:
                    body = probeNext.referenceBody;
                    break;

                // UpdateFromOrbitAtUT builds the escape patch from vessel + body state at the
                // crossing, so a stale conic offsets the departure and moves the far periapsis.
                case Orbit.PatchTransitionType.ESCAPE:
                    body = probe.referenceBody;
                    break;

                default:
                    return false;
            }

            if (body == null || !BodyOrbitUpdater.IsDriven(body))
            {
                return false;
            }

            double ut = probe.UTsoi;
            if (double.IsNaN(ut) || double.IsInfinity(ut))
            {
                return false;
            }

            entered = body;
            crossing = ut;
            return true;
        }
    }
}
