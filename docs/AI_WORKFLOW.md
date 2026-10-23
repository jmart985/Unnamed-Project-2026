# AI contributor workflow

This document defines how AI-assisted work should move the project forward.

## Choose current work

1. Read the repository instructions, the active milestone, the parent issue, and the complete assigned issue.
2. Select the smallest independent implementation issue marked Ready in the earliest active milestone. If none is Ready, inspect Backlog dependencies and board instructions; promote concrete, eligible work whose prerequisites and acceptance criteria are satisfied. If the milestone is complete, inspect the next incomplete milestone. Report a genuine unresolved product decision or dependency instead of implementing vague or blocked work.
3. Verify dependencies against the current `main` branch. Do not implement a parent or planning issue when its child issues are the actionable work.
4. Work on one issue per branch and pull request. Keep changes limited to the issue's acceptance criteria.

The active milestone changes as the roadmap advances. Do not assume Milestone 0.1 is still active. `docs/MILESTONE_0_1.md`, `docs/VALIDATION_0_1.md`, and the historical 0.1 backlog sequence describe the completed foundation and remain useful for regression checks, but they do not override a later milestone or a current issue.

## Implement and validate

- Apply [`IMPLEMENTATION_GUIDELINES.md`](IMPLEMENTATION_GUIDELINES.md) when an issue needs clearer acceptance criteria, system ownership, persistence rules, validation, or scale constraints.
- Inspect existing code and relevant design docs before changing files.
- Prefer the smallest data-driven Godot 4 + C# implementation that satisfies the issue.
- Add or update focused tests where practical.
- Run the relevant build, tests, and manual scenario; report only validation that actually ran.
- Update documentation when behavior or a deliberate architectural decision changes.

## Finish the issue

Review the diff for unrelated edits, commit the branch, push it, and open a pull request that links the issue. Leave the issue open until its acceptance criteria are verified and the pull request is merged. After merging, return to the latest `main` and select the next independent Ready issue.

When no independent Ready issue remains after the readiness/dependency audit, report the dependency or decision blocking progress instead of inventing substitute architecture or silently widening scope.

## Current accepted slices

### Current project status (2026-10-23)

