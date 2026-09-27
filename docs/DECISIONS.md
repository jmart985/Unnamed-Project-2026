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
