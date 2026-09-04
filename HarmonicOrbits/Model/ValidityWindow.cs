namespace HarmonicOrbits
{
    /// <summary>What to do outside the model's fitted window.</summary>
    public enum OutsideWindowAction
    {
        /// <summary>Keep evaluating the harmonics. Holds well past 2100 for most bodies.</summary>
        Continue,

        /// <summary>Hold the elements at the window edge and let mean motion run on.</summary>
        Clamp,
    }

    /// <summary>Applies the outside-window policy to an evaluation time.</summary>
    public static class ValidityWindow
    {
        public const double StartUt = 0.0;
        public const double EndUt = ModelEpoch.WindowEndUt;

        public static bool IsOutside(double ut)
        {
            return ut < StartUt || ut > EndUt;
        }

        /// <summary>UT to evaluate the model at, and the epoch to stamp on the orbit.</summary>
        // Clamping both is what makes clamp equal stock: elements frozen at the edge, with
        // KSP advancing mean anomaly from that epoch and nothing else changing.
        public static double EvaluationTime(double ut, OutsideWindowAction action)
        {
            if (action == OutsideWindowAction.Continue)
            {
                return ut;
            }
            if (ut < StartUt) return StartUt;
            if (ut > EndUt) return EndUt;
            return ut;
        }
    }
}
