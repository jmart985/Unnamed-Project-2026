# Milestone 0.7: Factions validation

## Integrated deterministic recovery scenario

`FactionFoundationScenarioTests.FactionFoundationIsDeterministicAndRecoversFromInvalidSnapshot` uses two factions (`north`, `south`), a human aligned with `north`, an unaligned creature, and a second human as a relationship target. It sets separate reputation values of 35 and 80, records `north`-to-`south` hostility without reverse hostility, and assigns an owner only to one of two settlement stockpiles. A transfer moves three of five wood units into the owned stockpile; the entity-to-entity trust value remains 20.

The scenario rejects unknown faction/entity/container references, an out-of-range reputation update, and self-hostility while preserving prior values. It then attempts to load a snapshot with an unknown stockpile owner, verifies the current World is unchanged and transient state was not reset, and loads the valid snapshot. Allegiance, reputation, directed hostility, owner metadata, transferred resources, and relationship values match after restoration. Running the fixed setup twice produces identical serialized snapshots.

## Validation

SDK: .NET `10.0.401`.

```text
dotnet restore UnnamedProject2026.slnx --verbosity minimal
```

Passed; all three solution projects restored.

```text
dotnet test tests/Simulation.Tests/Simulation.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~FactionFoundationScenarioTests --verbosity minimal
```

Passed: 1 test, 0 failures, 0 skipped.

```text
dotnet test tests/Simulation.Tests/Simulation.Tests.csproj --configuration Release --no-restore --verbosity minimal
```

Passed: 403 tests, 0 failures, 0 skipped. This includes the existing faction identity, relations, and ownership tests, plus the Ant Farm and relationship regression tests.

```text
dotnet build UnnamedProject2026.slnx --configuration Release --no-restore --verbosity minimal
```

Passed: 0 errors, 2 nullable warnings in `game/scripts/EntityView.cs` (lines 126 and 178; both are possible null arguments to `AddJoint`). No Godot executable was available on `PATH`, so the Godot headless check was not run.
