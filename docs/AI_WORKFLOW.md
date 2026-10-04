# AI contributor workflow

This document defines how AI-assisted work should move the project forward.

## Choose current work

1. Read the repository instructions, the active milestone, the parent issue, and the complete assigned issue.
2. Select the smallest independent implementation issue marked Ready in the earliest active milestone. If none is Ready, inspect Backlog dependencies and board instructions; promote concrete, eligible work whose prerequisites and acceptance criteria are satisfied. If the milestone is complete, inspect the next incomplete milestone. Report a genuine unresolved product decision or dependency instead of implementing vague or blocked work.
3. Verify dependencies against the current `main` branch. Do not implement a parent or planning issue when its child issues are the actionable work.
4. Work on one issue per branch and pull request. Keep changes limited to the issue's acceptance criteria.

The active milestone changes as the roadmap advances. Do not assume Milestone 0.1 is still active. `docs/MILESTONE_0_1.md`, `docs/VALIDATION_0_1.md`, and the historical 0.1 backlog sequence describe the completed foundation and remain useful for regression checks, but they do not override a later milestone or a current issue.

## Implement and validate

- Inspect existing code and relevant design docs before changing files.
- Prefer the smallest data-driven Godot 4 + C# implementation that satisfies the issue.
- Add or update focused tests where practical.
- Run the relevant build, tests, and manual scenario; report only validation that actually ran.
- Update documentation when behavior or a deliberate architectural decision changes.

## Finish the issue

Review the diff for unrelated edits, commit the branch, push it, and open a pull request that links the issue. Leave the issue open until its acceptance criteria are verified and the pull request is merged. After merging, return to the latest `main` and select the next independent Ready issue.

When no independent Ready issue remains after the readiness/dependency audit, report the dependency or decision blocking progress instead of inventing substitute architecture or silently widening scope.

## Current accepted slices

The 1.0 integrated game loop is documented in `docs/VALIDATION_1_0.md`. The user selected daily work/rest schedules for 1.1 in #200: feature #380 then acceptance gate #381, with evidence in `docs/VALIDATION_1.1.md`. After that gate merges, inspect #201 (1.2 Advanced Creature Behavior) and current Project #1 before selecting more work; it is a planning issue that needs a concrete minimum playable target. Deferred #302/#306/#369 retain their scope boundaries.

The user subsequently prioritized unfinished player menus before 1.2: #384 is a focused correction to completed 1.0 functionality. Its Inventory, Settlement and Save/Settings screens, button mapping, and deliberately disabled future placeholders are documented in `docs/PLAYER_MENUS.md`. Complete that correction before advancing #201; placeholders do not make future equipment/audio/display implementation Ready.

The next user-requested 1.0 correction is #388: gameplay target selection/cycling without opening menus, documented in `docs/WORLD_SELECTION.md`. It reuses accepted entity actions and resource projections, preserves existing input preferences, and leaves the #201 product-scope decision separate.

User playtesting then selected #390: targeted strike resolution, exposed humans overlapping storage, visible greeting/facing feedback, and tap-collect versus hold-use. `docs/GAMEPLAY_REFINEMENT.md` defines this focused correction; reuse existing combat, World inventory and reservation services. Health persistence #302 and the #201 future creature scope remain separate.

The user next requested #393 to repair the subsequently added InteractionResult/InteractionResolver capture, feed, pet and give paths and build diagnostics. `docs/INTERACTION_REPAIR.md` records current F feed/pet and G greeting behavior, truthful rejection, atomic transfers and sustained facing. Finish this repair before the pending health-persistence follow-up under #302; do not treat future #201 planning as Ready implementation.

After #393 merged, user playtesting selected #395: prevent follow orders from pushing the player across the map. `docs/FOLLOW_SPACING.md` records the 2 m stop / 3 m resume band and live movement boundary, validated with player physics enabled. Complete this focused 1.0 regression before the pending #302 health-persistence scope; #201 remains future planning.
