# Construction build mode direction

This document records the requested expansion of the Milestone 0.4 construction direction. It extends the bounded starter-hut acceptance without changing that acceptance retroactively.

## Player flow

1. Open **Build mode** from the settlement controls.
2. Browse a hotbar-style ribbon of approved build definitions: buildings, crafting tables, farms, and other placeable objects.
3. Select a definition. The game shows a framed ghost blueprint at the cursor/placement ray, including footprint, rotation, terrain requirements, navigation exclusion, and required materials.
4. The preview is green only when placement is valid. Invalid terrain, overlap, range, missing prerequisites, or blocked navigation show a red preview and a visible reason.
5. Confirming creates an authoritative construction site. Canceling removes only the preview and consumes nothing.
6. The site displays structural layers and progress. Entities can deliver required materials and perform work through the shared actor construction service.
7. When all material and work layers are complete, the site becomes the selected building, table, farm, or other object. Its own interaction menu then exposes the actions appropriate to that definition.

## Domain boundaries

- Build definitions are data-driven and identify footprint, visual layers, required materials, work steps, capabilities, and the resulting structure/object type.
- The authoritative `World` owns construction sites, supplied material counts, work progress, and completed objects. Godot ghost meshes, framing, progress bars, and menus are projections.
- Player, human NPC, and tool-capable creature actions use the same placement, supply, and work services. Autonomous job selection and pathing remain separate issues.
- A construction site accepts materials atomically. Partial delivery is visible and durable; invalid or stale deliveries do not consume items.
- Structural layers are ordered and inspectable. A layer can be planned, supplied, built, or complete; the presentation should show which layer is blocking completion.
- Completed objects expose their own domain menu. Crafting tables, cooking stations, smelters, forges, and similar objects reuse the existing recipe/work-order/resource-transfer systems rather than creating UI-only inventories.
- Build mode is a placement mode, not a replacement for the inventory, settlement, or object interaction menus. Escape/cancel returns to the prior mode without mutating the World.

## Initial expansion slice

The first implementation after the current starter-hut acceptance should remain small:

- add one data-driven `BuildDefinition` catalog with the existing hut plus one crafting table;
- replace the single starter-hut action with the ribbon and preview projection;
- support one rotation/placement validation path;
- show two or three structural layers with material and work progress;
- let a player and one tool-capable actor supply/build the site;
- open the completed crafting table menu and run one existing recipe;
- persist an in-progress site and completed object through the existing snapshot migration path.

Do not add free-form architectural editing, large blueprint libraries, worker scheduling, procedural structures, ownership rules, demolition, or a complete crafting/cooking/smelting/forging catalog in the first slice.

## Follow-up issue sequence

1. **Build definition catalog** — validated IDs, footprints, layers, materials, work steps, and resulting object type.
2. **Build mode ribbon and ghost preview** — selection, placement ray, rotation, valid/invalid feedback, cancel, and no-mutation preview behavior.
3. **Multi-layer construction progress** — authoritative staged progress, material delivery, work actions, visible layer feedback, and atomic rejection.
4. **Shared construction actors** — NPC and capable-creature delivery/work commands using the same service as the player.
5. **Completed object menus** — one crafting table consuming the existing recipe and transfer services.
6. **Construction persistence and acceptance gate** — save/load, reset, malformed snapshot rollback, rendered evidence, and one repeatable settlement scenario.

Each issue must be independently testable and remain attached to the 0.4 acceptance gate. The issue that introduces the next build-mode slice should be placed in Backlog until its dependencies and exact acceptance scenario are written.
