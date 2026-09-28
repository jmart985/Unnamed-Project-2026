# Decisions log

Record date, status, decision, reason, consequences, and revisit trigger. Supersede old entries explicitly so future coding sessions can follow the reasoning.

## D001 — Godot 4 .NET + C#

- Date: 2026-09-27
- Status: Provisional; requested initial direction
- Decision: Start with Godot 4 .NET and C#. Pin exact versions in F01 after validating setup.
- Reason: The initial risk is integrating a small simulation; typed domain code and a lightweight scene workflow suit that experiment.
- Consequence: Separate simulation rules from engine adapters where it is useful for tests. Avoid speculative portability layers.
- Revisit: At the end of 0.1, or earlier for demonstrated tooling or engine blockers; later graphical or world-streaming demands may justify another engine.

## D002 — Ant Farm scope

- Date: 2026-09-27
- Status: Initial scope baseline
- Decision: One fixed map, 10 humans, 3 individuals of one creature species, two resource types, eating, hauling, wandering, inspection, and save/load.
- Reason: Prove one complete autonomous loop before adding content.
- Consequence: Earlier brainstorming about combat, factions, relationships, and captivity is deferred. No deadline is asserted.
- Revisit: After the milestone acceptance scenario passes.

## D003 — Composition and a single simulation owner

- Date: 2026-09-27
- Status: Provisional implementation guidance
- Decision: Plain C# domain records/services, stable entity IDs, a fixed tick, a small action state machine, and engine adapters.
- Reason: Keep ownership explicit and changes understandable for AI-assisted coding.
- Consequence: No custom ECS, plugin system, or inheritance tree for every species.
- Revisit: Only when a concrete extension or measured bottleneck makes this inadequate.

## D004 — Durable snapshots; transient jobs restart

- Date: 2026-09-27
- Status: Provisional implementation guidance
- Decision: Save durable world state and carried items; rebuild jobs, reservations, and paths after loading.
- Reason: Prevent dangling runtime references and constrain save complexity.
- Consequence: Activity can restart after load; resource counts and need values must remain correct.
- Revisit: If future player commands or scheduling require exact job continuity.

## D005 — Prototype presentation

- Date: 2026-09-27
- Status: Provisional planning assumption
- Decision: Flat 3D map, overhead camera, static obstacles, placeholder meshes, desktop-first validation.
- Reason: Test spatial movement with minimal art and map work.
- Consequence: Final art style, platforms, and world scale remain undecided.
- Revisit: After observing the first complete loop.

## D006 — F01 toolchain pin

- Date: 2026-09-27
- Status: Provisional; validated for the scaffold
- Decision: Pin Godot 4.7.2 .NET (Mono), build `4.7.2.stable.mono.official.ed1daf0bf`, with .NET SDK 10.0.401.
- Reason: This is the installed compatible editor/SDK pair used to restore, build, test, and launch the initial project.
- Consequence: Local setup and future CI must use these versions unless a later issue records a deliberate upgrade.
- Revisit: If Godot or .NET compatibility blocks the prototype or a later F01 task.

## D007 — Fixed simulation clock policy

- Date: 2026-09-27
- Status: Provisional; implemented for F02
- Decision: Advance domain time in 0.1-second ticks at 1x or 4x speed. Pause discards elapsed wall time, and each frame processes at most eight ticks; overflow is discarded.
- Reason: Fixed ticks make simulation behavior independent of render frame chunking while the cap prevents long-frame catch-up spirals.
- Consequence: Frame deltas up to 0.8 simulated seconds are fully processed; larger deltas intentionally lose excess elapsed time.
- Revisit: If measured frame stalls or later simulation requirements need a different overflow policy.

## D008 — Entity ID lifetime

- Date: 2026-09-27
- Status: Provisional; implemented for E01a
- Decision: Allocate readable entity IDs monotonically within each World. Removing entities or resetting a World does not rewind its ID sequence; a newly created World starts a new sequence.
- Reason: A stale reference from before removal or reset must not resolve to a different entity in the same World.
- Consequence: Repeated scenario resets in one World continue increasing IDs.
- Revisit: If save/load or world replacement requires IDs to be restored from durable data.

## D009 — Entity action lifecycle

