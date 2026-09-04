using System.Collections.Generic;

namespace HarmonicOrbits
{
    /// <summary>Tracks which bodies are driven and writes their elements each frame.</summary>
    // Free of OrbitDriver so this is testable without Harmony or Unity.
    public static class BodyOrbitUpdater
    {
        private static readonly Dictionary<CelestialBody, BodyModel> Driven =
            new Dictionary<CelestialBody, BodyModel>();

        private static OutsideWindowAction _outsideWindow = OutsideWindowAction.Continue;

        public static int Count => Driven.Count;

        /// <summary>Resolves the enabled bodies present in the current scene.</summary>
        public static int Rebuild(ModelRegistry models, HarmonicOrbitsSettings settings)
        {
            Driven.Clear();
            if (models == null || settings == null || FlightGlobals.Bodies == null)
            {
                return 0;
            }

            _outsideWindow = settings.OutsideWindow;
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                CelestialBody body = FlightGlobals.Bodies[i];
                if (body == null || body.orbitDriver == null || body.orbit == null)
                {
                    continue;
                }
                if (settings.IsBodyEnabled(body.bodyName)
                    && models.TryGet(body.bodyName, out BodyModel model))
                {
                    Driven[body] = model;
                }
            }
            return Driven.Count;
        }

        public static void Clear()
        {
            Driven.Clear();
        }

        /// <summary>Rewrites the body's elements at the given UT; false if not driven.</summary>
        public static bool Apply(CelestialBody body, double ut)
        {
            if (Driven.Count == 0 || body == null)
            {
                return false;
            }
            if (!Driven.TryGetValue(body, out BodyModel model))
            {
                return false;
            }
            OrbitWriter.Write(body.orbit, model, ut, _outsideWindow);
            return true;
        }
    }
}
