namespace HarmonicOrbits
{
    /// <summary>Classical orbital elements in kilometres and degrees.</summary>
    public struct ClassicalElements
    {
        /// <summary>Semi-major axis, km.</summary>
        public double SemiMajorAxis;
        public double Eccentricity;
        /// <summary>Degrees.</summary>
        public double Inclination;
        /// <summary>Degrees.</summary>
        public double LongitudeOfAscendingNode;
        /// <summary>Degrees.</summary>
        public double ArgumentOfPeriapsis;
        /// <summary>Degrees, unbounded. Wrap only where a consumer requires it.</summary>
        public double MeanAnomaly;
    }
}
