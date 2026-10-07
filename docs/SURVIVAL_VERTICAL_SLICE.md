# Survival vertical slice: from bare hands to shelter

This is a framework test for the next playable survival loop. It replaces the current starter wood pile only after the resource and interaction phases below are implemented and validated together.

## Target player loop

1. Start with adult trees, loose rocks, a small food source, and an empty settlement storage location. No free box of wood is available.
2. Punch a tree to obtain a small amount of wood. Punching is slow and limited; it is a starter interaction, not the final combat system.
3. Pick up a loose rock from the ground.
4. Use the rock and starter wood to craft an axe.
5. Equip the axe. Chopping an adult tree yields wood per completed chop and advances the tree toward depletion.
6. Deposit gathered wood into settlement storage.
7. Build a bed and place it inside a building with an opening and walkable interior.
8. Build a fire, light it, and use it for warmth and one cooking recipe.
9. Sleep in the placed bed. Save/load preserves the player, tools, tree state, storage, building interior, bed, fire, and queued work.

## Implementation slices

### Slice A — world resource fixtures

- Replace the starter wood pile in the new-game player path with deterministic adult-tree records and loose-rock records.
- Keep existing resource containers and atomic transfer rules; tree and rock records own harvestable quantities and positions.
- Render adult trees and rocks and expose them as selectable/interactable world targets.
- Add growth state for trees. Adult starter trees are available immediately; depleted trees advance through a documented regrowth timer.

### Slice B — bare-hand gathering and tool crafting

- Add a close-range punch-tree interaction that produces a bounded starter wood yield.
- Add pickup for a loose rock.
- Add a deterministic axe recipe using gathered wood and rock.
- Add tool inventory/equip state with save/load coverage.

### Slice C — axe harvesting and storage

- Require an equipped axe for normal tree chopping.
- Each completed chop applies work and yields wood atomically.
- Depleted trees stop yielding and enter growth state.
- Deposit wood into the existing settlement storage network; no duplicated or silently destroyed resources.

### Slice D — shelter, bed, and fire

- Extend the construction definitions with an openable shelter shell, interior walkable space, a bed, and a fire.
- Placement validation must reject blocked interiors and inaccessible bed/fire positions.
- A completed bed supports sleep and recovery. A completed fire supports warmth and one cooking recipe.
- Persist construction progress, opening state, bed occupancy, fire state, and stored resources.

## Acceptance gate

- A fresh session contains no starter wood box and does contain adult trees and loose rocks.
- The player can complete the bare-hand wood and rock steps, craft/equip an axe, chop a tree, store the result, build a bed and fire, cook once, and sleep.
- Tree regrowth is deterministic and does not respawn resources instantly.
- Buildings have openings and interiors that can be entered and exited.
- Every transfer is atomic and every durable state survives save/load.
- Existing Ant Farm, construction, workstation, farm, and crowd-safety validations continue to pass.

Keep this slice deliberately small: one tree definition, one rock definition, one axe, one shelter, one bed, one fire, and one recipe. More species, tools, crops, recipes, and building sets remain follow-up issues.
