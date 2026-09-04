using HarmonyLib;

namespace HarmonicOrbits
{
    /// <summary>Rewrites a driven body's elements before KSP propagates them.</summary>
    // Patches the internal overload; the public no-arg one delegates to it, and Kopernicus
    // has no hook at this point.
    [HarmonyPatch(typeof(OrbitDriver), "updateFromParameters", new[] { typeof(bool) })]
    public static class OrbitDriverPatch
    {
        // ReSharper disable once UnusedMember.Local
        // ReSharper disable once InconsistentNaming
        private static void Prefix(OrbitDriver __instance)
        {
            if (BodyOrbitUpdater.Count == 0 || Planetarium.fetch == null)
            {
                return;
            }
            BodyOrbitUpdater.Apply(__instance.celestialBody, Planetarium.fetch.time);
        }
    }
}
