using System;

namespace HarmonicOrbits
{
    /// <summary>The six equinoctial elements the models are fitted in.</summary>
    public struct EquinoctialElements
    {
        /// <summary>Semi-major axis, km.</summary>
        public double A;
        public double H;
        public double K;
        public double P;
        public double Q;
        /// <summary>Mean longitude, degrees, unbounded.</summary>
        public double Lambda;

        /// <summary>Convert to classical elements.</summary>
        // Keep this as the single conversion point; errors in individual classical
        // elements cancel in position, so a duplicate would be a silent accuracy regression.
        public ClassicalElements ToClassical()
        {
            ClassicalElements c;
            c.SemiMajorAxis = A;
            // No need for hypot; values are far from overflow.
            c.Eccentricity = Math.Sqrt(H * H + K * K);

            double varpi = Rad2Deg * Math.Atan2(H, K);
            double lan = Rad2Deg * Math.Atan2(P, Q);
            c.Inclination = Rad2Deg * 2.0 * Math.Atan(Math.Sqrt(P * P + Q * Q));
            c.LongitudeOfAscendingNode = lan;
            c.ArgumentOfPeriapsis = varpi - lan;
            c.MeanAnomaly = Lambda - varpi;
            return c;
        }

        private const double Rad2Deg = 180.0 / Math.PI;
    }
}
