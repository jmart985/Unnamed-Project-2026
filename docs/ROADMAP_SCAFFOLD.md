# Roadmap milestone scaffolding

This document gives every future milestone a durable planning shell without pretending that the work is implementation-ready. The active board and issue acceptance criteria remain authoritative. A milestone may move work to **Ready** only after its objective is converted into small, independently testable issues.

## How to use this scaffold

For each milestone, create issues in this order:

1. **Scope decision** — define the smallest playable outcome and explicitly defer the rest.
2. **Foundation** — add or extend the data model and validation rules.
3. **System behavior** — implement one deterministic behavior with failure handling.
4. **Integration** — connect it to the existing simulation without a parallel architecture.
5. **Acceptance gate** — verify the scenario, persistence, reset behavior, and build evidence.

Every issue should name its dependencies, changed files or systems, rejection cases, tests, and manual validation. Parent milestone issues stay in Backlog; only the concrete child issue being implemented belongs in Ready/In Progress/Testing.

## Foundation Era follow-up milestones

### 0.2 Character Core
- **Goal:** reusable character stats, body parts, skills, injuries, and needs shared by humans and creatures.
- **Entry gate:** Ant Farm entity, inventory, needs, jobs, navigation, and save/load contracts are stable.
- **Exit gate:** one deterministic character scenario proves bounded stats, localized damage/recovery, skill progression, spawn/reset integration, and tests.
- **Deferred:** full combat, professions, equipment trees, and presentation polish.

### 0.4 Basic Settlement
- **Goal:** one usable settlement loop connecting storage, workbench, farm, construction, beds, and rest.
- **Entry gate:** creature and character data are available to spawned entities.
- **Exit gate:** a player can gather, produce, build, sleep, save, load, reset, and recover from invalid actions in one test settlement.
- **Deferred:** settlement economies, faction ownership, raids, and multi-settlement simulation.

### 0.6 Relationships & Loyalty
- **Goal:** bounded trust, fear, bonds, loyalty, and reversible relationship changes.
- **Entry gate:** character/creature identities and interaction results are durable.
- **Exit gate:** one scenario proves directed relationship changes, threshold transitions, persistence, and rejection of invalid actors.
- **Deferred:** full social memory, politics, recruitment careers, and moral systems.

## First Real Game depth milestones

### 1.1 Job Intelligence
- **Goal:** priorities, schedules, interruptions, and conflict resolution for one settlement.
- **Exit gate:** workers choose valid jobs, satisfy urgent needs, resume work, and produce deterministic evidence.

### 1.2 Advanced Creature Behavior
- **Goal:** bounded hunting, fleeing, pack/herd, territory, and tracking behavior.
- **Exit gate:** one predator/prey scenario demonstrates behavior transitions and recovery without requiring a full ecosystem.

### 1.3 Captivity Systems
- **Goal:** explicit captive, prisoner, recruited, enslaved, bonded, and domesticated state transitions.
- **Exit gate:** transitions validate authority, treatment, release, escape, and persistence.

### 1.4 Breeding & Genetics
- **Goal:** deterministic inheritance of a small approved trait set.
- **Exit gate:** two parents produce validated offspring, malformed inputs are rejected, and saved lineage round-trips.

### 1.5 Settlement Defense
- **Goal:** walls, gates, patrols, and one raid-response scenario.
- **Exit gate:** defenders detect, reach, and resolve an attack while settlement state persists.

### 1.6 Crafting & Industry
- **Goal:** finite multi-step production chains using existing inventory and jobs.
- **Exit gate:** inputs reserve atomically, production completes or rolls back, and outputs are inspectable.

### 1.7 Social Simulation
- **Goal:** memories, relationships, morale, and one bounded dispute/recovery loop.
- **Exit gate:** social events are directed, bounded, persisted, and do not bypass existing needs.

### 1.8 Faction Politics
- **Goal:** diplomacy, demands, alliances, and one conflict-resolution path.
- **Exit gate:** faction decisions use durable reputation/hostility and produce inspectable outcomes.

### 1.9 Systems Polish
- **Goal:** reliability, balance, performance, and simulation cleanup for the 1.x slice.
- **Exit gate:** repeatable scenario runs meet documented budgets with no known acceptance regressions.

## Living Region and Open World milestones

### 2.0 Living Region
- **Goal:** multiple settlements, regional trade, migration, conflict, and meaningful unattended change.
- **Exit gate:** leave/return validation shows durable regional change using detailed and statistical simulation.

### 2.1 Regional Economy · 2.2 Migration & Ecology · 2.3 War & Territory
- **Goal:** each expands one world-layer driver while reusing the existing region state.
- **Exit gate:** one causal scenario per milestone, with bounded update rules and persistence.

### 2.4 Dynamic Settlements · 2.5 Law, Crime & Reputation · 2.6 Trade Routes & Logistics
- **Goal:** settlements, law, and trade respond to physical resources and faction rules.
- **Exit gate:** one failure/recovery scenario per system; no global-resource shortcuts.

### 2.7 World Events · 2.8 Distant Simulation / LOD · 2.9 World Stability & Performance
- **Goal:** event generation, near/far expansion, and stable runtime budgets.
- **Exit gate:** deterministic event seeds, near/far reconciliation, and measured performance evidence.

### 3.0 Open World and 3.1–3.9 Identity Era
- **Goal:** several regions with cultures, biomes, ecosystems, economies, beliefs, settlements, warfare, and exploration.
- **Exit gate:** one cross-region journey changes the world and returns consistent state; each 3.x milestone must first receive a concrete minimum slice.

## Emergent Story, Freedom, and Release milestones

### 4.0 Emergent Story Simulator and 4.1–4.9 Player Freedom
- **Goal:** durable memories, careers, rivalries, and viable player lifestyles emerging from systems.
- **Exit gate:** a small set of replayable scenarios creates distinct outcomes without scripted quest resolution.

### 5.0 Feature Complete and 5.1–5.9 Alpha
- **Goal:** promised core systems exist, then receive UI, AI, save, performance, balance, accessibility, and stabilization passes.
- **Exit gate:** feature inventory is closed, known gaps are tracked, and repeatable alpha scenarios pass.

### 6.0 Closed Alpha and 6.1–6.9 Alpha Iteration
- **Goal:** external testing exposes crashes, corruption, onboarding failures, exploits, and performance problems.
- **Exit gate:** triaged reports have reproducible tests or explicit dispositions; no unresolved ship-blocking corruption remains.

### 7.0 Beta and 7.1–7.9 Beta Polish
- **Goal:** content-complete play with controller support, UX, tutorials, audio, balance, accessibility, and localization preparation.
- **Exit gate:** representative players can complete the core loop without developer intervention.

### 8.0 Release Candidate and 8.1–8.9 Release Hardening
- **Goal:** build verification, save migration, packaging, store preparation, and ship-stopping bug fixes.
- **Exit gate:** signed candidate builds, migration checks, and platform smoke tests pass.

### 9.0 Public Launch Candidate and 9.1–9.9 Feedback Cycle
- **Goal:** stable public build with measured community feedback and controlled follow-up changes.
- **Exit gate:** release criteria, support process, and regression coverage are explicit.

### 10.0 Full Release
- **Goal:** ship the initial coherent vision with documented known limitations.
- **Exit gate:** release approval, final migration, packaging, accessibility, and support documentation are complete.

## Readiness rule

Do not create a large batch of speculative child issues for a future milestone. When the current milestone has no independent Ready issue, select the next milestone objective, write its scope decision, and create only the smallest next implementation slice. This keeps AI-assisted work reviewable and prevents the roadmap from silently becoming a commitment.
