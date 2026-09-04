namespace HarmonicOrbits
{
    /// <summary>Conversion between KSP universal time and model time.</summary>
    public static class ModelEpoch
    {
        /// <summary>Julian date of UT = 0 (1951-01-01 00:00 GMT).</summary>
        // Source: Sol-KopernicusSettings.cfg "We now sample at JD2433647.500000000".
        public const double Ut0Jd = 2433647.5;

        public const double SecondsPerDay = 86400.0;

        /// <summary>UT at 2051-01-01, the end of the scored window.</summary>
        // Shipped constant, not per-body SpanDays: Europa was fitted 1900-2100 and
        // would otherwise run 50 years past anything measured.
        public const double WindowEndUt = 36525.0 * SecondsPerDay;

        /// <summary>Convert UT to days after the model epoch.</summary>
        // Offset is per body; the models do not share an epoch.
        public static double ToModelTime(double ut, double epochJd)
        {
            return ut / SecondsPerDay + (Ut0Jd - epochJd);
        }

        public static double ToUniversalTime(double t, double epochJd)
        {
            return (t - (Ut0Jd - epochJd)) * SecondsPerDay;
        }
    }
}
