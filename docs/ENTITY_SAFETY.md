# Entity movement safety and unstuck behavior

Entities must not be able to permanently trap the player or deadlock one another through crowding. This is a shared simulation requirement for humans, creatures, and future squads.

## Required behavior

- Preserve a minimum navigable clearance around the player and other active entities where the navigation rules permit it. Dense groups may intentionally become difficult to push through, but they must not become an unbounded permanent enclosure.
- Detect lack of meaningful progress over a bounded number of movement ticks, including repeated blocked paths, collision overlap, and a crowd with no free exit.
- Stop or replan the blocked entity before issuing more movement toward the same blocked target.
- Give the player a recoverable escape response: cancel the current move/follow order, allow manual movement, and request a route to the nearest valid free position.
- For non-player entities, back off, choose a nearby free position, or temporarily yield the destination before retrying.
- Treat a dense mob as a crowd state: movement through it is slow and resistant, while its members continue to resolve local separation and seek an exit when the player is enclosed.
- Never resolve crowding by moving an entity through solid world geometry or teleporting it across the map.
- Keep the authoritative World position synchronized before updating the visual node, and preserve valid save/load positions.

## Acceptance slice

The first implementation should cover:

1. one NPC following the player into a narrow space without permanently trapping the player;
2. several entities converging on one target and recovering when the target becomes occupied;
3. a blocked entity detecting no progress and cancelling/replanning rather than retrying forever;
4. player input remaining responsive while an NPC is crowding nearby;
5. recovery positions surviving save/load without overlap or invalid navigation;
6. regression coverage for normal move-to, hunger, follow, and construction movement.

## Crowd behavior boundary

Active entities may collide with the player and with other active entities so a mob has meaningful physical resistance. The safety layer must distinguish a difficult crowd from a deadlock: after a bounded enclosure interval, it cancels or replans the least-important crowd movement and leaves a valid player escape route. The system must not teleport the player or let a crowd pass through solid world geometry.

The system should be a movement-safety layer around existing navigation and orders. It should not introduce formations, tactical crowd AI, or arbitrary teleportation.
