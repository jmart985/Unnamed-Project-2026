# Unity simulation foundation

This is the first migration gate authorized by the owner after the repository integration audit. It preserves the Godot application and shared domain rules while proving that the existing simulation can run inside Unity. It does not yet migrate navigation, employment execution, farming/crafting interfaces, models or the full gameplay loop.

## Structure

- `src/Simulation` builds both `net10.0` for Godot and `netstandard2.1` for Unity from the same source.
- The .NET SDK compiles modern C# syntax externally. Unity imports the resulting managed assembly; it does not recompile the domain source with its C# compiler.
- `scripts/sync-unity-domain.ps1` builds the portable assembly and copies it, its resolved runtime dependencies, and the catalog JSON into Unity. Generated DLL/data copies are ignored by Git. The authoritative data remains in `data/`.
- `unity/` is a Unity 6000.6.7f1 project. No Unity MCP bridge is configured or required for the command-line workflow.
- `unity/Assets/Scenes/SimulationFoundation.unity` presents entity markers and a clock/needs display with pause, save-checkpoint and restore controls. Checkpoints in this scene are in memory, not user save slots. Actors do not yet navigate or perform work.
- `SimulationCompatibilitySmoke` runs in both the Editor and the Windows Mono player. It checks the assembly target, starter population, ticking, pause, full snapshot round trip, rejected-load rollback, actual file replacement and preservation after a serialization failure.
- `tests/Simulation.UnityCompatibility.Tests` reuses the existing test sources against the built portable DLL. These tests execute on .NET 10; the separate Unity smoke checks establish actual Unity runtime compatibility for the exercised paths.

## Run

From the repository root in PowerShell:

```powershell
./scripts/sync-unity-domain.ps1
./scripts/validate-unity-foundation.ps1
```

The validator runs the original and portable assembly regression suites, then opens Unity in batch mode to run its smoke check and build a Windows Mono development player, then runs that player with `--simulation-smoke`. Logs are written to `work/`. Override `-UnityEditor` when the installation path differs. Close this Unity project in the Editor before running batch validation.

To inspect the scene, add the `unity` directory through Unity Hub's Add project from disk option, open `Assets/Scenes/SimulationFoundation.unity`, and press Play. The saved scene is a foundation fixture; preserve it or duplicate it before experimenting. The Windows build is generated under `unity/Builds/Windows`.

Do not edit imported `Simulation.dll` or copy Godot scripts into Unity. Modify the shared source and rerun the sync script. Preserve stable entity and catalog IDs. Views do not own independent hunger or inventory values.

## Compatibility decisions

Framework-only argument guards are expressed with compatible checks; generic enum validation uses the older equivalent API. Ordering remains ordinal. The .NET 10 `IReadOnlySet` storage property remains unchanged; the portable target exposes the same contents through `IReadOnlyCollection`. Record metadata has a conditional compiler shim. Portable file replacement uses `File.Replace` for an existing destination, preserving the previous save if replacement fails rather than deleting it first. System.Text.Json 10.0.0 and its resolved dependencies are supplied for the portable build.

## Validation and remaining boundary

The initial implementation passed 802 original-target tests and the same 802 tests against the portable DLL. The full original solution also built with zero warnings or errors. Unity Editor smoke, Windows Mono development build and standalone player smoke passed.

After bringing the foundation onto the main branch that includes the unified controls, the Release solution built with zero warnings or errors. All 814 tests passed against each domain assembly. The current Unity validation run stopped at editor license initialization, so the Editor smoke check, Windows build and standalone player smoke still need to be run against this revision with an activated editor.

This is not full gameplay parity, a visual asset certification, or an IL2CPP/WebGL/mobile compatibility claim. The first connected gameplay slice still needs a Unity navigation/movement adapter and the relevant food/hauling service orchestration, followed by actual arrival, interruption, reservation cleanup and save/load validation. Preserve the ordering identified in the repository integration audit rather than starting independent MonoBehaviour update loops for each domain system.
