# Implemented Simulation Ownership and Data Contracts

**Status: verified against `main` on 2026-10-22.** This is a description of current code, not a target architecture. Where current behavior is incomplete or inconsistent, the gap is called out explicitly. The repository's [implementation guidelines](IMPLEMENTATION_GUIDELINES.md) remain the rules for new work; this document does not authorize a rewrite, custom ECS, or global event bus.

## Layer boundaries

| Concern | Current owner | Contract |
| --- | --- | --- |
| Durable simulation state | `Simulation.World` and its `EntityRecord`, resource, settlement, relationship, and faction state objects | `World` is authoritative for entities and their stable IDs, position, needs, inventory, health/skills objects, action lifecycle, captivity, allegiance, parent references, settlement objects, relationships, factions, schedules, and session identity. Public world operations validate and mutate this state; consumers should use those operations rather than edit backing collections. See [`World.cs`](../src/Simulation/World.cs) and [`EntityRecord.cs`](../src/Simulation/EntityRecord.cs). |
| Entity-local state | `EntityRecord` plus owned state objects | Each record has a species ID, [`NeedsState`](../src/Simulation/Needs.cs), [`CharacterStats`](../src/Simulation/CharacterStats.cs), [`CharacterHealth`](../src/Simulation/CharacterHealth.cs), [`SkillSet`](../src/Simulation/SkillProgression.cs), [`ActionLifecycle`](../src/Simulation/Actions.cs), inventory, captivity and other runtime fields. Health is currently initialized with the six-region humanoid model for every species. Only player health is captured/restored today; NPC and creature health persistence is a known gap, not an implied capability. |
| Species and capabilities | Validated `SpeciesCatalog` / immutable `SpeciesDefinition` from [`data/species.json`](../data/species.json) | Species supply stable IDs, movement/hunger tuning, diet, body/diet categories, base stats, temperament, interaction anchors, and capability flags such as hauling, tool use, door use, tracking, and friendly interaction. `World`/services consult these definitions; species definitions do not own per-entity runtime state. `WorldSnapshotSerializer.Load` requires the catalog and derives stats from it. Adding a species can be data-only when it fits the existing item and capability contracts; the static item ID allowlist in `ItemIds.cs` means adding new item IDs is not fully data-only. See [`SpeciesCatalog.cs`](../src/Simulation/SpeciesCatalog.cs) and [`SpeciesDefinition.cs`](../src/Simulation/SpeciesDefinition.cs). |
| Simulation operations | Focused services in `src/Simulation/` | Services perform domain actions against `World` and return explicit result values. Examples include [`FoodConsumptionService`](../src/Simulation/FoodConsumptionService.cs), [`WoodHaulingService`](../src/Simulation/WoodHaulingService.cs), [`SquadOrderService`](../src/Simulation/SquadOrderService.cs), `RelationshipEventService` in [`RelationshipEventCatalog.cs`](../src/Simulation/RelationshipEventCatalog.cs), and [`InteractionResolver`](../src/Simulation/InteractionResolver.cs). Services are not uniformly pure: some own transient navigation/reservation/action state while asking `World` to apply durable results. Do not infer a universal component/plugin interface from these classes. |
| Presentation and engine integration | Godot nodes in `game/scripts/` | `PlayerCharacter : CharacterBody3D`, `EntityView : CharacterBody3D`, and the partial `Main : Node3D` compose input, navigation, scene nodes, display, and adapters around simulation records. Views are associated with entities through semantic entity IDs; node paths, meshes, animation pivots, collision shapes, and camera state are not simulation identity. See [`PlayerCharacter.cs`](../game/scripts/PlayerCharacter.cs), [`EntityView.cs`](../game/scripts/EntityView.cs), and [`Main.cs`](../game/scripts/Main.cs). |

### Composition: what “components” means today

The simulation does **not** implement a formal ECS registry or a generic attach/detach component API. Composition is ordinary C# object ownership: an `EntityRecord` exposes focused state objects (`NeedsState`, `CharacterHealth`, `SkillSet`, `ActionLifecycle`) and entity data; species definitions provide optional capabilities that reusable services check. Godot presentation is composed separately from nodes and scenes. Keep additions inside these patterns unless profiling or a concrete feature demonstrates a limitation and a reviewed decision records the migration and compatibility cost.

## Mutation and identity contracts

