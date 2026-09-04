# HarmonicOrbits

Using harmonic fits to characterize orbits to represent details typically limited to
n-body or tabulated methods.

Bodies in KSP move on conics whose elements are constants. This mod makes each element
a function of time (a polynomial plus a short sum of sinusoids fitted to a real
ephemeris) so the bodies follow their actual paths without an ephemeris table or an
n-body integrator.

Elements are expected to be valid from 1951-2051 but can extend much longer without severe error.

## Building

The build uses [KSPBuildTools](https://github.com/KSPModdingLibs/KSPBuildTools), which
pulls KSP's own assemblies out of a local install rather than checking DLLs into the repo.

1. Copy `HarmonicOrbits.props.user.example` to `HarmonicOrbits.props.user` and point
   `KSPBT_GameRoot` at your KSP install.
2. `dotnet build HarmonicOrbits.sln`

The build writes an installable mod folder to `GameData/HarmonicOrbits` that you can copy to your GameData.