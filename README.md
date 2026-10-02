# Unnamed Project 2026

A systemic open-world colony RPG in which people and creatures share consistent simulation rules. **0.1: The Ant Farm** is the historical foundation baseline; active work follows the current GitHub milestone and its concrete implementation issues.

**Status:** The roadmap is progressing through later milestones, with active work tracked on GitHub. **Pinned stack:** Godot 4.7.2 .NET (Mono) + .NET SDK 10.0.401; revisit after the prototype if evidence warrants it.

## Work on GitHub

- [Project board](https://github.com/users/jmart985/projects/1): Ideas → Backlog → Ready → In Progress → Testing → Done
- [Milestone 0.1: The Ant Farm](https://github.com/jmart985/Unnamed-Project-2026/milestone/1)
- [Milestone 0.5: Combat Prototype](https://github.com/jmart985/Unnamed-Project-2026/milestone/29)
- [Milestone 0.6: Relationships & Loyalty](https://github.com/jmart985/Unnamed-Project-2026/milestone/30)
- [Milestone 1.0: First Real Game](https://github.com/jmart985/Unnamed-Project-2026/milestone/4)
- [All implementation issues](https://github.com/jmart985/Unnamed-Project-2026/issues)
- **Start here:** choose the earliest independent **Ready** implementation issue in the active milestone; [#221](https://github.com/jmart985/Unnamed-Project-2026/issues/221) tracks the current Creature Core scope and its implementable child issues.

## Start here

- [Project vision](docs/PROJECT_VISION.md)
- [Design principles](docs/DESIGN_PRINCIPLES.md)
- [Gameplay loop](docs/GAMEPLAY_LOOP.md)
- [Technical architecture](docs/TECHNICAL_ARCHITECTURE.md)
- [Decisions log](docs/DECISIONS.md)
- [Milestone scope and acceptance](docs/MILESTONE_0_1.md)
- [Combat prototype scope and acceptance](docs/MILESTONE_0_5.md)
- [First Real Game scope and acceptance](docs/MILESTONE_1_0.md)
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

The editor opens `game\main.tscn`, a flat placeholder map with an overhead camera. Build and test output should report success; the headless Godot command should exit with code 0. The Godot executable path is an example Windows installation path; use the pinned executable from the local installation when it differs.

The startup scene exposes Pause/Resume, 1x, 4x, and Reset controls. Domain time advances in fixed 0.1-second ticks. A frame processes at most eight ticks; any remaining overflow is discarded to prevent a long-frame catch-up spiral.


