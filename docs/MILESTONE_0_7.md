# Milestone 0.7: Factions

## Objective

Add a minimal durable faction foundation to the existing World. The milestone supports faction identity and allegiance, bounded reputation, explicit hostility, and container ownership metadata so later systems have a consistent source of truth.

This is a domain foundation, not faction simulation or a player-facing loop. Use the shared World, stable entity IDs, existing settlement containers, and current validated snapshot path.

## Included

- A small fixed set of validated faction IDs and optional allegiance on World-owned entities.
- Explicit entity-to-faction reputation bounded to [0, 100].
- Explicit directed hostility between distinct factions. Hostility is independent of personal reputation and has no autonomous updates.
- Optional faction owner ID on existing settlement resource containers. Ownership is metadata only; this milestone does not define access permissions.
- Snapshot persistence and pre-replacement validation for every new faction reference.
- Deterministic tests for success, invalid references, boundary values, save/load, and failure atomicity.

## Excluded

- Faction AI, diplomacy actions, alliances, demands, laws, crime, territory, war, raids, or autonomous reputation changes.
- Ownership enforcement, theft, property transfer, faction-owned markets, or access policy.
- A player-facing faction menu/editor, new UI, new ECS/framework, parallel inventory, or new serialization system.
- Regional simulation, multiple settlements, or expanding the 0.8 market/caravan rules.

## Acceptance scenario

1. Define two fixed faction IDs and create aligned and unaligned entities in the existing World. Confirm invalid/duplicate faction IDs and stale assignments are rejected.
2. Set independent reputation values for one entity toward both factions. Verify the [0, 100] bounds, explicit update behavior, and atomic rejection when an update would exceed a bound.
3. Set a directed hostile stance from faction A to faction B. Verify faction B's reverse stance is unchanged and a self-hostility relation is rejected.
4. Assign optional faction owner metadata to existing settlement containers. Confirm unowned containers remain valid and existing atomic transfers retain their behavior.
5. Save and load the World and verify allegiance, reputation, hostility, and owner metadata. Malformed, dangling, duplicate, or out-of-range references must reject the entire load without replacing the current World.
6. Repeat with identical inputs and operation order; durable results must be equivalent. Record actual test/build results and limitations.

## Handoffs and dependencies

- Reuse World/entity identity and snapshots (#3, #11), shared-character integration (#138), and existing settlement containers (#144).
- Start implementation after the accepted 0.3 integrated scenario (#325), avoiding overlapping durable-state schema changes.
- F07 issues are [#337](https://github.com/jmart985/Unnamed-Project-2026/issues/337), [#338](https://github.com/jmart985/Unnamed-Project-2026/issues/338), [#339](https://github.com/jmart985/Unnamed-Project-2026/issues/339), and [#340](https://github.com/jmart985/Unnamed-Project-2026/issues/340). The issue acceptance criteria are the task-level source of truth.
- Planning issue: [#222](https://github.com/jmart985/Unnamed-Project-2026/issues/222).

## Validation

Run from the repository root:

```powershell
dotnet test tests\Simulation.Tests\Simulation.Tests.csproj --configuration Release --no-restore
dotnet build UnnamedProject2026.slnx --configuration Release
```

The deterministic integrated scenario and the commands/results actually observed are recorded in [Faction validation](VALIDATION_0_7.md). Do not claim an engine/UI run for domain-only changes.

## Design references

- [Long-term vision at source commit 3e9de38](https://github.com/jmart985/Unnamed-Project-2026/blob/3e9de38abf0bfd07849a612bbedc4e01c977ba71/docs/LONG_TERM_VISION.md): factions participate in a larger simulation, but scale is earned by proving smaller layers first.
- [Technical architecture](TECHNICAL_ARCHITECTURE.md): authoritative World ownership, stable IDs, and validated snapshots.
- [Decisions log](DECISIONS.md): D063 records the provisional faction boundaries.

This milestone is accepted when every linked task is accepted, the [recorded faction scenario](VALIDATION_0_7.md) passes, snapshot rejection preserves the live World, and no unmet acceptance check is left without an explicit follow-up or deferral.
