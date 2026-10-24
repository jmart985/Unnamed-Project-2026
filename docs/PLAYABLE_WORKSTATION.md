# Playable town workbench

The town workbench is a selectable production target in the paved work area at `(10, 0, 10)`, clear of the central road crossing. Press **T** until the workbench is selected, move within 2 metres, then press **F** to open its controls. The panel shows the active recipe, input storage, output storage, and work progress.

The first provisional recipe is intentionally small: one plant and one wood produce two food after five work steps. Carry the materials, load each input, apply work five times, then collect the output. Every rejected action leaves carried items, workbench storage, and progress unchanged. Output capacity is checked before any work is applied.

Workbench position, inputs, outputs, active recipe, and progress are authoritative world state. Save schema 21 persists them. Schemas 11–20 migrate the fixed starter bench from the old crossroads to the paved work area while preserving its production state; schemas 4–10 restore the starter bench there with an empty basic-meal recipe.

This slice establishes one production loop for milestone 0.4. Recipe selection, workers, queues, construction, farming, beds, power, equipment bonuses, and broad crafting content remain in their focused follow-up issues.

## Validation

Run the normal solution build and simulation tests. The Godot `--validate-workbench` path verifies the rendered target, close-range F interaction, input loading, five work steps, exact output collection, partial-progress save/load, repeated reset, navigation footprint, and occlusion. A rendered production-panel image is written to `docs/evidence/workbench/production-controls.png` when a display is available.
