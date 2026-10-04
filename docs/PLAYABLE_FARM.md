# Playable town farm

Milestone 0.4 issue #403 turns the existing farming foundation into one usable, durable starter plot at `(5, 0, 5)`. The plot is part of the authoritative `World`; the Godot soil, crop meshes, label, target entry and action panel are projections of that state.

## Player loop

1. Collect at least one plant with the Collect control (default `E`).
2. Use the Target control (default `T`) until `Farm · farm-1` is selected.
3. Stand within 2 m and press Interact (default `F`).
4. Plant one carried plant, apply five growth steps, then harvest three food.

The world label and target HUD distinguish Empty, Growing and Harvest Ready. The panel reports every rejection. Planting, tending and harvesting reject missing/stale farms or plots, distance greater than 2 m, incapacitation, occupied/empty/premature states, missing seed and insufficient carry capacity without partially changing inventory or the plot.

## Shared actor actions

`ActorFarmService` is the single action boundary for player input and autonomous workers. It accepts an actor ID and checks the live entity, consciousness, range and the species `canUseTools` capability before changing the farm. Humans and tool-capable creatures such as apes can therefore plant, tend and harvest through the same commands. Incapable creatures fail without consuming items. `PlayerFarmService` is a thin adapter that supplies the current player ID; future schedule/job selection should call the actor service rather than reproduce its rules.

This slice does not assign farming jobs automatically. Work schedules, priorities and path execution can invoke these complete actions once their own focused issue defines when an NPC chooses them. Seasons, irrigation, crop catalogs, passive growth and worker queues remain later work.

## Persistence and compatibility

Snapshot schema 12 stores farm identity, position, plot capacity, crop ID and growth. Schemas 4-11 migrate session saves to the fixed empty starter farm; non-session fixture Worlds remain without a fabricated farm. Current snapshots reject duplicate IDs, invalid positions, missing plots, unknown crop IDs and growth outside the crop definition. Save/load replaces the World only after full validation.

## Validation

Build the Debug game, then run:

```powershell
& '<Godot .NET executable>' --path game --headless -- --validate-farm
& '<Godot .NET executable>' --path game -- --validate-farm
```

Require process exit 0 and `FARM VALIDATION PASSED`. The scenario targets the rendered plot, opens it through the gameplay interaction, completes an exact plant/grow/harvest cycle, checks repeated reset, saves and restores partial growth, and captures `docs/evidence/farm/production-cycle.png` in rendered mode. Domain tests additionally prove an NPC and a tool-capable creature can share the actions and an incapable creature is rejected atomically.
