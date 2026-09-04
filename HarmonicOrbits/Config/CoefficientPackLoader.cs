using System;
using System.IO;
using UnityEngine;

namespace HarmonicOrbits
{
    /// <summary>Loads every coefficient pack shipped beside the plugin.</summary>
    public static class CoefficientPackLoader
    {
        private const string PackFolder = "GameData/HarmonicOrbits/Bodies";

        /// <summary>Reads all packs; returns an empty registry if none load.</summary>
        public static ModelRegistry LoadAll()
        {
            var registry = new ModelRegistry();
            string dir = PackDirectory();
            if (!Directory.Exists(dir))
            {
                Debug.LogError("[HarmonicOrbits]: no pack folder at " + dir);
                return registry;
            }

            foreach (string path in Directory.GetFiles(dir, "*.bin"))
            {
                try
                {
                    using (FileStream fs = File.OpenRead(path))
                    {
                        registry.AddRange(ModelReader.Read(fs));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("[HarmonicOrbits]: " + Path.GetFileName(path)
                        + " failed to load: " + ex.Message);
                }
            }

            if (registry.Count == 0)
            {
                Debug.LogError("[HarmonicOrbits]: no models loaded from " + dir);
            }
            return registry;
        }

        // GetFullPath because ApplicationRootPath ends in "/../"; keeps log paths readable.
        private static string PackDirectory()
        {
            return Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath, PackFolder));
        }
    }
}
