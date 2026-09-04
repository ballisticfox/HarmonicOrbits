using System.Collections.Generic;

namespace HarmonicOrbits
{
    /// <summary>Tracks which bodies are driven and writes their elements each frame.</summary>
    // Free of OrbitDriver so this is testable without Harmony or Unity.
    public static class BodyOrbitUpdater
    {
        private static readonly Dictionary<CelestialBody, BodyModel> Driven =
            new Dictionary<CelestialBody, BodyModel>();

        private static readonly HashSet<CelestialBody> Parents = new HashSet<CelestialBody>();

        private static OutsideWindowAction _outsideWindow = OutsideWindowAction.Continue;

        public static int Count => Driven.Count;

        /// <summary>Resolves the enabled bodies present in the current scene.</summary>
        // Takes the list rather than reading FlightGlobals: its static ctor calls
        // Quaternion.Euler, so touching the type at all throws outside the game.
        public static int Rebuild(ModelRegistry models, HarmonicOrbitsSettings settings,
            IList<CelestialBody> bodies)
        {
            using (HarmonicOrbitsProfiler.Rebuild.Sample())
            {
                return RebuildCore(models, settings, bodies);
            }
        }

        private static int RebuildCore(ModelRegistry models, HarmonicOrbitsSettings settings,
            IList<CelestialBody> bodies)
        {
            Driven.Clear();
            Parents.Clear();
            if (models == null || settings == null || bodies == null)
            {
                return 0;
            }

            _outsideWindow = settings.OutsideWindow;
            for (int i = 0; i < bodies.Count; i++)
            {
                CelestialBody body = bodies[i];
                if (body == null || body.orbitDriver == null || body.orbit == null)
                {
                    continue;
                }
                if (settings.IsBodyEnabled(body.bodyName)
                    && models.TryGet(body.bodyName, out BodyModel model))
                {
                    Driven[body] = model;
                    Parents.Add(body.orbit.referenceBody);
                    PinRotationPeriod(body, model);
                }
            }
            return Driven.Count;
        }

        /// <summary>Gives a tidally locked body a fixed spin rate instead of a derived one.</summary>
        // CBUpdate derives rotationPeriod from orbit.period, which follows the osculating a;
        // UT/period is ~423 revolutions for the Moon, so a 1% wobble becomes hundreds of
        // degrees of spin. We clear tidallyLocked and compute our own period.
        private static void PinRotationPeriod(CelestialBody body, BodyModel model)
        {
            if (!body.tidallyLocked)
            {
                return;
            }
            body.tidallyLocked = false;
            body.rotationPeriod = model.MeanOrbitalPeriod();
        }

        public static void Clear()
        {
            Driven.Clear();
            Parents.Clear();
        }

        public static bool IsDriven(CelestialBody body)
        {
            return body != null && Driven.ContainsKey(body);
        }

        /// <summary>True if any driven body orbits the given parent.</summary>
        // Lets the encounter solver skip patches it could never improve, which is most of
        // them, without paying a solve to find out.
        public static bool HasDrivenChildren(CelestialBody parent)
        {
            return parent != null && Parents.Contains(parent);
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
            using (HarmonicOrbitsProfiler.OrbitWrite.Sample())
            {
                OrbitWriter.Write(body.orbit, model, ut, _outsideWindow);
            }
            return true;
        }
    }
}
