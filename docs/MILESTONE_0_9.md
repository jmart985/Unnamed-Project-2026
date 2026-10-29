# Milestone 0.9: Vertical Integration

## Objective

Prove that selected accepted core systems can coexist in one deterministic test region. This milestone is an integration/evidence gate, not a commitment to implement every system in the long-term vision or to begin regional simulation.

## Included

- One fixed test region and one settlement using the existing World and resource containers.
- Compose accepted 0.3 creature, 0.5 combat, 0.6 relationship, 0.7 faction, and 0.8 local economy state.
- Exercise one finite settlement-market/caravan exchange and verify exact durable resource and currency totals.
- Verify relevant faction, creature, relationship, and snapshot state before and after the scenario.
- Add only a focused scenario/test harness and validation evidence. Track missing behavior as separate follow-up issues.

## Excluded

- Multiple settlements, regional markets/routes, autonomous caravan travel, migration, world events, streaming/LOD, or distributed simulation.
- New faction, combat, creature, social, or economy mechanics.
- Broad AI, UI, art, new inventory/persistence frameworks, or infrastructure that is not required to run the scenario.
- Treating the entire long-term vision as a 0.9 delivery commitment.

## Acceptance scenario

1. Construct a test region from fixed seed and explicit inputs containing one settlement, two known factions, one human, one creature, and an owned settlement container.
2. Use the accepted 0.8 flow to complete one finite market exchange. Assert exact goods and coin totals and verify faction ownership and social/creature state are coherent.
3. Repeat with identical inputs and operation order; results and durable state must be equivalent.
4. Attempt a rejected trade (insufficient stock/currency or capacity), a stale/unknown faction reference, and an invalid snapshot. Each failure must be explicit and must not change any participating resource, relationship, or the running World.
5. Save/load at the documented safe boundary. Durable state remains valid; transient caravan travel or action state is not falsely restored.
6. Record exact commands, inputs, actual observations, engine checks not run, and limitations. File separate follow-ups for unmet product behavior.

## Handoffs and dependencies

- Depends on acceptance of the 0.3 creature-core scenario (#325), 0.7 faction verification (#340), and 0.8 economy scenario (#333). All three prerequisite verification issues are now accepted and merged. Combat and relationship acceptance records are complete (#184 and #197).
- The sole scoped issue is [#341](https://github.com/jmart985/Unnamed-Project-2026/issues/341); it is limited to integration testing and evidence.
- Planning issue: [#224](https://github.com/jmart985/Unnamed-Project-2026/issues/224).

## Validation

Run from the repository root:

```powershell
dotnet test tests\Simulation.Tests\Simulation.Tests.csproj --configuration Release --no-restore
dotnet build UnnamedProject2026.slnx --configuration Release
```

Run the pinned Godot headless smoke command from `README.md` only when the executable is available, and record its actual result. The scenario issue is not complete until its fixed inputs, results, and any limitations are documented.

The fixed-input integration test and its actual results are documented in
[the 0.9 validation record](VALIDATION_0_9.md).

## Design references

- [Long-term vision at source commit 3e9de38](https://github.com/jmart985/Unnamed-Project-2026/blob/3e9de38abf0bfd07849a612bbedc4e01c977ba71/docs/LONG_TERM_VISION.md): systems should create observable interactions, with larger scales earned incrementally.
- [Technical architecture](TECHNICAL_ARCHITECTURE.md): one authoritative World, stable ownership, fixed ticks, and validated snapshots.
- [Milestone 0.8 acceptance](MILESTONE_0_8.md): local economy inputs and rejection behavior reused without expanding the trade system.
- [Decisions log](DECISIONS.md): D064 records the integration boundary.

This milestone is accepted when the integration scenario is repeatable, failure paths preserve exact state, actual evidence is recorded, and any missing behavior is explicitly deferred or tracked separately.
