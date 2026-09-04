using Unity.Profiling;

namespace HarmonicOrbits
{
    /// <summary>Every profiler marker the mod raises, in one table.</summary>
    // See Docs/PROFILING.md. Markers ship in every build; an unrecorded one costs only a
    // native call that returns immediately.
    internal static class HarmonicOrbitsProfiler
    {
        #region Per-tick frame work

        /// <summary>The updateFromParameters prefix, which runs for every orbit driver.</summary>
        internal static readonly HarmonicMarker OrbitUpdate = Marker("OrbitUpdate");

        /// <summary>Evaluating a body's model and writing the six elements.</summary>
        internal static readonly HarmonicMarker OrbitWrite = Marker("OrbitUpdate.Write");

        #endregion

        #region Encounter solving, per patch per frame while a chain is drawn

        /// <summary>The whole re-osculating CalculatePatch, including the stock solves.</summary>
        internal static readonly HarmonicMarker CalculatePatch = Marker("CalculatePatch");

        /// <summary>One probe solve on a copy, to learn where and when the crossing is.</summary>
        internal static readonly HarmonicMarker CalculatePatchProbe = Marker("CalculatePatch.Probe");

        /// <summary>The single stock solve on the caller's patch, with the body re-osculated.</summary>
        internal static readonly HarmonicMarker CalculatePatchFinal = Marker("CalculatePatch.Final");

        #endregion

        #region Off the frametime path

        /// <summary>Reading the settings node.</summary>
        internal static readonly HarmonicMarker LoadSettings = Marker("Load.Settings");

        /// <summary>Decoding the coefficient packs.</summary>
        internal static readonly HarmonicMarker LoadPack = Marker("Load.Pack");

        /// <summary>Resolving which bodies this scene drives.</summary>
        internal static readonly HarmonicMarker Rebuild = Marker("Rebuild");

        #endregion

        private static HarmonicMarker Marker(string name)
        {
            return new HarmonicMarker("HarmonicOrbits." + name);
        }
    }

    /// <summary>One named region, timed for as long as a using block holds it.</summary>
    // Begin/End and Profiler.BeginSample carry [Conditional("ENABLE_PROFILER")], evaluated at
    // the call site. This assembly isn't built by Unity and lacks that symbol, so those calls
    // compile away. Auto() has no such attribute.
    internal struct HarmonicMarker
    {
        private readonly ProfilerMarker _marker;

        internal HarmonicMarker(string name)
        {
            _marker = new ProfilerMarker(name);
        }

        /// <summary>Times the enclosing using block.</summary>
        internal ProfilerMarker.AutoScope Sample()
        {
            return _marker.Auto();
        }
    }
}
