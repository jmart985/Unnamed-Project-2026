# Playable construction and starter hut

Milestone 0.4 issue #404 connects the existing blueprint, construction-site, material and structure foundations into one bounded in-game loop. Construction state belongs to the authoritative `World`; previews, site framing, hut meshes, target HUD and panels are projections.

## Build loop

1. Collect six wood.
2. Open **Build mode** from Settlement, or press **B** (default; configurable in Controls). A ten-slot ribbon appears without pausing the simulation or capturing UI clicks. Slots **1–9 and 0** select matching build slots only while Build mode is active; slots 1–4 are starter hut, fireplace, additional workbench, and additional farm, while 5–0 are empty. The original starter workbench and farm remain in the world. Those number keys have no build action outside this mode.
3. The player keeps moving and looking. The selected ghost follows the centered camera reticle's ground intersection, snaps to the existing placement grid, and shows green when valid or red with a reason when rejected. Left click confirms; Escape or B exits without consuming materials. The hut creates a construction site, the fireplace consumes its existing wood cost, and additional workbenches/farms are immediate prototype placements without material cost or staged progress. The normal left-click combat action is unchanged outside Build mode.
4. Select the site with the Target control, stand within 2 m and press Interact.
5. Supply six carried wood, then apply five work steps.
6. Completion removes the site and creates one targetable hut with solid floor, ceiling, wall, and door collision. Select the hut and press Interact to open or close its door; the door collision and navigable doorway update together.

Placement stays within 8 m and rejects invalid terrain, the navigation exclusions for all workbenches, the town boundary and overlap with structures, sites, existing workbenches, farms, fireplaces or stockpiles without partial mutation. Navigation boundaries split around each two-metre workbench clearance so a fixture does not remove an oversized town cell. Scenery buildings have solid collision and remain excluded from NPC navigation when fixture boundaries subdivide their grid cells. Completed huts use matching physical wall/floor/ceiling collision and navigation wall segments; their open doorway remains passable, and closing it blocks both movement and navigation. New workbench and farm IDs, storage and crop state are World-owned and saved through existing snapshot records. Direct placement uses the hidden mouse and fixed center reticle; controller defaults are left-stick click to toggle build mode, D-pad left/right to cycle the four available options, left shoulder to place and B to exit. Supply and work reject stale/remote sites, missing material, premature work, incapacitation and species without tool-use capability without partial mutation.

`ActorConstructionService` accepts a live actor ID. Players, human NPCs and tool-capable creatures call the same placement, supply and work operations. This slice supplies actionable commands for later job selection and navigation; it does not autonomously choose builders, demolish buildings or introduce free-form architecture.

## Persistence

Snapshot schema 13 stores incomplete site blueprint identity, footprint, requirements, supplied materials and work, plus completed structure type, position and door state. Schema 12 and earlier migrate with empty construction state. Full snapshot validation occurs before live World replacement.

## Validation

Build Debug, then run Godot with `--path game --headless -- --validate-construction`; omit `--headless` for rendered evidence. Require process exit 0 and `CONSTRUCTION VALIDATION PASSED`. The scenario checks the non-modal hotbar, keys 1–4 and empty slots, D-pad cycling, movement/look-following previews, invalid-placement rejection, multiple workbench/farm creation, independent fixture targeting and state, exact workbench navigation clearance, one authoritative hut target through supply/work/completion, doorway/door behavior, save/load and reset. It captures `docs/evidence/construction/completed-hut.png` when a display is available.

Domain coverage also proves shared NPC/tool-capable-creature actions and atomic rejection for invalid placement and incapable creatures. The complete simulation and interaction suites remain required regressions.
