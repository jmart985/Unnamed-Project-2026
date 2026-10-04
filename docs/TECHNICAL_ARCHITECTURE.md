# Technical architecture

**Status: provisional, 2026-09-27.** Use Godot 4 .NET with C# for the first prototype. F01 pins an exact compatible Godot and .NET SDK pair and documents verified setup commands. This document makes no claim about the latest release.

## Small initial structure

```text
game/                 Godot project, scenes, C# engine adapters and UI
src/Simulation/       Plain C# domain state and simulation rules
tests/Simulation.Tests/  Domain tests
data/                 Species and item definitions
docs/                 Design, decisions, milestone and work sequence
```

These are planned paths. F01 creates the runnable scaffold. Use a fixed, flat 3D map (400 x 400 m: central town plus rural perimeter) with placeholder meshes and an overhead camera. This prototype presentation does not settle the final visual direction.

## Ownership and update model

- A World owns entity records identified by stable IDs, resource containers, and simulation time. It owns food sources, haul piles, stockpiles, and new-session metadata (seed, player entity ID, and player profile); entity inventories are bounded resource containers. The Godot PlayerCharacter is the view/controller for the World-owned player record. Use simple composition, not a custom ECS framework.
- For 1.0, the World owns one player-led squad's durable membership as entity IDs. An entity may belong to at most one squad; membership references must resolve to live human NPCs. Removing an entity removes its membership. Squad orders and navigation are transient and are re-evaluated after load; membership remains durable.
- Entity IDs are allocated monotonically within a World and are not reused after entity removal or world reset. Reset clears records but preserves the allocator, so stale IDs cannot resolve to new entities; a newly created World starts a new ID sequence.
- Entity state contains ID, species ID, position, satiety, inventory, and current activity. Views reference IDs; scene-node paths are not save identities.
- Species-aware spawning derives character max health, movement speed, strength, and endurance from the validated species definition; other character stats use the World's configured defaults. Spawning without a species definition retains those defaults. Snapshot restoration re-derives the same stats from the supplied species catalog rather than serializing duplicate definition data.
- Directed relationship state is keyed by distinct observer and target entity IDs; self-relationships are invalid. Familiarity, trust, fear, and loyalty accept explicit finite deltas and remain bounded to [0, 100]. New records start at zero except domestic species defaults toward humans/non-predators; existing or saved records are never reinitialized. Social adjustments validate all deltas before applying them atomically. Opposing directions are independent; no implicit decay or autonomous social behavior is introduced by the relationship data type.
- Faction reputation is keyed independently by live entity and known faction IDs, starts at zero, and accepts explicit finite deltas only when the result remains within [0, 100]. Faction hostility is a separate directed boolean stance between distinct known factions; neutral or hostile state in one direction does not imply the reverse.
- Explicit relationship events are loaded from validated JSON profiles and update only the directed observer-to-target relationship. Prototype profiles are provisional tuning data; they do not cause events to be selected autonomously.
- The 1.0 player greeting is one explicit, close-range interaction gated by a species capability. A successful greeting applies the validated `greeted` event profile only to the directed creature-to-player relationship; unsupported or invalid requests do not create or mutate relationship state.
- Social melee combat composes damage resolution with the required `harmed` relationship profile. Combat routes damage through character health, rejects incapacitated attackers and dead or already-destroyed targets, and applies the social event only when health actually decreases. Rejected or zero-applied-damage attacks leave relationship state unchanged; the reverse direction is independent.
- A read-only bond classifier derives Stranger, Familiar, Trusted, Loyal, or Fearful from relationship values and a validated threshold profile. Initial thresholds are provisional and do not mutate the underlying relationship.
- Voluntary recruitment readiness is evaluated from the prospective recruit's directed relationship toward the recruiter. Only the Loyal bond level is eligible in the prototype policy; evaluation does not create or mutate relationship state.
- The entity inspector displays each selected entity's outgoing relationships in stable target-ID order, with qualitative bond levels in gameplay. Numeric values remain in the immutable diagnostic read model and saved World state; inspection is a read-only projection.
- World owns directed relationship records for live entity pairs. Removing either participant or resetting the World removes the associated records; valid records persist through versioned world snapshots.
- Creature Core extends the existing species catalog with a validated temperament profile containing boldness, aggression, fear, curiosity, sociability, and territoriality, each finite and bounded to [0, 1]. Existing deterministic behavior uses boldness, curiosity, and sociability; behavior driven by aggression, fear, and territoriality remains deferred. Creature lifecycle state belongs to the simulation domain and remains attached to the authoritative entity: wild creatures can be captured, made captive, domesticated, or released back to wild. Death remains authoritative in `CharacterHealth`; dead entities cannot transition, and health/death persistence remains outside the snapshot contract.
- Read-only perception queries use the observer species' smell range and diet to return available nearby food, other nearby entities, and supplied track observations in stable distance/ID order. Track queries require the species' tracking capability; track generation, aging, persistence, and behavior consumers remain separate.
- A single-threaded fixed simulation tick updates hunger, chooses actions, and processes outcomes in a documented stable order. Godot adapters perform navigation and report arrival/failure back to the simulation.
- World-owned food sources are selected and reserved by the domain. After navigation advances and synchronizes the authoritative position, food is consumed only for a reported arrival within tolerance; nutrition is applied once and needs are checked before lower-priority work can resume.
- Haul actions reserve available source wood and stockpile capacity before travel. They transfer to the entity inventory only after validated arrival, release the source claim after pickup, and keep the stockpile capacity claim until deposit or cancellation.
- The simulation clock advances in 0.1-second ticks. Each tick invokes domain update callbacks in stable order, then the engine refreshes presentation. It supports 1x and 4x speed, ignores elapsed wall time while paused, and caps one frame at eight ticks; excess accumulated time is discarded rather than causing an unbounded catch-up.
- Simulation rules do not depend on rendering or UI. A small navigation boundary permits deterministic fake-path tests; live navigation is validated in Godot.
- Use a simple action state machine: Idle, Moving, Eating, Hauling, Waiting. Hunger preempts work. Avoid behavior-tree or utility-AI frameworks until necessary.
- Inventory transfers go through one domain service. Counts stay nonnegative, capacity is enforced, and a transfer either fully succeeds for its agreed quantity or changes neither container. Entity inventory capacity is configurable per World (default 10 units). A World-owned reservation service claims resource quantities and destination capacity for active entity actions; completion, cancellation, entity removal, and target removal release those transient claims.
- The simulation clock advances in 0.1-second ticks. Each tick invokes domain update callbacks in stable order (needs, then navigation advancement, then action outcomes such as food consumption), then the engine refreshes presentation. It supports 1x and 4x speed, ignores elapsed wall time while paused, and caps one frame at eight ticks; excess accumulated time is discarded rather than causing an unbounded catch-up.
- Simulation rules do not depend on rendering or UI. A small navigation boundary permits deterministic fake-path tests; live navigation is validated in Godot.
- Use a simple action state machine: Idle, Moving, Eating, Hauling, Waiting. Hunger preempts work. Avoid behavior-tree or utility-AI frameworks until necessary.
- Inventory transfers go through one domain service. Counts stay nonnegative, capacity is enforced, and a transfer either fully succeeds for its agreed quantity or changes neither container. A World-owned reservation service claims resource quantities and destination capacity for active entity actions; completion, cancellation, entity removal, and target removal release those transient claims. The transfer service also moves items atomically between a resource container and an entity's carried inventory (pickup/deliver); carried inventory has no capacity limit until a job defines one per species.
- A shared, documented bounded-retry policy lets a waiting job (no reachable/available candidate) defer reselection for a fixed number of simulation ticks instead of reevaluating every tick. Action candidates can require an entity's carried inventory to be empty before starting, so hunger (or any preemption) that interrupts a loaded trip keeps the carried items and the entity must resume delivery before claiming new pickup-style work.
- Item/species definitions use small validated JSON files with stable string IDs. `data/species.json` defines each species' positive movement speed (world units per simulation second), nonnegative hunger rate (satiety per simulation second), known item IDs in its diet, and hauling capability. Capability profiles include body category, dietary profile, max health, strength, endurance, dexterity, smell range, tool use, door use, and tracking. Missing optional values default to `"unspecified"` for body category, an inferred profile for dietary profile, `100` for max health, `1` for strength/endurance/dexterity/smell range, and `false` for tool use/door use/tracking. Max health and the four capability stats must be finite and greater than zero. Start with food, wood, human, and one creature species. No mod loader or schema framework.

