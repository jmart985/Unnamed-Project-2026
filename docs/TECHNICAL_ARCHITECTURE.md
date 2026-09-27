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

These are planned paths. F01 creates the runnable scaffold. Use a fixed, flat 3D map with placeholder meshes and an overhead camera. This prototype presentation does not settle the final visual direction.

## Ownership and update model

- A World owns entity records identified by stable IDs, resource containers, and simulation time. Use simple composition, not a custom ECS framework.
- Entity state contains ID, species ID, position, satiety, inventory, and current activity. Views reference IDs; scene-node paths are not save identities.
- A single-threaded fixed simulation tick updates hunger, chooses actions, and processes outcomes in a documented stable order. Godot adapters perform navigation and report arrival/failure back to the simulation.
- The simulation clock advances in 0.1-second ticks. Each tick invokes domain update callbacks in stable order, then the engine refreshes presentation. It supports 1x and 4x speed, ignores elapsed wall time while paused, and caps one frame at eight ticks; excess accumulated time is discarded rather than causing an unbounded catch-up.
- Simulation rules do not depend on rendering or UI. A small navigation boundary permits deterministic fake-path tests; live navigation is validated in Godot.
- Use a simple action state machine: Idle, Moving, Eating, Hauling, Waiting. Hunger preempts work. Avoid behavior-tree or utility-AI frameworks until necessary.
- Inventory transfers and reservations go through one domain service. Counts stay nonnegative, capacity is enforced, and a transfer either fully succeeds for its agreed quantity or changes neither container.
- Item/species definitions use small validated JSON files with stable string IDs. Start with food, wood, human, and one creature species. No mod loader or schema framework.

## Navigation

Use Godot navigation on a static map, with explicit arrival tolerance, failure/timeout handling, and throttled replanning. Domain decisions select goals; the engine adapter moves a view and feeds authoritative movement results back to entity position. Do not maintain independent UI and domain inventories or hunger values.

## Save/load boundary

Use a versioned JSON snapshot of durable state: schema version, simulation time, entity IDs/species/positions/satiety/inventories, and world resource containers. Validate into a temporary world before replacing the current world. Unsupported versions or invalid references produce an error without changing the running world.

Save at a tick boundary while simulation is paused. Persist carried items. Jobs, reservations, paths, and current movement are transient: release/rebuild them after load and re-evaluate actions, depositing carried wood first. The first version promises durable-state continuity, not bit-for-bit replay of paths or random wandering. Write through a temporary file and preserve the previous valid save on failure.

## Tests and observability

Unit tests cover tick-driven hunger, entity identity, atomic inventory operations, reservation cleanup, prioritization, and save round trips. Integration scenarios cover actual Godot movement and the complete eating/hauling loop. An inspector shows ID, species, satiety, action, target, inventory, and waiting reason. Record reproducible scenario inputs and observed output in the issue/PR.

## Revisit criteria

After 0.1, reassess engine, presentation, and architecture using prototype results. Consider changes if measured simulation or navigation cost, C# tooling friction, or concrete rendering/world-streaming requirements cannot be addressed simply. Do not build world streaming, distributed simulation, multiplayer, or production-scale ECS in 0.1.
