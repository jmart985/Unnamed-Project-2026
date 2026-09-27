# Initial implementation backlog

All tasks belong to **0.1: The Ant Farm**. Stable planning keys below become GitHub issue references when published. Only F01 begins Ready. Each task is intended as one focused PR; split further if implementation reveals independent changes.

## Dependency order

F01 → F02 → E01 → {I01, N01, H01} → J01 → H02 → {J02, C01} → S01 → F03.

J01 requires I01 and H01; H02 also requires N01. Work can proceed independently only after its prerequisites pass.

## Tasks

### F01 — Foundation: Create the Godot C# sandbox and build checks

## Outcome

Create the Godot C# sandbox and build checks.

## Scope

Create the minimal Godot 4 .NET project, plain C# simulation library, and test project. Pin an exact compatible engine/SDK pair. Add a placeholder map, camera, and startup scene; no simulation features yet.

## Dependencies

None. This is the first Ready task.

## Acceptance criteria

- [ ] A clean checkout builds using documented commands and launches the sandbox scene in the pinned editor.
- [ ] A minimal domain smoke test runs locally and in CI; CI uses the pinned SDK.
- [ ] README records engine edition/version, SDK version, restore/build/test/run steps, and expected output.
- [ ] Generated engine/build files stay untracked.

## Validation

From a clean checkout, execute documented restore, build, and test commands; launch the scene and record tool versions. Verify CI passes.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### F02 — Foundation: Add a fixed simulation clock with pause and speed controls

## Outcome

Add a fixed simulation clock with pause and speed controls.

## Scope

Add a single-owner fixed tick with 1x and 4x speed, pause/resume, and scenario reset. Define tick interval and update order.

## Dependencies

F01

## Acceptance criteria

- [ ] Equal simulated durations produce the same number of domain ticks regardless of render frame chunking within the supported range.
- [ ] Pause advances neither simulation time nor domain state; resume continues without catch-up for paused time.
- [ ] 1x/4x and reset controls are visible; reset clears time and rebuilds the scenario.
- [ ] Tick interval and overflow/catch-up policy are documented.

## Validation

Test clock advancement with varied frame deltas, pause/resume, reset, and 1x/4x. Manually verify controls.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### E01 — Entity Simulation: Add stable entity IDs, spawn/remove, and state inspection

## Outcome

Add stable entity IDs, spawn/remove, and state inspection.

## Scope

Create a world-owned entity registry with species ID, position, satiety, inventory placeholder, and activity. Add placeholder views and selection inspector.

## Dependencies

F02

## Acceptance criteria

- [ ] Spawn ten humans with distinct stable IDs; removal and respawn never collide with a live ID.
- [ ] Views resolve records by ID and do not own duplicate domain state.
- [ ] Removing a selected entity clears selection safely; missing IDs return a controlled result.
- [ ] Inspector shows ID, species, position, satiety, activity, target, inventory, and waiting reason as fields become available.

## Validation

Test uniqueness, lookup, removal, and reset. Select/remove entities in the sandbox and confirm no stale view remains.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### I01 — Inventory/Resources: Implement bounded containers and atomic resource transfers

## Outcome

Implement bounded containers and atomic resource transfers.

## Scope

Define food and wood items, ground piles, entity containers, and one stockpile. Use simple unit capacities; no weight/equipment system.

## Dependencies

E01

## Acceptance criteria

- [ ] Counts are nonnegative and capacity is enforced for all mutations.
- [ ] A transfer of an agreed quantity either succeeds completely or changes neither container.
- [ ] Insufficient source, full destination, zero/negative quantity, and unknown item IDs have explicit outcomes.
- [ ] Total wood is conserved across successful and rejected transfers.

## Validation

Unit-test exact-capacity transfers, insufficient source, full destination, invalid amounts/IDs, and repeated moves conserving totals.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### N01 — Navigation: Move entities to targets and report arrival or failure

## Outcome

Move entities to targets and report arrival or failure.

## Scope

Connect a small domain navigation interface to Godot navigation on a static flat map. Add one obstacle and an unreachable test goal.

## Dependencies

E01

## Acceptance criteria

- [ ] An entity routes around the obstacle and reports arrival only within a documented tolerance.
- [ ] Unreachable, removed, and timed-out goals report controlled failure and stop the current movement.
- [ ] Movement and authoritative position stay synchronized; pause freezes movement.
- [ ] Requests are throttled and do not replan every frame; tests can use a fake navigation adapter.

## Validation

Run live Godot reachable/unreachable/cancel scenarios and a pause check. Test the domain adapter contract with a fake navigator.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### H01 — Needs/Hunger: Decrease satiety on simulation ticks and request food

## Outcome

Decrease satiety on simulation ticks and request food.

## Scope

Add satiety in [0,100], initial value 60, drain 0.2 per simulation second, hungry threshold <=30, and visible starving status at zero. Values are configurable.

## Dependencies

E01

## Acceptance criteria

- [ ] Satiety changes by simulation time and is clamped to [0,100].
- [ ] Crossing <=30 requests food; zero sets starving without death or health mechanics.
- [ ] Adding nutrition clamps at 100; invalid tuning data is rejected.
- [ ] Paused simulation produces no satiety change and inspector reflects current state.

## Validation

Test threshold boundaries, zero clamp, nutrition clamp, alternate tick sizes covering equal time, and pause.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### J01 — Jobs: Add action priorities, resource reservations, and cancellation

## Outcome

Add action priorities, resource reservations, and cancellation.

## Scope