## Navigation

Use Godot navigation on a static map, with explicit arrival tolerance, failure/timeout handling, and throttled replanning. Domain decisions select goals; the engine adapter moves a view and feeds authoritative movement results back to entity position. Do not maintain independent UI and domain inventories or hunger values.

In the sandbox, select `entity-000001` and press `R` to route around the central obstacle, `U` to request an unreachable goal, `C` to cancel, or `D` to mark the active goal removed. Movement and navigation time advance only on simulation ticks, so pause freezes both.

## Save/load boundary

Use a versioned JSON snapshot of durable state: schema version, simulation time, entity IDs/species/positions/satiety/inventories, durable creature capture/captivity state, food/resource-pile/stockpile containers, directed relationship values, and optional new-session seed/player identity/profile. Validate the complete snapshot before replacing the current world. Unsupported versions or invalid references produce an error without changing the running world.

Save at a tick boundary while simulation is paused. Persist carried items. Jobs, reservations, paths, and current movement are transient: registered simulation services reset only after snapshot validation and re-evaluate actions on the next tick, depositing carried wood first. The first version promises durable-state continuity, not bit-for-bit replay of paths or random wandering. Write through a temporary file and preserve the previous valid save on failure.

Creature captivity is durable entity state in snapshot schema version 5; schema version 4 snapshots migrate with all entities restored as wild. Snapshot schema version 6 adds validated faction identities and entity allegiance; schema versions 4 and 5 migrate with entities unaligned and no factions. Schema version 7 adds validated entity-to-faction reputation and directed faction hostility; schema versions 4, 5, and 6 migrate with no reputation or hostility records. Schema version 8 adds optional faction ownership to settlement stockpiles; schema versions 4–7 migrate with stockpiles unowned. The 1.0 snapshot adds validated squad membership and increments the current schema version from 8 to 9. Squad orders and navigation requests remain transient.

