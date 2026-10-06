# Construction build mode direction

This document records the requested expansion of the Milestone 0.4 construction direction. It extends the bounded starter-hut acceptance without changing that acceptance retroactively.

## Two construction workflows

The project has two distinct placement workflows. They share authoritative build definitions, placement validation, construction sites, material delivery, and progress, but they use different camera and input rules.

### Direct placement (0.4 prototype / hotbar ribbon)

This in-world workflow is used while controlling the player: open **Settlement → Build mode**, choose a definition from the hotbar-style ribbon, move the player through the world, and place the framed ghost at the player-relative placement ray/range. The player remains in the normal third-person simulation.

### Free-camera planning (Sims-style settlement tooling)

This later workflow is entered explicitly from the Settlement menu. The camera detaches from the player and can pan, orbit, and zoom over the bounded settlement area. The player can place or queue multiple ghost blueprints without moving the player character. Confirmed plans submit to the same construction-site and material/work queues. Exiting planning mode restores the player-follow camera and input context; it must never teleport or silently move the player.

Planning-camera controls are a separate follow-up issue. Do not make the direct-placement prototype depend on detached-camera tooling.

## Direct placement access and player flow

Build mode is opened and closed from the **Settlement** menu. It is a settlement placement mode, separate from Inventory, object interaction, and character creation.

1. Open **Settlement → Build mode**.
2. Browse a hotbar-style ribbon of approved build definitions: buildings, crafting tables, farms, and other placeable objects.
3. Select a definition. The game shows a framed ghost blueprint at the cursor/placement ray, including footprint, rotation, terrain requirements, navigation exclusion, and required materials.
4. The preview is green only when placement is valid. Invalid terrain, overlap, range, missing prerequisites, or blocked navigation show a red preview and a visible reason.
5. Confirming creates an authoritative construction site. Canceling removes only the preview and consumes nothing.
6. The site displays structural layers and progress. Entities can deliver required materials and perform work through the shared actor construction service.
7. When all material and work layers are complete, the site becomes the selected building, table, farm, or other object. Its own interaction menu then exposes the actions appropriate to that definition.

Character creation and body-part sliders are separate from both construction workflows and are assigned to 0.2 Character Core and 1.0 First Real Game. Build mode may inspect builder capabilities, but it does not edit body proportions.

## Domain boundaries

- Build definitions are data-driven and identify footprint, visual layers, required materials, work steps, capabilities, and the resulting structure/object type.
- The authoritative `World` owns construction sites, supplied material counts, work progress, and completed objects. Godot ghost meshes, framing, progress bars, and menus are projections.
- Player, human NPC, and tool-capable creature actions use the same placement, supply, and work services. Autonomous job selection and pathing remain separate issues.
- A construction site accepts materials atomically. Partial delivery is visible and durable; invalid or stale deliveries do not consume items.
- Structural layers are ordered and inspectable. A layer can be planned, supplied, built, or complete; the presentation should show which layer is blocking completion.
- Completed objects expose their own domain menu. Crafting tables, cooking stations, smelters, forges, and similar objects reuse the existing recipe/work-order/resource-transfer systems rather than creating UI-only inventories.

## Initial expansion slice

The first implementation after the current starter-hut acceptance should remain small:

- add one data-driven `BuildDefinition` catalog with the existing hut plus one crafting table;
- open it through Settlement → Build mode;
- replace the single starter-hut action with the ribbon and preview projection;
- support one rotation/placement validation path;
- show two or three structural layers with material and work progress;
- let a player and one tool-capable actor supply/build the site;
- open the completed crafting table menu and run one existing recipe;
- persist an in-progress site and completed object through the existing snapshot migration path.

Do not add free-form architectural editing, large blueprint libraries, worker scheduling, procedural structures, ownership rules, demolition, or a complete crafting/cooking/smelting/forging catalog in the first slice.
