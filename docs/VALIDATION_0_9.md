# Milestone 0.9: Vertical Integration validation

## V09a single-region scenario

`RegionIntegrationScenarioTests.OneRegionComposesCoreSystemsAndRestoresOnlyDurableState`
runs the same explicit inputs and operation order twice, comparing combat,
transaction, roaming result, the complete serialized World, and external caravan
cargo. This test composes existing services only. Prerequisites #325, #340, and
#333 are accepted; E08d separately verifies production feeding the market.

Inputs: one World, human `entity-000001` at `(0, 0, 0)`, creature
`entity-000002` at `(1, 0, 0)` with satiety 70, both spawned from the repository
species catalog. One settlement has a capacity-40 `market` stockpile owned by
`settlers`, containing six food and ten coin. Factions are `settlers` and
`visitors`; the human belongs to `settlers` and has reputation 35 toward
`visitors`. Settlers are hostile toward visitors, with the reverse explicitly
non-hostile. The creature is unaligned. Roaming uses decision seed 42 and goals
`(2, 0, 0)` and `(5, 0, 0)`.

The creature starts with a directed relationship toward the human of familiarity
5, trust 30, fear 0, loyalty 25. A valid ten-damage Torso melee hit within reach
two applies the existing `harmed` event: relationship values become **8, 15, 18,
13**, respectively, and Torso health decreases by exactly ten. No reverse
relationship is created. Existing capture/lifecycle operations make the living
creature Captured, then Captive.

The finite capacity-13 caravan contains one food and 12 coin. It opens
`region-visit`, arrives explicitly, and purchases three market food at the
configured two coin per unit. Final settlement totals are **3 food / 16 coin**;
caravan totals are **4 food / 6 coin**. Seven food and 22 coin are conserved.
Faction identities, allegiance, ownership, reputation/hostility, relationship,
and captivity are asserted before and after save/load.

One 0.1-second tick advances both entities' needs and starts a creature wandering
trip through the existing service and fake navigation boundary. The test then
pauses at that tick boundary. The snapshot contains durable state only, while
the arrived caravan visit and navigation request remain active in memory.

## Rejection and restore checks

- Purchasing four food when only three remain rejects explicitly with
  `InsufficientMarketGoods`; all World and caravan resource/social state remains
  byte-equivalent to the prior projection.
- Reputation and stockpile ownership updates referring to an unknown faction
  return `UnknownFaction`, each preserving the entire prior state.
- A zero-damage melee request returns `InvalidRequest`, applies no relationship
  event, and preserves health and all durable/resource state.
- A snapshot referencing an unknown market owner throws `InvalidDataException`
  before replacement or transient cleanup. Original human and creature references,
  the arrived visit, active entity action, and InProgress navigation survive.
- Loading the valid snapshot restores identical durable World data and simulation
  time 0.1, with the clock paused. The caravan service cancels the active visit;
  wandering cleanup cancels the navigation request, and restored entities have
  no active actions. Trading through the old visit returns `AlreadyCancelled`
  without mutating restored stock or external cargo.

## Actual validation (2026-10-03)

Windows; .NET SDK `10.0.401`; pinned Godot
`4.7.2.stable.mono.official.ed1daf0bf`.

| Exact command | Result |
| --- | --- |
| `dotnet test tests/Simulation.Tests/Simulation.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~RegionIntegrationScenarioTests --verbosity minimal` | Passed: 1 test, 0 failed, 0 skipped; scenario executed twice. |
| `dotnet test tests/Simulation.Tests/Simulation.Tests.csproj --configuration Release --no-restore --verbosity minimal` | Passed: 497 tests, 0 failed, 0 skipped, including existing creature, combat/social, faction, economy, and Ant Farm regressions. |
| `dotnet build UnnamedProject2026.slnx --configuration Release --verbosity minimal` | Passed: 0 errors; 2 existing CS8604 warnings in `game/scripts/EntityView.cs`, lines 126 and 178. |
| `& 'C:/Developement/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64.exe' --path game --headless --quit` | Passed: exit 0, no engine errors, with normal user-directory access and the already-built unchanged Debug game assembly. Startup smoke only. |

No separate formatter/linter is configured; `git diff --check` verifies
whitespace. Repository CI uses the solution build and full simulation suite.

## Explicit limits and deferred product behavior

This is domain integration evidence for one fixed region/settlement, with
explicit operations and fake navigation. No new regional simulation or UI was
implemented; no windowed scenario was run. The Godot check proves startup only.
Farming/recipe production remains covered by the accepted E08d scenario; this
test starts from its six-food output and composes it with the other systems.

Health and injuries are outside the existing snapshot contract: after load the
creature's Torso returns to its species-derived maximum while its durable harm
relationship and captivity persist. The test explicitly checks and documents
this boundary, rather than claiming health continuity. Health/facing persistence
is deferred in existing issue #302; it is not implemented by this integration
gate. Farm/work-order progress, actions/navigation, and external caravan cargo
are also outside durable snapshots as documented by earlier milestones.
Ownership and hostility are metadata; this scenario does not infer trade access
policy or autonomous hostility. No uncovered acceptance criterion requires new
product mechanics, migrations, or compatibility changes.
