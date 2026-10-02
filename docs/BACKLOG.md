# Implementation backlog

The [GitHub issues](https://github.com/jmart985/Unnamed-Project-2026/issues) are the editable task specifications and source of truth for scope, acceptance criteria, and validation. This document is a starting index.

- [Ant Farm Board](https://github.com/users/jmart985/projects/1)
- [Milestone 0.1: The Ant Farm](https://github.com/jmart985/Unnamed-Project-2026/milestone/1)
- [Milestone acceptance scenario](MILESTONE_0_1.md)
- [Milestone 0.3: Creature Core](MILESTONE_0_3.md)

## Initial work sequence

Only #1 begins Ready. Other tasks begin in Backlog and move to Ready after their dependencies are complete. The board tracks current status.

| Issue | Area | Task | Depends on |
| --- | --- | --- | --- |
| [#1](https://github.com/jmart985/Unnamed-Project-2026/issues/1) (F01) | Foundation | Create the Godot C# sandbox and build checks | None |
| [#2](https://github.com/jmart985/Unnamed-Project-2026/issues/2) (F02) | Foundation | Add a fixed simulation clock with pause and speed controls | [#1](https://github.com/jmart985/Unnamed-Project-2026/issues/1) |
| [#3](https://github.com/jmart985/Unnamed-Project-2026/issues/3) (E01) | Entity Simulation | Add stable entity IDs, spawn/remove, and state inspection | [#2](https://github.com/jmart985/Unnamed-Project-2026/issues/2) |
| [#4](https://github.com/jmart985/Unnamed-Project-2026/issues/4) (I01) | Inventory/Resources | Implement bounded containers and atomic resource transfers | [#3](https://github.com/jmart985/Unnamed-Project-2026/issues/3) |
| [#5](https://github.com/jmart985/Unnamed-Project-2026/issues/5) (N01) | Navigation | Move entities to targets and report arrival or failure | [#3](https://github.com/jmart985/Unnamed-Project-2026/issues/3) |
| [#6](https://github.com/jmart985/Unnamed-Project-2026/issues/6) (H01) | Needs/Hunger | Decrease satiety on simulation ticks and request food | [#3](https://github.com/jmart985/Unnamed-Project-2026/issues/3) |
| [#7](https://github.com/jmart985/Unnamed-Project-2026/issues/7) (J01) | Jobs | Add action priorities, resource reservations, and cancellation | [#4](https://github.com/jmart985/Unnamed-Project-2026/issues/4), [#6](https://github.com/jmart985/Unnamed-Project-2026/issues/6) |
| [#8](https://github.com/jmart985/Unnamed-Project-2026/issues/8) (H02) | Needs/Hunger | Complete the autonomous find-food, move, and eat loop | [#5](https://github.com/jmart985/Unnamed-Project-2026/issues/5), [#6](https://github.com/jmart985/Unnamed-Project-2026/issues/6), [#7](https://github.com/jmart985/Unnamed-Project-2026/issues/7) |
| [#9](https://github.com/jmart985/Unnamed-Project-2026/issues/9) (J02) | Jobs | Haul wood to storage and resume delivery after eating | [#8](https://github.com/jmart985/Unnamed-Project-2026/issues/8) |
| [#10](https://github.com/jmart985/Unnamed-Project-2026/issues/10) (C01) | Creatures | Add one data-defined creature species that wanders and eats | [#8](https://github.com/jmart985/Unnamed-Project-2026/issues/8) |
| [#11](https://github.com/jmart985/Unnamed-Project-2026/issues/11) (S01) | Save/Load | Save and restore durable world state with validation | [#9](https://github.com/jmart985/Unnamed-Project-2026/issues/9), [#10](https://github.com/jmart985/Unnamed-Project-2026/issues/10) |
| [#12](https://github.com/jmart985/Unnamed-Project-2026/issues/12) (F03) | Foundation | Verify and document the ten-minute Ant Farm acceptance scenario | [#11](https://github.com/jmart985/Unnamed-Project-2026/issues/11) |

## Workflow

Ideas → Backlog → Ready → In Progress → Testing → Done.

Keep each implementation issue focused enough for one reviewable PR. Split work further if needed and preserve links to the parent task. Acceptance checkboxes are not complete until verified. See [CONTRIBUTING.md](../CONTRIBUTING.md) for the AI-assisted workflow.

## Follow-on milestone scope

Issue [#221](https://github.com/jmart985/Unnamed-Project-2026/issues/221) defines the 0.3 Creature Core target and links its focused implementation issues. This work reuses the completed Ant Farm species catalog and wandering foundations; it does not reopen Milestone 0.1 or pull player-driven creature interactions forward from 1.0.

| Issue | Area | Task | Depends on |
| --- | --- | --- | --- |
| [#324](https://github.com/jmart985/Unnamed-Project-2026/issues/324) (C03a) | Creatures | Add validated temperament profiles to creature species | None |
| [#326](https://github.com/jmart985/Unnamed-Project-2026/issues/326) (C03c) | Creatures | Drive deterministic wild behavior from temperament | [#324](https://github.com/jmart985/Unnamed-Project-2026/issues/324) |
| [#323](https://github.com/jmart985/Unnamed-Project-2026/issues/323) (C03b) | Creatures | Model durable capture and captivity state | None |
| [#325](https://github.com/jmart985/Unnamed-Project-2026/issues/325) (C03d) | Validation | Verify the integrated creature-core scenario | [#323](https://github.com/jmart985/Unnamed-Project-2026/issues/323), [#324](https://github.com/jmart985/Unnamed-Project-2026/issues/324), [#326](https://github.com/jmart985/Unnamed-Project-2026/issues/326) |

## Deferred design topics

Capture/domestication, relationships, combat, injuries, construction, production, factions, economy, breeding, and world streaming remain outside 0.1. Refine these after the Ant Farm acceptance scenario passes; future ideas do not become milestone commitments automatically.
