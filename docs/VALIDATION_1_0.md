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
& "C:\Developement\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path game --headless -- --validate-session-profiles
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
It starts seed 42 and the `Squad validation` profile in survival-gathering mode:
four adult tree piles hold 48 wood, four loose-rock piles hold one rock each, and
the legacy free wood pile is absent. It exercises actual controller
input across physics frames, advances the fixed clock and player hunger, then invokes
the same eating and greeting handlers used by the UI. It rejects far/empty eating,
unsupported/far/missing greetings, repeated membership, and an invalid squad target.

Two selected NPCs receive the actual squad button signals and are expected to move
using the real navigation mesh/adapter to (4, 0, 3), follow to (7, 0, -3) then
(10, 0, -3), and stop.
The inspector reports arrival and membership. A paused snapshot is captured,
invalid load is rejected atomically, the whole session is reset with seed 43/profile
`Reset session`, and the saved snapshot is restored. Full durable serialization
matches, loaded inspector shows membership with order None, no actions remain, and
resource totals are 399 food/48 wood. Snapshots are in memory, leaving the player's
disk save slot untouched.

## Latest audit run

On 2026-10-23, the Release solution build and all **636 Simulation.Tests** passed.
The Debug game build passed with zero warnings/errors. After squad members began
receiving deterministic nearby movement destinations and separated follow positions,
three consecutive Godot 4.7.2 Mono headless runs of `--validate-first-game` each
exited 0 and printed `FIRST GAME VALIDATION PASSED`. All three used the same built-in
seed 42 and verified movement, eating, squad move/follow/stop, directed greeting,
rejected commands, reset/load, 399 food, and 48 wood. Godot still prints renderer
resource-leak warnings during headless shutdown after the success marker; these did
not change the successful process exit code or scenario result.

The historical navigation failure was reproduced before the fix: one selected NPC
remained `InProgress` while another was reported `Cancelled` at (4, 0, 3). The squad
service now assigns stable nearby endpoints to move orders and spaced positions around
the player for follow orders, preserving the clicked move target and the existing
follow distance bands. Focused unit tests verify the assigned destinations are
separated and remain near the requested location. Issue
[#450](https://github.com/jmart985/Unnamed-Project-2026/issues/450) tracks this repair.

The focused `--validate-session-profiles` check passes. It activates the normal
Start New Game button, verifies the tree/rock survival start, resets without losing
that profile, then verifies the deterministic baseline setup. PR CI runs this check
with Godot 4.7.2 Mono; it does not replace the broader first-game scenario.

The startup assertion also exposed that all validation sessions were inheriting the
player's survival-gathering resources even when a focused feature scenario expected
the deterministic baseline wood pile. Issue [#449](https://github.com/jmart985/Unnamed-Project-2026/issues/449)
separates those profiles and adds a focused startup/reset check.

The original engine run's `ArgumentNullException` for `_catalog` in
`FoodConsumptionService.TryPlayerEat` remains as historical evidence that domain
tests alone did not validate scene wiring. Its fix is already in the recorded
implementation; the present blocker is the squad navigation failure above.

This bounded scenario does not replace the historical ten-minute Ant Farm regression,
test every possible route, validate health/facing persistence (#302), or establish
game balance, polished UX, formations, or long-term stability. Schema 9 membership
and schema 4–8 migrations are additionally covered by the focused squad tests.
