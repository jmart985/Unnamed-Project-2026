# AI contributor workflow

This document defines how AI-assisted work should move the project forward.

## Choose current work

1. Read the repository instructions, the active milestone, the parent issue, and the complete assigned issue.
2. Select the smallest independent issue marked Ready (or clearly available for implementation).
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

When no independent Ready issue remains, report the dependency or decision that is blocking progress instead of inventing substitute architecture or silently widening scope.
