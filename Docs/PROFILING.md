# HarmonicOrbits — Profiler Markers

Every region of the mod that costs measurable time raises a Unity profiler marker, so a
capture attributes frametime to the work by name instead of leaving it inside
`BehaviourUpdate` or whichever stock method the patch hangs off. The markers are declared in
one table, `HarmonicOrbits/Profiling/HarmonicOrbitsProfiler.cs`, and every name is prefixed
`HarmonicOrbits.` so a capture can be filtered to this mod.

Markers ship in every build. One that is not being recorded costs a native call that returns
immediately, so there is nothing to switch on before taking a capture and no separate build
that measures something a player's build would not.

---

## What is instrumented

| Marker | Site | When it runs |
|---|---|---|
| `HarmonicOrbits.OrbitUpdate` | `OrbitDriverPatch.Prefix` | Every orbit driver, every physics tick, vessels included |
| `HarmonicOrbits.OrbitUpdate.Write` | ↳ `BodyOrbitUpdater.Apply` | Only for a driven body: model evaluation plus `SetOrbit` |
| `HarmonicOrbits.CalculatePatch` | `ReosculatingSolver` | Per patch per frame while a conic chain is drawn |
| `HarmonicOrbits.CalculatePatch.Probe` | ↳ `Probe` | Each solve on a copy, to find the crossing. One when the warm cache confirms, two when it does not |
| `HarmonicOrbits.CalculatePatch.Final` | ↳ the single stock solve | Once per patch, with the body re-osculated at the crossing |
| `HarmonicOrbits.Load.Settings` | `HarmonicOrbitsSettings.Load` | Once, on the first scene with a loaded database |
| `HarmonicOrbits.Load.Pack` | `CoefficientPackLoader.LoadAll` | Once, alongside the settings |
| `HarmonicOrbits.Rebuild` | `BodyOrbitUpdater.Rebuild` | Once per scene load |

---

## Reading a capture

`OrbitUpdate` fires for every `OrbitDriver`, so its call count is roughly bodies plus
on-rails vessels per tick, while `OrbitUpdate.Write` fires only for the handful of bodies
actually driven. A large gap between the two counts is expected and is the cost of the
prefix's early-out, not of the model.

Inside `CalculatePatch`, the two nested markers say where the time went and how well the
warm-start cache is doing:

- **one `Probe` per `CalculatePatch`** means last frame's crossing confirmed, the cheap path
- **two `Probe`s** means it was stale and the solver fell back to a cold estimate
- **no `Probe` and no `Final`** means the early-out fired: nothing driven under this patch's
  reference body and nothing driven being left, so stock ran untouched

`Final` should always be exactly one per `CalculatePatch` that did any work. More than one
would mean the caller's patch was solved twice, which is the defect that made periapsis
predictions worse rather than better; see PLUGIN_STRUCTURE.md §6, Phase 6.

Reference measurements on .NET 10, one `CalculatePatch` with a lunar encounter: stock
0.147 ms, cold 0.463 ms, warm 0.334 ms. A patch under a parent with nothing driven costs
0.0002 ms.
