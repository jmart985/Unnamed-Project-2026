# Construction build mode direction

This document records the requested expansion of the Milestone 0.4 construction direction. It extends the bounded starter-hut acceptance without changing that acceptance retroactively.

## Two construction workflows

The project has two distinct placement workflows. They share authoritative build definitions, placement validation, construction sites, material delivery, and progress, but they use different camera and input rules.

### Direct placement (0.4 prototype / hotbar ribbon)

This in-world workflow is used while controlling the player: open **Settlement → Build mode** or press the configurable Build mode binding (default **B**), choose a definition from the hotbar-style ribbon, move the player through the world, and place the framed ghost under the centered camera reticle. The player remains in the normal third-person simulation, including movement and camera look.

### Free-camera planning (Sims-style settlement tooling)

This later workflow is entered explicitly from the Settlement menu. The camera detaches from the player and can pan, orbit, and zoom over the bounded settlement area. The player can place or queue multiple ghost blueprints without moving the player character. Confirmed plans submit to the same construction-site and material/work queues. Exiting planning mode restores the player-follow camera and input context; it must never teleport or silently move the player.

Planning-camera controls are a separate follow-up issue. Do not make the direct-placement prototype depend on detached-camera tooling.

The current direct-placement hotbar has four implemented options:

- **1 — Starter hut:** creates the existing six-wood, five-work construction site.
- **2 — Fireplace:** creates the existing two-wood fireplace.
- **3 — Workbench:** creates another independent World-owned workstation with its own storage and recipe state.
- **4 — Farm:** creates another independent World-owned farm with one empty plot.

The original starter workbench and farm remain in place. Additional workbenches and farms are immediate prototype placements with no material cost or staged construction layers because no approved material/work definitions exist for them yet. Their positions and gameplay state use existing snapshot fields. Every workbench receives collision and a matching two-metre navigation clearance. The navigation grid adds boundaries around each workbench clearance so one fixture does not remove an entire large town cell; nearby walkable cells remain available. Farms use independent presentation and interaction targets. Slots 5–0 are visibly empty. These choices make the existing production systems placeable without pretending their full construction recipes are designed. A later construction slice can give them material layers and build progress.

## Direct placement access and player flow

Build mode is opened and closed from the **Settlement** menu. It is a settlement placement mode, separate from Inventory, object interaction, and character creation.

1. Open **Settlement → Build mode** from the Settlement menu or use the configurable Build mode binding (keyboard **B**, controller left-stick click by default).
2. Browse the ten-slot hotbar. Keyboard keys **1–9 and 0** select matching slots only while Build mode is open; they remain unassigned to build actions during normal play. Controller D-pad left/right cycles through the four available options. Slots 1–4 are Starter hut, Fireplace, Workbench, and Farm; slots 5–0 are empty.
3. Select an item. The OS cursor stays hidden, the center reticle remains fixed, and the preview follows the camera-center ray onto the ground as the player moves or looks. The hotbar ignores mouse input, so movement, jumping, and camera look remain active. Left click (controller left shoulder by default) places the selected ghost; Escape or the Build mode binding closes the mode. Outside Build mode, left click retains its normal combat action.
4. The preview is green only when placement is valid. Invalid terrain, overlap, range, missing prerequisites, or blocked navigation show a red preview and a visible reason.
5. Confirming a hut creates an authoritative construction site. Confirming a fireplace, workbench, or farm creates its existing World-owned object immediately. Canceling removes only the preview and consumes nothing; invalid placement creates nothing.
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

## Later construction expansion

The next construction slice can add staged blueprints for production objects, beginning with one crafting table:

- add one data-driven `BuildDefinition` catalog with the existing hut plus a staged crafting table;
- open it through Settlement → Build mode;
- replace the single starter-hut action with the ribbon and preview projection;
- support one rotation/placement validation path;
- show two or three structural layers with material and work progress;
- let a player and one tool-capable actor supply/build the site;
- open the completed crafting table menu and run one existing recipe;
- persist an in-progress site and completed object through the existing snapshot migration path.

Do not add free-form architectural editing, large blueprint libraries, worker scheduling, procedural structures, ownership rules, demolition, or a complete crafting/cooking/smelting/forging catalog in that slice.
