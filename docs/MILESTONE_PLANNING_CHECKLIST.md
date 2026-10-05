# Milestone planning checklist

Use this checklist when opening the next milestone planning issue or converting a roadmap item into implementation work.

- [ ] State the smallest playable outcome in one sentence.
- [ ] Link the preceding milestone's acceptance evidence.
- [ ] List the exact systems and data contracts that already exist.
- [ ] Name dependencies and identify anything still in Backlog.
- [ ] Split the outcome into foundation, behavior, integration, and acceptance issues.
- [ ] Give each child issue bounded acceptance criteria and rejection cases.
- [ ] Define deterministic test data, persistence expectations, and reset behavior.
- [ ] Define manual Godot validation only where automated checks cannot observe the result.
- [ ] Keep deferred ideas in the parent issue instead of adding them to Ready.
- [ ] Move only the next independent child issue to Ready.

A milestone is not complete because its parent issue is closed. It is complete when its acceptance gate has evidence on the current main branch and the board reflects that evidence.
