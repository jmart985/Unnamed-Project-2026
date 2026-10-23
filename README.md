# Unnamed Project 2026

A systemic open-world colony RPG in which people and creatures share consistent simulation rules. **0.1: The Ant Farm** is the historical foundation baseline; active work follows the current GitHub milestone and its concrete implementation issues.

**Status:** The roadmap is progressing through later milestones, with active work tracked on GitHub. **Pinned stack:** Godot 4.7.2 .NET (Mono) + .NET SDK 10.0.401; revisit after the prototype if evidence warrants it.

## Work on GitHub

- [Project board](https://github.com/users/jmart985/projects/1): Ideas → Backlog → Ready → In Progress → Testing → Done
- [Milestone 0.1: The Ant Farm](https://github.com/jmart985/Unnamed-Project-2026/milestone/1)
- [Milestone 0.2: Character_Core](https://github.com/jmart985/Unnamed-Project-2026/milestone/26)
- [Milestone 0.3: Creature_Core](https://github.com/jmart985/Unnamed-Project-2026/milestone/27)
- [Milestone 0.4: Basic_Settlement](https://github.com/jmart985/Unnamed-Project-2026/milestone/28)
- [Milestone 0.5: Combat Prototype](https://github.com/jmart985/Unnamed-Project-2026/milestone/29)
- [Milestone 0.6: Relationships & Loyalty](https://github.com/jmart985/Unnamed-Project-2026/milestone/30)
- [Milestone 0.7: Factions](https://github.com/jmart985/Unnamed-Project-2026/milestone/31)
- [Milestone 0.8: Economy Prototype](https://github.com/jmart985/Unnamed-Project-2026/milestone/32)
- [Milestone 0.9: Vertical Integration](https://github.com/jmart985/Unnamed-Project-2026/milestone/33)
- [Milestone 1.0: First Real Game](https://github.com/jmart985/Unnamed-Project-2026/milestone/4)
- [Milestone 1.1: Job Intelligence](https://github.com/jmart985/Unnamed-Project-2026/milestone/5)
- [All implementation issues](https://github.com/jmart985/Unnamed-Project-2026/issues)
- **Start here:** choose the earliest independent **Ready** implementation issue in the active milestone.

## Start here

- [Project vision](docs/PROJECT_VISION.md)
- [Design principles](docs/DESIGN_PRINCIPLES.md)
- [Gameplay loop](docs/GAMEPLAY_LOOP.md)
- [Technical architecture](docs/TECHNICAL_ARCHITECTURE.md)
- [Creature rig families and species generation](docs/CREATURE_RIG_FAMILIES.md)
- [Interaction anchor standard](docs/INTERACTION_ANCHOR_STANDARD.md)
- [Decisions log](docs/DECISIONS.md)
- [Milestone scope and acceptance](docs/MILESTONE_0_1.md)
- [Combat prototype scope and acceptance](docs/MILESTONE_0_5.md)
- [First Real Game scope and acceptance](docs/MILESTONE_1_0.md)
- [Squad controls and validation](docs/VALIDATION_SQUAD.md)
- [Player menus, character inventory and button mapping](docs/PLAYER_MENUS.md)
- [Gameplay target selection and cycling](docs/WORLD_SELECTION.md)
- [Capture/feed/pet/give repair and current interaction controls](docs/INTERACTION_REPAIR.md)
- [Follow distance, player clearance and physics validation](docs/FOLLOW_SPACING.md)
- [NPC daily schedules and validation](docs/NPC_SCHEDULES.md)
- [Social feedback and Trusted petting](docs/SOCIAL_FEEDBACK.md)
- [Town-center workbench presentation](docs/TOWN_WORKBENCH.md)
- [Playable workbench production and persistence](docs/PLAYABLE_WORKSTATION.md)
- [Playable town farm and shared actor actions](docs/PLAYABLE_FARM.md)
- [Playable construction and starter hut](docs/PLAYABLE_CONSTRUCTION.md)
- [Basic Settlement integrated validation](docs/VALIDATION_0_4.md)
- [Daily schedule integrated acceptance](docs/VALIDATION_1.1.md)
- [First Real Game integrated validation](docs/VALIDATION_1_0.md)
- [Work sequence and issue index](docs/BACKLOG.md)
- [Contribution and AI workflow](CONTRIBUTING.md)
- [AI contributor workflow](docs/AI_WORKFLOW.md)

## Completed 0.1 baseline

The original 0.1 target was one fixed rectangular map, 10 human NPCs, 3 individuals of one creature species, one separately controlled human player, food and wood, one stockpile, hunger, eating, hauling, wandering, basic inspection, and manual save/load. Its ten-minute unattended run remains a regression scenario for the completed foundation.

The 0.1 acceptance documents remain useful for regression checks against the original foundation. Their scope exclusions apply only to 0.1 work; they do not block implementation in later milestones.

Start with the earliest independent Ready implementation issue in the active milestone.

## Local setup

Install Godot `4.7.2.stable.mono.official.ed1daf0bf` and .NET SDK `10.0.401`.

From the repository root:

```powershell
dotnet restore UnnamedProject2026.slnx
dotnet build UnnamedProject2026.slnx --configuration Release
dotnet test tests\Simulation.Tests\Simulation.Tests.csproj --configuration Release --no-restore
& "C:\Developement\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path game --editor
& "C:\Developement\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path game --headless --quit
```

The editor opens `game\main.tscn`, a flat placeholder map (400 x 400 m: a 120 x 120 m town surrounded by countryside) with a chase camera. Build and test output should report success; the headless Godot command should exit with code 0. The Godot executable path is an example Windows installation path; use the pinned executable from the local installation when it differs.

The startup scene currently bypasses character creation and starts a test game as `Test Player` with 10 human NPCs and one ape, wolf, and cow. Species-compatible plant and meat food sources are placed near the countryside creatures. Set `SkipCharacterCreationForTesting` on the Main node to `false` to restore the creation form when testing is closer to complete. The player is a jointed procedural rig shaped by the creation choices (height, body type, sex), with procedural walk/idle, mouse head-look and wind-up/strike/recovery attacks. T cycles visible targets, including the town workbench and farm; F opens their controls within 2 m or feeds/pets a selected creature. The farm consumes one plant, takes five explicit growth steps and yields three food; NPCs and tool-capable creatures use the same domain actions when a job assigns them. Mouse attacks resolve one aimed body-part hit against the captured target within 2.2 m, while facing it with an unobstructed strike. Tap E collects; hold E for 0.6 s uses one compatible carried food. G greets humans and supported creatures within 2 m. Successful interactions have visible feedback and sustained facing toward the live player during and after acknowledgment. See [gameplay refinement and validation](docs/GAMEPLAY_REFINEMENT.md), [playable workbench](docs/PLAYABLE_WORKSTATION.md), and [playable farm](docs/PLAYABLE_FARM.md) for controls and prototype limitations. The scene exposes Pause/Resume, 1x, 4x, and Reset controls. Domain time advances in fixed 0.1-second ticks. A frame processes at most eight ticks; any remaining overflow is discarded to prevent a long-frame catch-up spiral.
