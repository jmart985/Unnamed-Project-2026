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
