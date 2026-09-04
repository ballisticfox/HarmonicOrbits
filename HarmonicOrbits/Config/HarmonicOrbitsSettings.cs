using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarmonicOrbits
{
    /// <summary>Plugin settings, read from HARMONIC_ORBITS_SETTINGS.</summary>
    public sealed class HarmonicOrbitsSettings
    {
        private const string NodeName = "HARMONIC_ORBITS_SETTINGS";

        private readonly Dictionary<string, bool> _bodies =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public bool Enabled = true;
        public bool ReosculateEncounters = true;
        public bool DumpModels;
        public OutsideWindowAction OutsideWindow = OutsideWindowAction.Continue;

        /// <summary>Defaults if the node is missing or malformed.</summary>
        public static HarmonicOrbitsSettings Load()
        {
            var settings = new HarmonicOrbitsSettings();
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(NodeName);
            if (nodes == null || nodes.Length == 0)
            {
                Debug.LogError("[HarmonicOrbits]: no " + NodeName + " found; using defaults");
                return settings;
            }

            ConfigNode node = nodes[0];
            settings.Enabled = ReadBool(node, "enabled", settings.Enabled);
            settings.ReosculateEncounters =
                ReadBool(node, "reosculateEncounters", settings.ReosculateEncounters);
            settings.DumpModels = ReadBool(node, "dumpModels", settings.DumpModels);

            string action = node.GetValue("outsideWindow");
            if (!string.IsNullOrEmpty(action))
            {
                try
                {
                    settings.OutsideWindow =
                        (OutsideWindowAction)Enum.Parse(typeof(OutsideWindowAction), action, true);
                }
                catch (ArgumentException)
                {
                    Debug.LogError("[HarmonicOrbits]: outsideWindow = '" + action
                        + "' is not continue or clamp; using " + settings.OutsideWindow);
                }
            }

            foreach (ConfigNode body in node.GetNodes("BODY"))
            {
                string name = body.GetValue("name");
                if (string.IsNullOrEmpty(name))
                {
                    Debug.LogError("[HarmonicOrbits]: a BODY node has no name; ignored");
                    continue;
                }
                settings._bodies[name] = ReadBool(body, "enabled", true);
            }
            return settings;
        }

        /// <summary>True if the body has an enabled entry in the config.</summary>
        public bool IsBodyEnabled(string bodyName)
        {
            return _bodies.TryGetValue(bodyName, out bool enabled) && enabled;
        }

        private static bool ReadBool(ConfigNode node, string key, bool fallback)
        {
            string raw = node.GetValue(key);
            if (string.IsNullOrEmpty(raw) || !bool.TryParse(raw, out bool value))
            {
                if (!string.IsNullOrEmpty(raw))
                {
                    Debug.LogError("[HarmonicOrbits]: " + key + " = '" + raw
                        + "' is not true or false; using " + fallback);
                }
                return fallback;
            }
            return value;
        }
    }
}
