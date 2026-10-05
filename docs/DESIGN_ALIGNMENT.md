# Design alignment and scope boundary

This note records the review of the supplied design references:

- `1. Character mechanics.txt`
- `1. Core Gameplay Loop and Mechanics.txt`
- `Code game systems for open-world RP.txt`
- `Design principles.txt`
- `Project vision.txt`
- `The Four Layers of Play.txt`

## Alignment findings

The repository is aligned with the supplied direction in the following ways:

- Humans and creatures use shared entity, needs, inventory, navigation, relationship, and persistence foundations where the rules are appropriate to both.
- Species data supplies physical and behavioral differences. Current capability data already covers body category, diet, health, strength, endurance, dexterity, smell range, tool use, door use, and tracking.
- The four scales are represented in the architecture: individual entity state, player-led squad membership, settlement-owned resources/work systems, and world/faction/regional simulation foundations.
- Food and resources are physical containers that are reserved, transferred, consumed, and persisted rather than global counters.
- Character health is localized to body parts, bounded, and shared by humans and creatures. Combat and injury consequences are treated as durable simulation state where the accepted scope requires them.
- The project favors inspectable behavior, deterministic tests, small reviewable issues, data-driven definitions, and prototype-first presentation.
- Player identity remains emergent. The current systems do not impose a class, heroic quest, or required career.
- The roadmap scaffold preserves the long-term systems named in the references while requiring each future milestone to earn its scope through a small playable scenario.

## Intent versus implementation scope

The supplied checklists are design inventory, not instructions to implement every unchecked item immediately. The following ideas remain valid long-term direction but are intentionally deferred until a concrete milestone slice exists:

- full behavior trees and complex utility AI;
- complete combat, equipment, crafting, and injury progression;
- advanced capture, domestication, breeding, and genetics;
- autonomous settlement expansion, diplomacy, law, crime, and faction politics;
- regional climate, dynamic weather, disasters, migration, and full ecology;
- markets, prices, demand, trade routes, and multi-settlement economy;
- distant simulation/LOD and world streaming;
- careers, emergent identity analysis, and broad player-freedom balancing;
- production assets, audio, tutorials, accessibility passes, and release packaging.

These are not missing from the vision. They are deliberately outside the current acceptance queue unless the active milestone and a concrete child issue say otherwise.

## Current implementation boundary

The current repository should continue to follow `docs/AI_WORKFLOW.md`, `docs/BACKLOG.md`, and the active GitHub Project board. A future issue is implementation-ready only when it has:

1. one observable gameplay outcome;
2. explicit dependencies against the current `main` branch;
3. bounded data and failure rules;
4. focused tests or a documented manual validation path;
5. a stated persistence and reset decision; and
6. a clear relationship to one milestone exit gate.

When a supplied design reference and a current issue appear to differ, treat the reference as the long-term design constraint and the issue as the current delivery scope. Resolve a genuine architectural conflict in `docs/DECISIONS.md` before coding.
