using System.Reflection;
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

            new HarmonyLib.Harmony("HarmonicOrbits").PatchAll(Assembly.GetExecutingAssembly());
            GameEvents.onLevelWasLoaded.Add(OnLevelLoaded);
        }

        private void OnLevelLoaded(GameScenes scene)
        {
            BodyOrbitUpdater.Clear();
            if (!EnsureLoaded() || !Settings.Enabled)
            {
                return;
            }
            if (scene != GameScenes.SPACECENTER && scene != GameScenes.TRACKSTATION
                && scene != GameScenes.FLIGHT)
            {
                return;
            }

            int driven = BodyOrbitUpdater.Rebuild(Models, Settings);
            if (driven == 0)
            {
                Debug.LogError("[HarmonicOrbits]: no bodies matched; " + Models.Count
                    + " model(s) loaded, check the BODY names in the config");
            }
            else if (Settings.DumpModels)
            {
                Debug.Log("[HarmonicOrbits]: driving " + driven + " body/bodies in " + scene);
            }
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
    }
}
