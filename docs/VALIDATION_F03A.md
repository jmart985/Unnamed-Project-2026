# F03a: Ten-minute Ant Farm acceptance scenario

Date: 2026-09-29
Branch: issue-37-ant-farm-acceptance

The automated acceptance scenario advances 6,000 fixed 0.1-second ticks (600 simulated seconds) with 10 humans, 3 creatures, 100 food, 100 ground wood, and an empty 100-capacity stockpile. No player orders are issued.

Results:

- All 13 entities eat at least once; 26 meals consume 26 food, leaving 74. The food invariant is 100 = 74 + 26.
- Humans deliver all 100 wood to storage, exceeding the 50-unit target. Ground + carried + stored wood remains 100 on every tick.
- Resource containers and inventories stay within nonnegative capacity bounds on every tick.
- A mid-haul snapshot is loaded after transient eating, hauling, and wandering state is reset. Durable JSON state, IDs, species, positions, satiety, time, inventories, and resource counts match exactly. Carried wood is then delivered before another pickup.
- Pause and 1x/4x clock behavior are checked separately.

The scenario uses deterministic immediate-arrival navigation so CI can complete the ten-minute simulation quickly. It verifies the same domain services and update order as the sandbox. Live Godot rendered navigation timing is outside this automated run; the local Godot executable was unavailable in the validation environment.
