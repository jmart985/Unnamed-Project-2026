# Milestone 1.0 validation — First Real Game

Issue [#322](https://github.com/jmart985/Unnamed-Project-2026/issues/322) composes
completed session #319, eating #321, squad #312, and greeting #313 on the existing
400 × 400 fixed map. No construction, combat, faction gameplay, captivity, economy,
or world streaming is added by this acceptance gate.

## Environment and commands

Recorded 2026-10-03 on Windows: .NET SDK **10.0.401**, Godot **4.7.2 stable Mono**
official `ed1daf0bf`; rendered mode uses OpenGL 3.3 compatibility on AMD Radeon RX 6400.

From the repository root:

```powershell
dotnet restore UnnamedProject2026.slnx
dotnet build UnnamedProject2026.slnx --configuration Release
dotnet test tests/Simulation.Tests/Simulation.Tests.csproj --configuration Release --no-restore
dotnet test tests/Simulation.Tests/Simulation.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~FirstGameIntegrationTests
dotnet build game/UnnamedProject2026.csproj --configuration Debug --no-restore
& "C:\Developement\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path game --headless --quit
& "C:\Developement\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path game --headless -- --validate-first-game
```

Godot uses the Debug assembly. In PowerShell, the GUI executable can return before
its child finishes; `Start-Process -WindowStyle Hidden -Wait -PassThru` was used to
collect the actual scenario exit code. Success requires exit 0 **and** the marker
`FIRST GAME VALIDATION PASSED` in `user://logs/godot.log`; startup alone is insufficient.
To render the same scenario, omit `--headless`. Existing squad screenshots are in
[the squad validation record](VALIDATION_SQUAD.md).

## Deterministic automated scenario

`FirstGameIntegrationTests` executes the complete case twice and compares the final
serialized state. Each run starts profile `Ada`, seed 42, 14 entities (10 human NPCs,
3 starter creatures, 1 player), four finite food sources totaling 400 edible units,
100 wood, and the starter stockpile.

1. Advance the fixed clock/needs; accept a player movement command and apply its
   target to World position. Eat one food unit within reach. Nutrition applies once,
   clamped to 100; food totals 399 and consumed count is one.
2. Add an empty test source. Missing/empty/out-of-range requests reject without
   changing the serialized World or consumed count.
3. Add two human NPCs with no duplicate membership. Move to (4, 0, 3), follow the
   player, then stop through the existing navigation/action service. Nonfinite
   movement rejects and preserves the stop order.
4. Reuse wood hauling so ground wood and carried wood both exist at the save
   boundary, with an active delivery. Their sum with storage remains 100.
5. Greet the cow within one unit. Only player-to-cow familiarity becomes five;
   trust/fear/loyalty stay zero, and no reverse relation is created. Missing,
   out-of-range ape, and unsupported wolf requests leave state unchanged.
6. Pause; reject an invalid membership snapshot without cancelling the active haul.
   Load the valid snapshot and compare the entire serialized durable state, including
   time, player identity/profile/position/satiety, squad IDs, relationships, inventories,
   and resource locations/counts. All actions/orders/navigation clear, food stays 399,
   wood stays 100, and membership count stays two.

Domain navigation completion/positions are controlled explicitly in this deterministic
case. The engine scenario below verifies actual movement and scene wiring.

## Godot integration scenario

`--validate-first-game` extends the existing squad harness instead of duplicating it.
It starts seed 42 and the `Squad validation` profile, exercises actual controller
input across physics frames, advances the fixed clock and player hunger, then invokes
the same eating and greeting handlers used by the UI. It rejects far/empty eating,
unsupported/far/missing greetings, repeated membership, and an invalid squad target.

Two selected NPCs receive the actual squad button signals and move using the real
navigation mesh/adapter to (4, 0, 3), follow to (7, 0, -3) then (10, 0, -3), and stop.
The inspector reports arrival and membership. A paused snapshot is captured,
invalid load is rejected atomically, the whole session is reset with seed 43/profile
`Reset session`, and the saved snapshot is restored. Full durable serialization
matches, loaded inspector shows membership with order None, no actions remain, and
resource totals are 399 food/100 wood. Snapshots are in memory, leaving the player's
disk save slot untouched.

## Actual results and recovery

- Focused integration case passes and repeats identical final snapshots internally.
  The full suite passes **509 tests**, zero failed/skipped.
- Restore and Release solution build pass. Two existing EntityView CS8604 warnings
  remain. Local NuGet vulnerability metadata is unavailable (NU1900), including on
  retry with normal network permissions; local audit freshness is unverified.
- Debug game build and documented headless startup pass.
- Headless and rendered full-loop scenarios exit 0 with the explicit success marker.
  No unhandled exceptions or stuck actions remain at their completion boundary.
- The initial engine run failed with `ArgumentNullException` for `_catalog` in
  `FoodConsumptionService.TryPlayerEat`. Main had constructed the food service using
  its autonomous-only overload. Supplying the already-loaded species catalog fixes
  player eating; the subsequent full-loop run passes. The failure is retained here
  as evidence that domain tests alone did not validate scene wiring.

This bounded scenario does not replace the historical ten-minute Ant Farm regression,
test every possible route, validate health/facing persistence (#302), or establish
game balance, polished UX, formations, or long-term stability. Schema 9 membership
and schema 4–8 migrations are additionally covered by the focused squad tests.
