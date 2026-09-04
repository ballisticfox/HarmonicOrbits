using UnityEngine;

namespace HarmonicOrbits
{
    /// <summary>Entry point; loads once at startup and survives scene changes.</summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class HarmonicOrbits : MonoBehaviour
    {
        public static HarmonicOrbits Singleton { get; private set; }
        public static ModelRegistry Models { get; private set; }
        public static HarmonicOrbitsSettings Settings { get; private set; }

        // ReSharper disable once UnusedMember.Global
        public void Awake()
        {
            Singleton = this;
            DontDestroyOnLoad(this);
            GameEvents.onLevelWasLoaded.Add(OnLevelLoaded);
        }

        private void OnLevelLoaded(GameScenes scene)
        {
            if (!EnsureLoaded() || !Settings.Enabled)
            {
                return;
            }
            if (scene != GameScenes.SPACECENTER && scene != GameScenes.TRACKSTATION
                && scene != GameScenes.FLIGHT)
            {
                return;
            }
            ApplyAll();
        }

        // GameDatabase holds no configs at Startup.Instantly, so settings and packs load on
        // the first scene where it is ready. Loading in Awake leaves every body disabled.
        private static bool EnsureLoaded()
        {
            if (Settings != null)
            {
                return true;
            }
            if (GameDatabase.Instance == null || !GameDatabase.Instance.IsReady())
            {
                return false;
            }

            Settings = HarmonicOrbitsSettings.Load();
            Models = CoefficientPackLoader.LoadAll();
            if (Settings.DumpModels)
            {
                ModelDump.Log(Models);
            }
            return true;
        }

        /// <summary>Writes each enabled body's elements at the current UT.</summary>
        // Phase 2 only: written once per scene, then left to drift. Phase 3 replaces this
        // with a per-FixedUpdate rewrite.
        public void ApplyAll()
        {
            if (Models == null || FlightGlobals.Bodies == null || HighLogic.CurrentGame == null)
            {
                return;
            }

            double ut = Planetarium.GetUniversalTime();
            int applied = 0;
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                CelestialBody body = FlightGlobals.Bodies[i];
                if (body == null || body.orbitDriver == null || body.orbit == null)
                {
                    continue;
                }

                if (!Settings.IsBodyEnabled(body.bodyName)
                    || !Models.TryGet(body.bodyName, out BodyModel model))
                {
                    continue;
                }

                OrbitWriter.Write(body.orbit, model, ut, Settings.OutsideWindow);
                body.orbitDriver.UpdateOrbit();
                applied++;
            }

            if (applied == 0)
            {
                Debug.LogError("[HarmonicOrbits]: no bodies matched; "
                    + Models.Count + " model(s) loaded, check the BODY names in the config");
            }
            else if (Settings.DumpModels)
            {
                Debug.Log("[HarmonicOrbits]: applied " + applied + " body/bodies at UT " + ut);
            }
        }
    }
}