## Tests and observability

Unit tests cover tick-driven hunger, entity identity, atomic inventory operations, reservation cleanup, prioritization, and save round trips. Integration scenarios cover actual Godot movement and the complete eating/hauling loop. An inspector shows ID, species, satiety, action, target, inventory, and waiting reason. Record reproducible scenario inputs and observed output in the issue/PR.

## Revisit criteria

After 0.1, reassess engine, presentation, and architecture using prototype results. Consider changes if measured simulation or navigation cost, C# tooling friction, or concrete rendering/world-streaming requirements cannot be addressed simply. Do not build world streaming, distributed simulation, multiplayer, or production-scale ECS in 0.1.

## Daily schedules (1.1)

World owns per-human-NPC work-hour overrides; phase derives from SimulationClock with a 240-second day beginning at 08:00. WoodHaulingService reconciles rest before navigation, cancels empty pickup trips through ActionLifecycle, and allows carried delivery. Food and squad priorities remain higher. Schema 10 adds validated WorkSchedules; schemas 4-9 use defaults. See docs/NPC_SCHEDULES.md for boundaries, precedence and validation.

Relationship and faction standing notifications carry actual applied deltas after authoritative changes. Gameplay displays transient signed changes; production social profiles and faction event steps use +1/-1. Petting and selection rings use the existing recipient Trusted/Loyal bond, with Fearful overriding permission. See [SOCIAL_FEEDBACK.md](SOCIAL_FEEDBACK.md) for domestic defaults, save compatibility and feedback validation.
