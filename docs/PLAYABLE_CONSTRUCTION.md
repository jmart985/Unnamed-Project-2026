# Playable construction and starter hut

Milestone 0.4 issue #404 connects the existing blueprint, construction-site, material and structure foundations into one bounded in-game loop. Construction state belongs to the authoritative `World`; previews, site framing, hut meshes, target HUD and panels are projections.

## Build loop

1. Collect six wood.
2. Open Settlement → Overview and choose **Place starter hut**.
3. The snapped 4 × 4 m preview appears five metres ahead. Green is valid and red is rejected. Confirm or cancel with the visible placement controls.
4. Select the site with the Target control, stand within 2 m and press Interact.
5. Supply six carried wood, then apply five work steps.
6. Completion removes the site and creates one targetable hut. Select the hut and press Interact to open or close its door.

Placement stays within 8 m and rejects invalid terrain, the existing navigation exclusion, the town boundary and overlap with structures, sites, the workbench, farm or stockpile without consuming materials. Supply and work reject stale/remote sites, missing material, premature work, incapacitation and species without tool-use capability without partial mutation.

`ActorConstructionService` accepts a live actor ID. Players, human NPCs and tool-capable creatures call the same placement, supply and work operations. This slice supplies actionable commands for later job selection and navigation; it does not autonomously choose builders, demolish buildings or introduce free-form architecture.

## Persistence

Snapshot schema 13 stores incomplete site blueprint identity, footprint, requirements, supplied materials and work, plus completed structure type, position and door state. Schema 12 and earlier migrate with empty construction state. Full snapshot validation occurs before live World replacement.

## Validation

Build Debug, then run Godot with `--path game --headless -- --validate-construction`; omit `--headless` for rendered evidence. Require process exit 0 and `CONSTRUCTION VALIDATION PASSED`. The scenario uses the settlement placement controls, validates one authoritative target through supply/work/completion, checks an open doorway ray and closed-door obstruction, toggles the door, saves/loads, resets, and captures `docs/evidence/construction/completed-hut.png`.

Domain coverage also proves shared NPC/tool-capable-creature actions and atomic rejection for invalid placement and incapable creatures. The complete simulation and interaction suites remain required regressions.
