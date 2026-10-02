# Contributing and AI-assisted work

1. Choose one ready issue and read its acceptance criteria, dependencies, and linked design docs.
2. Inspect the current implementation. Propose the smallest change that satisfies the issue; split the issue if it cannot be reviewed as one focused change.
3. Implement on a branch. Preserve documented ownership and interfaces. Record consequential design changes in docs/DECISIONS.md.
4. Run tests appropriate to changed behavior and the relevant Godot scenario. Do not claim unrun tests passed.
5. Open a PR linking the issue with `Closes #N`. Explain observable behavior, validation evidence, and known limitations. Review before merging.
6. Update docs when behavior changes. Close the issue only when its acceptance criteria are satisfied.

## Work states

Ideas → Backlog → Ready → In Progress → Testing → Done.

Use the GitHub Project status field when available. Ready means dependencies are complete and acceptance criteria are clear. Testing means implementation awaits verification/review. Done means accepted and merged. The active milestone and current issue list change over time; do not assume F01 or Milestone 0.1 is still the next task. See docs/AI_WORKFLOW.md.

## AI task handoff

Provide the issue URL, current branch, relevant docs, and expected validation command/scenario. Require a summary of changed behavior, tests actually run, and remaining limitations. The issue and repository are the durable context; chat history is supplemental.