Introduce a small action lifecycle and shared reservation service. Eating outranks hauling; wandering is fallback. Keep rules in domain code.

## Dependencies

I01, H01

## Acceptance criteria

- [ ] An entity has at most one active action; selection is stable for equivalent candidates.
- [ ] A resource unit or destination capacity cannot be reserved by two actions at once.
- [ ] Completion, cancellation, entity removal, and target removal release all associated reservations.
- [ ] Hunger can interrupt hauling without deleting carried items; carried wood is marked for deposit before a new pickup.
- [ ] Waiting uses a documented retry delay instead of spinning every tick.

## Validation

Unit-test priority/preemption, contention for one unit, cancellation/removal cleanup, retained cargo, and bounded retries.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### H02 — Needs/Hunger: Complete the autonomous find-food, move, and eat loop

## Outcome

Complete the autonomous find-food, move, and eat loop.

## Scope

Select compatible available food, reserve one unit, travel, consume at arrival, add 50 nutrition, and re-evaluate needs before returning to work.

## Dependencies

N01, H01, J01

## Acceptance criteria

- [ ] A hungry human reaches available food and consumes exactly one reserved unit per successful eat action.
- [ ] Food is never consumed remotely; depleted/removed/unreachable targets release reservations and allow another candidate.
- [ ] Two hungry entities competing for the final unit cannot both consume it.
- [ ] No available food yields an inspectable waiting reason and bounded retries; replenishment allows recovery.
- [ ] Food consumed counters reconcile with remaining food.

## Validation

Test success and contention with fake navigation. Run live Godot hunger loop, blocked food with an alternate source, no-food recovery, and target removal.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### J02 — Jobs: Haul wood to storage and resume delivery after eating

## Outcome

Haul wood to storage and resume delivery after eating.

## Scope

Humans claim wood, reserve source/destination, pick up within capacity, and deposit into the stockpile. Integrate hunger interruption.

## Dependencies

H02

## Acceptance criteria

- [ ] A human moves wood from ground to inventory to storage without creating or losing wood.
- [ ] Two haulers cannot claim the same source quantity or exceed reserved destination capacity.
- [ ] Hunger interrupts a loaded trip; wood remains carried and is delivered after eating before any new pickup.
- [ ] Full storage or removed targets cause safe waiting/recovery, with reservations released appropriately.
- [ ] No available haul work returns the entity to idle.

## Validation

Test total conservation, destination capacity contention, interruption before/after pickup, full storage, and target removal. Run two humans hauling while one becomes hungry.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### C01 — Creatures: Add one data-defined creature species that wanders and eats

## Outcome

Add one data-defined creature species that wanders and eats.

## Scope

Load validated human and creature species data with movement speed, hunger rate, diet, and canHaul. Spawn three creatures sharing the entity/needs/navigation systems.

## Dependencies

H02

## Acceptance criteria

- [ ] Three distinct creature IDs use the same need, reservation, navigation, and eating services as humans.
- [ ] The creature accepts food in its diet, wanders to reachable bounded goals when fed, and never takes a human haul job.
- [ ] Changing a species speed or hunger rate in data changes behavior without a species-specific code branch.
- [ ] Invalid species references, diet IDs, or nonpositive speed are rejected with useful messages.

## Validation

Test species validation and capability/diet filtering. Observe all three creatures eating and wandering on the map while humans retain hauling behavior.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### S01 — Save/Load: Save and restore durable world state with validation

## Outcome

Save and restore durable world state with validation.

## Scope

Add one manual JSON save slot with schema version, simulation time, entity IDs/species/positions/satiety/inventories, and resource containers. Save at a paused tick boundary; rebuild transient actions after load.

## Dependencies

J02, C01

## Acceptance criteria

- [ ] A round trip restores all durable fields within documented numeric tolerance, including carried wood and stable IDs.
- [ ] After load, reservations/paths are rebuilt and entities resume valid work; carried wood is deposited before a new pickup.
- [ ] Malformed, unsupported-version, duplicate-ID, invalid-species, or negative-count snapshots leave the current world unchanged and display an error.
- [ ] A failed write preserves the previous valid save; no engine node references are serialized.

## Validation

Round-trip tests include mixed species and a loaded hauler; test corrupt/unsupported data and failed-write behavior. Manually save, reset, load, and observe resumed eating/hauling.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.

### F03 — Foundation: Verify and document the ten-minute Ant Farm acceptance scenario

## Outcome

Verify and document the ten-minute Ant Farm acceptance scenario.

## Scope

Assemble the exact scenario in docs/MILESTONE_0_1.md and record results. Fix only integration defects required by the milestone; split any substantial fixes into linked issues.

## Dependencies

S01

## Acceptance criteria

- [ ] The 600-second unattended scenario satisfies all milestone population, eating, hauling, and resource-accounting checks.
- [ ] Pause/speed and mid-haul save/load checks pass.
- [ ] Depletion, blocked food, full storage, removed targets, and final-unit contention produce safe, visible recovery.
- [ ] docs/VALIDATION_0_1.md records pinned versions, reproducible steps, actual outcomes, and limitations.
- [ ] All prerequisite issues meet their acceptance criteria; no deferred features were required.

## Validation

Run the documented milestone scenario plus focused failure scenarios. Preserve a concise validation report with counters and observed outcomes; record any failed criteria instead of marking complete.

## Design references

- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Gameplay loop](GAMEPLAY_LOOP.md)
- [Milestone 0.1](MILESTONE_0_1.md)

No systems outside Milestone 0.1 are included.