- Date: 2026-09-27
- Status: Provisional; implemented for J01a
- Decision: Give each entity one domain-owned active action, selected by fixed priority and stable candidate ID. Completion, cancellation, and preemption run the active action cleanup exactly once.
- Reason: Hunger must be able to preempt lower-priority work without duplicating action state or dropping carried inventory.
- Consequence: Action completion, cancellation, and preemption provide a lifecycle hook for transient reservation cleanup. Bounded retry timing remains a separate concern for follow-up job issues.
- Revisit: If action selection needs measured scheduling or parallel work beyond the Milestone 0.1 scope.

## D010 — Action-scoped resource reservations

- Date: 2026-09-27
- Status: Provisional; implemented for J01b
- Decision: A World-owned reservation service tracks resource quantities and destination capacity by active entity action and stable target ID. Action completion/cancellation, entity removal, and target removal release transient reservations.
- Reason: Competing actions must not claim the same resource units or storage capacity, and cleanup must not leave stale claims after lifecycle changes.
- Consequence: Reservations do not mutate durable inventories; transfer operations remain atomic and separate. Removed target IDs cannot be reused in that World.
- Revisit: If later job systems require partial reservation transfer or target recreation semantics.

## D012 — Arrival-gated food consumption

- Date: 2026-09-27
- Status: Provisional; implemented for H02b
- Decision: A domain food-consumption service composes food selection, action cleanup, and the navigation adapter. It removes one reserved food unit only after navigation reports arrival and the authoritative entity position is within the request tolerance.
- Reason: Keep food and satiety authoritative in the simulation while preventing remote, repeated, or failed-navigation consumption.
- Consequence: World owns food-source lifetimes; cancelling an eat action cancels its navigation request and releases its reservation. The Godot tick synchronizes movement before processing food outcomes.
- Revisit: When eating duration, partial nutrition, or persisted in-progress actions become part of a later issue.

## D013 — Bounded entity inventory

- Date: 2026-09-27
- Status: Provisional; implemented for J02a
- Decision: Store entity inventory in a World-created `ResourceContainer` with a configurable World capacity and a default of 10 item units.
- Reason: Hauling needs the same atomic quantity and capacity checks as ground sources and stockpiles.
- Consequence: All entities use the same initial capacity; inventory counts remain read-only to callers and transfers use the domain resource-transfer service.
- Revisit: If species-specific carrying capacity is required by a later milestone.
## D013 — Generic cargo retention and bounded action retries

- Date: 2026-09-27
- Status: Provisional; implemented for J01c
- Decision: Extract the bounded-retry delay used by food selection into a reusable `RetryScheduler` (10-tick default, matching D011) that any waiting job keys per entity. Give `EntityRecord` atomic, uncapped carried-inventory mutation, and extend the single inventory-transfer domain service (`ResourceTransferService`) with `TryPickUp`/`TryDeliver` overloads that atomically move items between a `ResourceContainer` and an entity's carried inventory. Give `ActionCandidate` an opt-in `RequiresEmptyCargo` flag so `ActionLifecycle.TryStart` returns `ActionStartResult.CargoPending` instead of starting new pickup-style work while the owning entity still carries items.
- Reason: Action completion, cancellation, and preemption already never touch `EntityRecord.Inventory`, but nothing previously exercised that guarantee with real carried items, and no job had a documented, reusable way to require delivery before a new pickup or to retry without reselecting every tick. These primitives let J02 (hauling) implement "resume delivery before a new pickup" and bounded waiting without inventing job-specific mechanisms.
- Consequence: Carried inventory has no capacity limit yet; bounded haul capacity per species is deferred to the hauling job that defines it. Only whole-entity removal discards carried inventory; cancellation, preemption, and reservation release never do.
- Revisit: When the hauling job (J02) defines per-species haul capacity, or if other job kinds need a different cargo-completion signal than "any carried item."
## D014 - Versioned durable world snapshots

- Date: 2026-09-27
- Status: Provisional; implemented for S01a
- Decision: Capture simulation time, the entity ID allocator position, entity durable state, carried inventory, and food, resource-pile, and stockpile containers in a versioned JSON snapshot. Runtime actions, reservations, navigation requests, and engine nodes remain transient. Snapshot schema version 2 restores every durable resource location, then resets registered transient services before replacing the world so their next tick rebuilds valid work.
- Reason: Save/load needs a stable, engine-independent boundary that can be validated before a later restore operation replaces the running world.
- Consequence: Snapshot schema changes require an explicit version update; transient work is rebuilt rather than serialized.
- Revisit: When S01b adds atomic validation and world restoration.
