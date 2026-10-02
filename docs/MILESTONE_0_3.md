# Milestone 0.3: Creature Core

## Objective

Extend the existing data-defined creature foundation into a deterministic, inspectable creature simulation core. A repeatable scenario must show temperament affecting wild behavior and safely validate capture/captivity state without requiring player-facing capture controls.

This milestone is a domain-core step, not a complete player interaction loop. The Ant Farm already proves the shared hunger, navigation, eating, and wandering loop; Creature Core adds differentiated wild behavior and durable creature state that later player-facing work can use.

## Included

- Extend the existing `SpeciesCatalog` with a small, validated, data-defined temperament profile. Keep species identity, diet, movement, and capability data in the existing catalog rather than creating a second registry.
- Use temperament in deterministic wild-behavior choices on fixed simulation ticks. Reuse existing entity, navigation, and wandering ownership; expose enough state or results for a test scenario to explain the choice.
- Represent capture/captivity as authoritative simulation-domain state attached to the creature. Validate permitted transitions and reject invalid requests without partial mutation.
- Persist durable creature capture/captivity state in versioned world snapshots. Validate it before replacing a live world and test round-trip and failure atomicity.
- Add focused domain tests and a deterministic integrated scenario that demonstrates two different creature profiles, continued wild behavior, a valid capture transition, and recovery from invalid requests or snapshot data.

## Deferred

- Player controls, capture tools, capture UI, carrying/transport, cages or settlement housing, escape, and release gameplay.
- Taming, domestication, bonds, recruitment, and relationship changes.
- Combat, injury/death behavior changes, breeding/genetics, hunting, packs/herds, territory, migration, and predator-prey ecology.
- New engine/framework architecture, ECS, mod support, and presentation/art work.

## Acceptance scenario

1. Load the existing validated species catalog with the added temperament profiles. Invalid or incomplete profile data is rejected with a clear error; existing species data and Ant Farm behavior remain valid.
2. In a fixed-seed test world, spawn at least two creature species with different configured temperaments and advance a fixed number of simulation ticks. The same seed, inputs, and tick count produce the same decisions, while the chosen profiles produce the documented distinct wild-behavior outcomes.
3. Exercise the capture/captivity domain transition for one eligible live creature. A duplicate, stale, invalid, or otherwise ineligible request is explicitly rejected and leaves entity/world state unchanged. The other creature continues its wild behavior.
4. Save and restore while one creature is captive. The creature's identity, species, temperament source, and capture/captivity state remain valid; malformed state is rejected before the running world or transient services are reset.
5. Confirm existing hunger, eating, navigation, entity identity, and snapshot invariants still hold; record the exact commands, results, and known limitations in the milestone validation record.

All behavior is simulation-owned and deterministic under fixed inputs. A player command, cage, capture item, or live UI is not required for this scenario.

## Handoffs and dependencies

- **0.1 The Ant Farm:** reuse the species catalog, needs, navigation, wandering, save/load, and deterministic scenario foundation. Do not reopen the completed 0.1 scope.
- **0.2 Character Core:** consume shared entity/character data and health state; use its authoritative captivity state if available, and do not define a second creature model or parallel captivity flag.
- **0.4 Basic Settlement:** creature housing, cages, storage, and workstations remain out of scope.
- **0.5 Combat Prototype:** no new combat, damage, incapacitation, or death mechanics are required.
- **0.6 Relationships & Loyalty:** do not add trust/fear/bonds, domestication, or recruitment.
- **1.0 First Real Game:** player-driven creature interaction, capture controls, and captivity gameplay remain deferred to a player-facing milestone.
- **1.2 Advanced Creature Behavior:** hunting, packs, herds, and territory remain deferred.

## Traceability to milestone issues

| Issue | Task | Depends on |
| --- | --- | --- |
| [#324](https://github.com/jmart985/Unnamed-Project-2026/issues/324) (C03a) | Add validated temperament profiles to creature species | None |
| [#326](https://github.com/jmart985/Unnamed-Project-2026/issues/326) (C03c) | Drive deterministic wild behavior from temperament | #324 |
| [#323](https://github.com/jmart985/Unnamed-Project-2026/issues/323) (C03b) | Model durable creature capture and captivity state | None |
| [#325](https://github.com/jmart985/Unnamed-Project-2026/issues/325) (C03d) | Verify the integrated creature-core scenario | #323, #324, #326 |

Issue [#221](https://github.com/jmart985/Unnamed-Project-2026/issues/221) tracks the scope and milestone integration. Issue acceptance criteria are the task-level source of truth; this document defines the shared scenario and boundaries.

The milestone is complete when every linked task is accepted, the deterministic scenario passes, snapshot failure paths preserve the live world, and no unmet acceptance check lacks an explicit follow-up issue.
