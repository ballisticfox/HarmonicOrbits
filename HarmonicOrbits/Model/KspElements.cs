using System;

namespace HarmonicOrbits
{
    /// <summary>Elements in the units and ranges Orbit.SetOrbit expects.</summary>
    // Free of KSP types so the unit conversion is testable without the game.
    public struct KspElements
    {
        /// <summary>Degrees.</summary>
        public double Inclination;
        public double Eccentricity;
        /// <summary>Metres.</summary>
        public double SemiMajorAxis;
        /// <summary>Degrees.</summary>
        public double LongitudeOfAscendingNode;
        /// <summary>Degrees.</summary>
        public double ArgumentOfPeriapsis;
        /// <summary>Radians, [0, 2pi).</summary>
        public double MeanAnomalyAtEpoch;

        public static KspElements From(ClassicalElements c)
        {
            KspElements e;
            e.Inclination = c.Inclination;
            e.Eccentricity = c.Eccentricity;
            e.SemiMajorAxis = c.SemiMajorAxis * KmToM;
            e.LongitudeOfAscendingNode = c.LongitudeOfAscendingNode;
            e.ArgumentOfPeriapsis = c.ArgumentOfPeriapsis;
            // Lambda is unbounded; Orbit.Init loses precision unwrapped.
            e.MeanAnomalyAtEpoch = Wrap(c.MeanAnomaly * Deg2Rad);
            return e;
        }

        /// <summary>Reduces an angle to [0, 2pi).</summary>
        public static double Wrap(double radians)
        {
            double r = radians % TwoPi;
            return r < 0.0 ? r + TwoPi : r;
        }

        private const double Deg2Rad = Math.PI / 180.0;
        private const double TwoPi = Math.PI * 2.0;
        private const double KmToM = 1000.0;
    }
}
