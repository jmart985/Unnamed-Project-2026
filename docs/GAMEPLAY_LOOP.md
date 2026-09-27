# Gameplay loop

## Ant Farm loop

1. Start a fixed test map with 10 humans, 3 creatures of one species, food piles, wood piles, and a stockpile.
2. Advance simulation time. Satiety decreases toward 0; the scenario starts at 60 and the maximum is 100.
3. At satiety <= 30, request food. Eating takes priority over hauling or wandering.
4. Reserve one compatible food unit, choose an accessible source, and move there.
5. At arrival, atomically consume that unit and add 50 satiety, clamped to 100.
6. Release the reservation and re-evaluate needs. If still hungry, repeat; otherwise humans seek hauling work and creatures wander.

Rates and thresholds are prototype tuning data. Initial satiety drain is 0.2 per simulation second. Zero satiety records a visible starving state; death and health are deferred.
The sandbox status reports food units remaining across all sources and food units consumed; successful eating decreases the former and increments the latter once.

## Hauling

A human claims a wood transfer from a ground pile to the stockpile, reserves available quantity and destination space, walks to the source, picks up within capacity, walks to storage, and deposits. Carried wood stays in inventory if hunger interrupts the trip; after eating, deposit carried wood before claiming more. Cancellation releases claims and never deletes carried items.

## Failures and recovery

- Empty or incompatible food: remain hungry, show the reason, and retry after 10 simulation ticks (1.0 simulated second).
- Unreachable source: release its reservation and try another candidate. Do not retry every frame.
- Target removed or source depleted: cancel safely and re-evaluate.
- Full stockpile: retain carried wood, show waiting status, and retry later.
- Two entities want the last unit: exactly one can reserve and consume it.

## Player controls for 0.1

Start/reset scenario, pause/resume, run at 1x or 4x, select an entity to inspect state, save, and load. Supply is finite; automatic production is deferred. The acceptance scenario begins with enough food for ten minutes.