- Entity IDs are allocated by `World`, use a stable string representation, and the last allocated numeric ID is captured so removed IDs are not reused after loading. Snapshot validation rejects duplicate or malformed IDs and unresolved references. See `World.SpawnEntity`, `WorldSnapshotSerializer.Validate`, and the snapshot tests in [`WorldSnapshotTests.cs`](../tests/Simulation.Tests/WorldSnapshotTests.cs).
- `EntityRecord` exposes inventory as a read-only view; carried-item changes go through its internal checked operations and higher-level resource services. See [`ResourceTransferService.cs`](../src/Simulation/ResourceTransferService.cs) for validated transfers, amounts, known item IDs, capacity, and atomic failure behavior.
- Needs, action progress, navigation, and relationships have their own state contracts. `ActionLifecycle` arbitrates priorities and cleanup; services own completion/cancellation of the work they start. `World.Reset` clears durable world state and cancels actions/reservations.
- Several policies still use the literal species ID `human` for player identity, squad membership, and daily schedules. These are current restrictions, not a general humanoid role contract. The current checks are in [`World.cs`](../src/Simulation/World.cs) and snapshot validation in [`WorldSnapshot.cs`](../src/Simulation/WorldSnapshot.cs). Broader humanoid support needs an explicit validated role/capability field and a separate compatibility change.
- Simulation anatomy is not yet species-selected: `EntityRecord` calls `CharacterHealth.CreateHumanoid` for all species. The active health-region names remain `Head`, `Torso`, `LeftArm`, `RightArm`, `LeftLeg`, and `RightLeg`; detailed semantic regions are a future migration target. See [`CharacterHealth.cs`](../src/Simulation/CharacterHealth.cs) and [`HUMAN_ANATOMY_AND_BREEDING_REFERENCE.md`](HUMAN_ANATOMY_AND_BREEDING_REFERENCE.md).

## Events and notifications

There are three distinct mechanisms; do not treat them as one event bus:

1. `World.RelationshipChanged` and `World.FactionStandingChanged` are synchronous .NET notifications raised after successful durable mutations. `Main` consumes relationship notifications to refresh presentation. They are not queued, replayed, or saved. See [`World.cs`](../src/Simulation/World.cs) and [`Main.cs`](../game/scripts/Main.cs).
2. `RelationshipEventCatalog` loads validated, lowercase-ID adjustment profiles from [`data/relationship_events.json`](../data/relationship_events.json). `RelationshipEventService` applies a selected profile to a directed relationship through `World`. The catalog is data; it does not autonomously schedule events. See [`RelationshipEventCatalog.cs`](../src/Simulation/RelationshipEventCatalog.cs).
3. `WorldEventQueue` stores generic string `Type`/`Payload` events and can process due entries in deterministic tick/ID order. Repository search confirms it is referenced only by its own unit tests, not production simulation or presentation code. It is currently an unused standalone abstraction, not a persistence contract and not the integration path for domain notifications. No work should expand it into a global bus without a concrete consumer and decision.

## Save/load contract

[`WorldSnapshot.cs`](../src/Simulation/WorldSnapshot.cs) defines the JSON snapshot DTOs and serializer. The current schema is **17**. Loading first deserializes, migrates supported schemas 4–16, validates the complete resulting snapshot and catalog references, then applies it; invalid or unsupported input must leave the existing world unchanged. `SimulationSaveBoundary` additionally requires the clock to be paused and resets registered transient state only after successful load. [`SimulationSaveBoundaryTests.cs`](../tests/Simulation.Tests/SimulationSaveBoundaryTests.cs) and [`WorldSnapshotTests.cs`](../tests/Simulation.Tests/WorldSnapshotTests.cs) cover these boundaries.

Current durable snapshot data includes:

- schema version, simulation time, last allocated entity ID, optional session seed/player ID/player profile, and player facing;
- entities: stable ID, species ID, position, satiety, carried inventory, captivity state, allegiance, parent IDs, sleep/bed state, and equipped tool;
- food sources, resource piles, stockpiles and ownership; relationships; factions, reputation and directed hostility;
- squad membership, work schedules and home positions; wild territory centers/radii; workstations/recipes/progress; farms/plots; structures/beds; construction blueprints/material and work progress; tree regrowth timers; fireplace fuel;
- player-only health-region values, pending death time, and dead state.

Catalog definitions (species, recipes/content definitions) are supplied at load and are not duplicated as per-entity definition data. Save identifiers are semantic string IDs, not node paths or imported bone indices. Snapshot ordering is normalized for deterministic serialization.

