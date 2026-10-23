# 1.2 territory-return validation

## Playable target

Each starter wild creature owns a durable 35 m territory centered on its spawn. Ordinary roaming targets stay inside that territory. If a wild creature is moved outside it, the existing decision policy requests a return trip through Godot navigation. Hunger cancels travel for food; after feeding, the creature can resume the return. Captured or domesticated creatures are not forced home.

## Automated checks

Run the pinned Release build and simulation tests in the root README. Focused `WanderingServiceTests` cover deterministic in-territory goals, displacement and return, hunger interruption/resumption, captured-creature behavior, and route-failure retry delay/action cleanup.

Run the actual Godot navigation scenario:

```powershell
& "C:\Developement\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path game --headless -- --validate-wild-territories
```

Require exit code 0 and `WILD TERRITORY VALIDATION PASSED`. The scenario confirms a starter wolf receives a home territory, starts with a roaming target inside it, is displaced just beyond its border, returns using the live navigation adapter, then resumes local roaming.

## Manual play check

1. Start a new game, open Settlement, and select the wolf from the entity list.
2. Confirm the inspector reports a wandering action and a changing navigation target in the countryside. After saving and loading, confirm the wolf resumes local roaming from its saved position.
3. Run `--validate-wild-territories` for the forced-displacement check; the playable prototype does not expose a player-facing creature teleport control.
4. Focused tests lower satiety during return, verify food priority cancels the route, then restore satiety and verify return resumes. They also capture a wolf outside its territory and verify no forced return.

The 35 m starter radius is provisional. This slice does not add visible territory borders, threat sensing, pack/herd cohesion, migration, or an ecological population model.

## Wolf hunt and feeding validation

The starter wolf begins hungry, the starter cow spawns within its home territory, and no free meat pile is provided. Wolf prey species and carcass yield are defined in `data/species.json`. Hunting selects the nearest eligible wild prey deterministically, uses the existing navigation and torso combat paths, removes defeated prey once into a meat carcass, and lets the existing food service consume that meat. A failed route clears the active target and retries after a delay.

Run the actual Godot scenario:

```powershell
& "C:\Developement\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path game --headless -- --validate-wolf-hunting
```

Require exit code 0 and `WOLF HUNTING VALIDATION PASSED`. The scenario advances the live game tick and navigation until it observes increased wolf satiety, consumed food, a world carcass, and removal of the hunted cow.

Focused `CreatureHuntingServiceTests` verify nearest-prey and stable-ID selection, exclusions for dead/captured/domesticated/wrong-species/friendly targets, navigation failure cleanup, configured carcass yield, and eating through `FoodConsumptionService`.

For a manual check, start a fresh game and watch the hungry wolf near the cow outside the town. It should travel to the cow, attack until the cow dies, leave a meat marker, then travel to and eat from the carcass. The early marker is a simple resource indicator; there is no detailed carcass mesh, pack hunting, prey defense, tracking, or carcass decay in this slice.
