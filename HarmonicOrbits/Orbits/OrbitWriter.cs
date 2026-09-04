using System;

namespace HarmonicOrbits
{
    /// <summary>Writes model elements into a KSP orbit.</summary>
    public static class OrbitWriter
    {
        /// <summary>Osculates the orbit at the given UT, subject to the window policy.</summary>
        public static void Write(Orbit orbit, BodyModel model, double ut,
            OutsideWindowAction outsideWindow)
        {
            if (orbit == null) throw new ArgumentNullException("orbit");
            if (model == null) throw new ArgumentNullException("model");

            // Under Clamp this is the window edge, so the epoch stamped below is too and KSP
            // advances mean anomaly from there.
            ut = ValidityWindow.EvaluationTime(ut, outsideWindow);

            var e = KspElements.From(model.EvaluateAtUniversalTime(ut));
            orbit.SetOrbit(
                e.Inclination,
                e.Eccentricity,
                e.SemiMajorAxis,
                e.LongitudeOfAscendingNode,
                e.ArgumentOfPeriapsis,
                e.MeanAnomalyAtEpoch,
                ut,
                orbit.referenceBody);
        }
    }
}