Milestone 1.2 territory and wolf hunting (#461–#463), prey response (#467/#469), carcass freshness (#471), and World-owned far-population aggregates (#473) are merged. PR #474 delivered #473 with snapshot schema 19. The next bounded Living Region slice is #475: validate one starter wildlife region, keep its distant populations abstract, activate and demote wildlife by player proximity, and persist active membership in schema 20 so survivors return to the right counts and dead/removed wildlife is not restored. Predator teamwork, migration, respawning, and broad world streaming remain future scope; use tracker #202 for later evidence-based expansion.

### Historical project status (2026-10-05)

At that point, the Ant Farm board was the live routing source for implementation work. Character Core issues #135, #136, #137, and #139, far-population validation #369, actor-spacing work #415, and Basic Settlement acceptance #406 were complete and belonged in **Done**. Deferred health/facing persistence #302, future capability-gated actions #306, Advanced Creature Behavior #201, Captivity Systems #225, and Breeding & Genetics #226 were planning horizons and belonged in **Backlog**. There were no independent Ready implementation issues at that time; the current route is recorded above.

Do not infer readiness from an issue being open or assigned a future milestone. A closed issue with a merged pull request is Done even if an old project status was left behind; a planning issue remains Backlog until its minimum playable slice, dependencies, and validation steps are explicit.

The 1.0 integrated game loop is documented in `docs/VALIDATION_1_0.md`. The user selected daily work/rest schedules for 1.1 in #200: feature #380 then acceptance gate #381, with evidence in `docs/VALIDATION_1.1.md`. After that gate merges, inspect #201 (1.2 Advanced Creature Behavior) and current Project #1 before selecting more work; it is a planning issue that needs a concrete minimum playable target. Deferred #302/#306 retain their scope boundaries; #369 and #415 are completed implementation work.

The user subsequently prioritized unfinished player menus before 1.2: #384 is a focused correction to completed 1.0 functionality. Its Inventory, Settlement and Save/Settings screens, button mapping, and deliberately disabled future placeholders are documented in `docs/PLAYER_MENUS.md`. Complete that correction before advancing #201; placeholders do not make future equipment/audio/display implementation Ready.

The next user-requested 1.0 correction is #388: gameplay target selection/cycling without opening menus, documented in `docs/WORLD_SELECTION.md`. It reuses accepted entity actions and resource projections, preserves existing input preferences, and leaves the #201 product-scope decision separate.

User playtesting then selected #390: targeted strike resolution, exposed humans overlapping storage, visible greeting/facing feedback, and tap-collect versus hold-use. `docs/GAMEPLAY_REFINEMENT.md` defines this focused correction; reuse existing combat, World inventory and reservation services. Health persistence #302 and the #201 future creature scope remain separate.

The user next requested #393 to repair the subsequently added InteractionResult/InteractionResolver capture, feed, pet and give paths and build diagnostics. `docs/INTERACTION_REPAIR.md` records current F feed/pet and G greeting behavior, truthful rejection, atomic transfers and sustained facing. Finish this repair before the pending health-persistence follow-up under #302; do not treat future #201 planning as Ready implementation.

After #393 merged, user playtesting selected #395: prevent follow orders from pushing the player across the map. `docs/FOLLOW_SPACING.md` records the 2 m stop / 3 m resume band and live movement boundary, validated with player physics enabled. Complete this focused 1.0 regression before the pending #302 health-persistence scope; #201 remains future planning.

After #395, the user selected #397: hidden relationship totals, unit changes above creatures/NPCs and for faction standing, reversible Trusted petting, and domestic starting trust. `docs/SOCIAL_FEEDBACK.md` defines directed recipient-to-actor responses, existing bond thresholds, non-resetting defaults and exact save compatibility. The player's outgoing relationship UI is hidden and capture remains unavailable for humans. This accepted 1.0 correction precedes the pending #302 persistence scoping and #201 minimum-playable-target decision.

User playtesting next selected #399: replace the town-center obstacle cube with recognizable workbench presentation at the established placement. `docs/TOWN_WORKBENCH.md` records static landmark geometry, actual occlusion and reset/load checks. This visual correction does not implement crafting controls or workstation snapshot persistence.

The milestone 0.4 audit found that its closed issues delivered domain foundations but not a playable settlement loop. The focused correction lane is #401 -> #403 -> #404 -> #405 -> #406. Issue #401 makes the existing town workbench selectable and usable, with atomic player material transfers and schema 11 persistence documented in `docs/PLAYABLE_WORKSTATION.md`. Do not pull farming, construction, building, bed, worker, queue, or content expansion scope into #401; those capabilities remain ordered in the later issues.

Issue #403 makes the fixed starter farm selectable and usable through explicit plant, growth and harvest controls, with schema 12 persistence documented in `docs/PLAYABLE_FARM.md`. `ActorFarmService` deliberately supports humans and species with tool-use capability so later NPC/creature jobs can call the same atomic actions. This issue does not autonomously choose or path workers; construction remains next at #404.

Issue #404 supplies one bounded starter-hut placement, material and work loop through shared actor commands, with schema 13 site/structure/door persistence documented in `docs/PLAYABLE_CONSTRUCTION.md`. Beds and recovery remain ordered in #405.

Issue #406 accepted the playable Basic Settlement loop after #401, #403, #404 and #405. `docs/VALIDATION_0_4.md` records the integrated workbench, farm, hut, door, sheltered-bed rest, save/load, invalid rollback, reset and rendered-evidence gate. The construction implementation is therefore complete for the current 0.4 slice; build-mode expansion remains a separately scoped follow-up in `docs/CONSTRUCTION_BUILD_MODE.md`. #302 and #306 remain deferred backlog scope.