Transient or reconstructible state is intentionally not in the snapshot: active `ActionLifecycle` state, navigation requests/paths, reservations, service-private trip/selection/retry state, and camera/presentation state. Services implementing `ITransientSimulationState` receive `Reset()` after a successful load; current implementations include food selection/consumption, hauling, wandering, squad/order, and caravan visit services. After load, jobs and paths must be re-evaluated; this is durable-state continuity, not bit-for-bit activity replay. See [`SimulationSaveBoundary.cs`](../src/Simulation/SimulationSaveBoundary.cs) and [`VALIDATION_1_0.md`](VALIDATION_1_0.md).

### Known persistence gaps

- NPC/creature health is not saved; only the player's health is represented in schema 17.
- Skills are runtime-owned by each entity but are not included in `EntitySnapshot`.
- Any new durable state requires an explicit DTO field, validation, migration/rejection policy, and round-trip/failure tests. Do not silently rely on service or Godot-node state to restore it.

## Verified architecture risks and bounded follow-ups

These are evidence-based candidate tasks, not directions to make broad changes in this documentation PR.

| Finding | Evidence in current code | Bounded follow-up |
| --- | --- | --- |
| Two wander services are invoked for the same eligible non-hauling entity. | [`Main.AdvanceWandering`](../game/scripts/Main.cs) calls [`WanderingService.Advance`](../src/Simulation/WanderingService.cs) and then [`WanderService.Advance`](../src/Simulation/WanderService.cs); the first may start/advance the entity's `Wander` action, while the second rejects when an action is active. Both have separate unit tests. | Confirm intended live behavior and differences, then select one production path or explicitly sequence them. Preserve hunger preemption, deterministic goals, cancellation, and load reset; add/update focused integration coverage. |
| Large orchestration/serialization files concentrate change risk. | Current line counts: [`World.cs`](../src/Simulation/World.cs) 1,090; [`WorldSnapshot.cs`](../src/Simulation/WorldSnapshot.cs) 963; [`Main.cs`](../game/scripts/Main.cs) 1,304; [`EntityView.cs`](../game/scripts/EntityView.cs) 500. `Main` has already been split into partial files, but still owns substantial startup/update wiring. | Extract only a bounded responsibility when changing it; keep public behavior and schema stable, add focused tests, and avoid a drive-by rewrite. Consider a separately scoped migration extraction for snapshot code only if it can preserve validation order and all existing migration tests. |
| Capability data is extensible, but item identity is still a code allowlist and some roles are species-ID checks. | [`SpeciesCatalog`](../src/Simulation/SpeciesCatalog.cs) validates JSON capability values; [`ItemIds.IsKnown`](../src/Simulation/ItemIds.cs) is used to validate inventory/diet contents. `World` compares `SpeciesId` to `human` for player/squad/schedule eligibility. | Track separate implementation issues for item catalog ownership and humanoid/NPC role classification. Do not broaden IDs or permissions without compatibility tests and, where saved data changes, a migration plan. |
| Generic queue suggests a production event path it does not have. | `WorldEventQueue` and `WorldEvent` are used only by `WorldEventQueueTests`; production notifications use direct typed World events. | Either remove the unused abstraction in a cleanup issue or give it a specific typed consumer and tested lifecycle. Do not persist or globalize it by implication. |
| Simulation anatomy is shared as humanoid regardless of species. | `EntityRecord` constructs `CharacterHealth.CreateHumanoid`; species data does not choose an anatomy profile. | Before expanding creature injuries/combat, add a species anatomy contract and a versioned migration with round-trip and invalid-save tests. Keep semantic region IDs lowercase per D042; maintain current schema names until that migration is implemented. |

## Runnable validation records

The project already has recorded vertical slices; they are evidence of specific tested behavior, not proof that every system is integrated:

- [0.1 Ant Farm validation](VALIDATION_0_1.md)
- [0.4 settlement/construction validation](VALIDATION_0_4.md)
- [1.0 First Real Game end-to-end validation](VALIDATION_1_0.md)
- [1.1 schedule validation](VALIDATION_1.1.md)

For issue readiness, coding-agent handoff, test selection, save changes, and architectural review, see [Implementation Guidelines](IMPLEMENTATION_GUIDELINES.md). The solution currently builds the simulation, simulation test project, and Godot game; use its actual project list in [`UnnamedProject2026.slnx`](../UnnamedProject2026.slnx) when reporting validation.
