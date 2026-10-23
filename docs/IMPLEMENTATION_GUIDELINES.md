# Implementation Guidelines

These guidelines turn the project's architecture and AI-assisted workflow principles into checks that can be applied to issues, code reviews, and pull requests. They guide new work without requiring a rewrite of existing systems.

## 1. Keep entity behavior composable

- Reuse the existing entity, species, and simulation contracts before adding a new base class or framework.
- Add capabilities through the project's existing composition and data mechanisms. Do not create a separate giant class for each species or role.
- Use Godot scenes and nodes for composition where the current presentation layer needs them. Do not introduce a custom ECS or third-party framework unless profiling shows a concrete limit and a reviewed decision records the migration cost.
- A species may omit a capability. Systems must check that capability and return a clear, safe result instead of assuming every entity can perform every action.

## 2. Give each system clear ownership

- Name the authoritative owner of every piece of durable state before implementing a feature. For example, simulation services own gameplay outcomes; Godot nodes present those outcomes.
- Change owned state through the owning system's validated operation. Other systems must not directly mutate its internals.
- Connect systems through small domain operations, typed results, or narrowly scoped signals. Avoid feature code reaching into unrelated systems' private state or adding a global event bus without a concrete use case.
- Keep views, animations, collision presentation, and imported bone names out of save data and gameplay rules. Use stable semantic IDs and explicit adapters at asset boundaries.
- Persist durable outcomes only. Treat paths, reservations, animation state, and other reconstructible activity as transient unless the issue explicitly requires otherwise.

## 3. Keep content data-driven

- Store tunable species, item, faction, recipe, and behavior parameters in the repository's validated data definitions rather than duplicating them in code.
- Put algorithms and state transitions in reusable simulation systems; keep data definitions descriptive and free of hidden side effects.
- Validate identifiers, ranges, required fields, and unsupported values when data enters the simulation. Reject invalid input before changing world state.
- Use stable semantic identifiers for saved or cross-system references. Do not save array positions, node paths, imported bone names, or display labels as identity.
- Extend existing data contracts in a backward-compatible way when possible. If a save contract must change, define the schema version, migration, invalid-save behavior, and tests in the issue before implementation.

## 4. Deliver vertical slices

Plan work around a complete observable behavior rather than a horizontal layer of infrastructure. A small slice should connect the relevant data, domain operation, presentation or actor, and persistence only where required.

An implementation issue is ready when it states:

- **Outcome:** what a player or tester can observe when it works.
- **Starting state and trigger:** the inputs, actor, and world conditions for the behavior.
- **Success criteria:** specific state changes and visible or queryable evidence.
- **Failure criteria:** invalid, unsupported, blocked, or insufficient-resource behavior, including confirmation that failure makes no partial changes.
- **Dependencies:** existing systems and completed issues the work relies on.
- **Boundaries:** what this issue deliberately does not add.
- **Validation:** focused automated tests and any required Godot scene or manual scenario.
- **Persistence:** whether the outcome survives save/load and the expected migration behavior, if applicable.

If those points cannot be stated without guessing, keep the issue in Backlog and refine it before implementation. A milestone should leave behind a runnable, testable game slice, not only a collection of completed scripts.

## 5. Implement one reviewable change at a time

- Read repository instructions, the issue, relevant design documents, and the current interfaces before editing.
- Verify prerequisites in `main`; do not build a substitute architecture to work around a missing dependency.
- Use one issue branch and pull request per change. Keep unrelated cleanup, formatting, and bulk asset normalization out of the issue branch.
- Make the smallest complete change that meets acceptance criteria. Avoid broad refactors and future features unless the issue requires them.
- Record consequential decisions in `docs/DECISIONS.md`, including status, reason, consequences, and a concrete revisit trigger.
- Review the full diff before committing: changed files should map to the issue, generated files should use the prescribed tools, and no local secrets or debug scaffolding should remain.

## 6. Verify behavior at the right level

- Run the repository's prescribed build and test commands for every code change. Add focused tests for domain rules and failure paths where practical.
- Use deterministic inputs and random seeds for simulation tests. Assert outcomes and invariants rather than incidental implementation details.
- Test system boundaries with integration or Godot scene checks when a unit test cannot verify wiring, rendering, input, physics, or save integration.
- For a player-visible feature, record reproducible manual steps when automated coverage cannot observe the actual experience.
- Test save/load whenever an issue changes durable state. Include a migration or rejection check when the save schema changes.
- Report exactly which checks ran and their results. Do not treat a successful compile as proof that a gameplay scenario works.

## 7. Scale only from evidence

- Keep simulation state independent of visual instancing so distant or inactive entities can later use cheaper updates without changing gameplay identity.
- Separate high-frequency work such as physics and combat from slower needs, schedule, and economy updates when measured requirements call for it.
- Do not simulate, render, or navigate every entity at full detail by default when the existing near/far model can answer the gameplay need more cheaply.
- Before optimization, define a reproducible workload, measure it, and record the target and result. Optimize observed bottlenecks rather than adding speculative caching, threading, ECS, or world-streaming systems.

## 8. AI handoff and review

AI contributors must receive the issue URL, branch, relevant documents, dependencies, acceptance criteria, and exact validation commands or scenario. Their pull request summary must state:

- what observable behavior changed;
- which acceptance criteria were met;
- files changed and why they belong to the issue;
- tests and scenarios actually run;
- save or compatibility effects;
- known limitations and follow-up issues.

Architecture or save-contract changes require review of the system boundaries, migration behavior, and failure cases in addition to the normal build and tests. The project owner retains decisions about product behavior and unresolved architecture tradeoffs.
