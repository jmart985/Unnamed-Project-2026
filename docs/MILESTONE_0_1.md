# Milestone 0.1: The Ant Farm

## Objective

Run a tiny autonomous world for ten minutes and explain what each entity is doing. The milestone closes only when the acceptance scenario passes and required issues are complete.

## Included

- One fixed rectangular map with static obstacles and reachable food/storage areas.
- 10 humans and 3 creatures of one species.
- Food and wood; one stockpile; finite initial supplies.
- Stable entity identity, spawning/removal, satiety, eating, human hauling, and creature wandering.
- Pause, 1x/4x speed, reset, entity inspection, and one manual save slot.
- Focused domain tests and a documented Godot integration check.

## Deferred

Combat, health/injury/death, factions, captivity, taming, breeding/genetics, relationships, sleep/beds, building placement, crafting, farming, economy/trade, procedural maps, world streaming, multiplayer, polished art, and mod support.

## Acceptance scenario

1. Start the documented scenario with 10 humans, 3 creatures, at least 100 food units, 100 wood units, and an empty stockpile of at least 100 wood capacity. Start satiety at 60; use 0.2 drain per simulation second and 50 nutrition per food unit.
2. Run 600 simulation seconds at 1x without player orders. Every entity successfully eats at least once. At least 50 wood units reach storage. No unhandled exceptions or permanently stuck reservations occur.
3. Count food consumed and verify initial food equals remaining food plus consumed food. Verify total wood across ground, inventory, and storage stays 100. No container has negative counts or exceeds capacity.
4. Pause for five wall-clock seconds: simulation time, satiety, inventories, and positions remain unchanged. At 4x, simulation time advances approximately four times the 1x rate within a documented tolerance.
5. Save while a human carries wood, reset, and load. IDs, species, positions, satiety, time, and resource quantities match the saved snapshot within documented numeric tolerance. Entities resume valid activity; carried wood is deposited before another haul pickup.
6. Separately test inaccessible food, depletion, full storage, target removal, and competition for the final food unit. Each has a visible waiting/recovery reason, no resource duplication, and bounded retry behavior.
7. Record engine/SDK versions, commands, scenario settings, results, and any known limits in docs/VALIDATION_0_1.md when implemented.

All figures are prototype acceptance targets, not claims about measured performance. Do not add production scope to make this scenario pass.
