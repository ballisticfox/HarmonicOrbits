# HarmonicOrbits

Using harmonic fits to characterize orbits to represent details typically limited to
n-body or tabulated methods.

Bodies in KSP move on conics whose elements are constants. This mod makes each element
a function of time - a secular polynomial plus a short sum of sinusoids fitted to a real
ephemeris - so the bodies follow their actual paths without an ephemeris table or an
n-body integrator. Validity runs 1951-2051.

## Building

The build uses [KSPBuildTools](https://github.com/KSPModdingLibs/KSPBuildTools), which
pulls KSP's own assemblies out of a local install rather than checking DLLs into the repo.

1. Copy `HarmonicOrbits.props.user.example` to `HarmonicOrbits.props.user` and point
   `KSPBT_GameRoot` at your KSP install.
2. `dotnet build HarmonicOrbits.sln`

The build writes an installable mod folder to `GameData/HarmonicOrbits` (gitignored):
the plugin, its `.version` file, and everything under `Resources/`.

`$(Version)` in `Directory.Build.props` is the only place the version is written; the
assembly attributes, `[KSPAssembly]`, and the `.version` file are all generated from it.
