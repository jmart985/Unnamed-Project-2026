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

## D009 — Bounded item containers

- Date: 2026-09-27
- Status: Provisional; implemented for I01a
- Decision: Use one `ItemContainer` implementation backed by a validated JSON item catalog and a unit-count capacity. Entity inventories, ground piles, and the stockpile share its mutation rules; the initial catalog defines food and wood by stable IDs.
- Reason: Keep item-specific behavior out of storage mechanics and enforce nonnegative counts and capacity at every mutation boundary.
- Consequence: Container capacity is configured in units, applies across all contained item IDs, and does not model weight. Transfers remain a separate atomic operation.
- Revisit: If item definitions require additional fields or container capacity semantics become item-specific.
