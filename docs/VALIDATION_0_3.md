# Milestone 0.3: Creature Core validation

## C03d: Integrated creature-core scenario

**Date:** 2026-10-02

**Issue:** [#325](https://github.com/jmart985/Unnamed-Project-2026/issues/325)

### Fixed inputs and observations

The domain scenario loads `data/species.json`, creates a new World, and spawns a `creature` followed by a `wolf`, both at `(0, 0, 0)` with satiety `70`. A `SimulationClock` advances seven 0.1-second ticks (simulation time `0.7`); each tick advances needs and wandering with decision seed `42`. The species select their first trip at tick `1`, then retain their deterministic result for the remaining ticks. Satiety ends at `69.86` for the creature and `69.832` for the wolf. The configured goals are `(1, 0, 0)`, `(3, 0, 0)`, and `(5, 0, 0)`. The scenario verifies:

- The creature temperament is `(boldness 0.3, curiosity 0.3, sociability 0.5)` and selects cautious roaming toward `(1, 0, 0)`.
- The wolf temperament is `(boldness 0.85, curiosity 0.65, sociability 0.45)` and selects curious exploration toward `(5, 0, 0)`.
- Capturing the creature succeeds once; repeat capture returns `AlreadyCaptive`, and an unknown creature ID returns `TargetNotFound` without changing the captive state.
- An invalid captivity enum in a snapshot is rejected before replacing either entity or resetting active navigation/actions.
- Loading the valid snapshot at simulation time `0.7` preserves both creature states and durable JSON. Transient trips/actions are canceled rather than restored.
- Running the same fixed inputs and operations twice produces equal behavior results and serialized durable state.

The complete simulation suite also runs the existing Ant Farm acceptance scenario, covering hunger, navigation, entity identity, food consumption, hauling/resource conservation, and save/load.

### Validation

SDK: .NET `10.0.401`.

```text
dotnet restore UnnamedProject2026.slnx
```

Passed; all three solution projects restored.

```text
dotnet test tests/Simulation.Tests/Simulation.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~CreatureCoreScenarioTests
```

Passed: 1 test, 0 failures, 0 skipped.

```text
dotnet test tests/Simulation.Tests/Simulation.Tests.csproj --configuration Release --no-restore
```

Passed: 334 tests, 0 failures, 0 skipped.

```text
dotnet build UnnamedProject2026.slnx --configuration Release --no-restore
```

Passed: 0 warnings, 0 errors.

Godot headless was not run. This issue adds only a domain-level test and validation record; no Godot scene/runtime integration changed, and no Godot executable was available on `PATH`.
