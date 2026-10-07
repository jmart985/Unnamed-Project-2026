# Implementation backlog

The [GitHub issues](https://github.com/jmart985/Unnamed-Project-2026/issues) are the editable task specifications and source of truth for scope, acceptance criteria, and validation. This document is a starting index.

## Current status (2026-10-05)

Character Core issues #135, #136, #137, and #139, far-population arithmetic validation #369, actor-spacing movement protection #415, and Basic Settlement acceptance #406 are complete. Issues #302, #306, #201, #225, and #226 are planning or deferred scope and belong in **Backlog**, not Ready. The current 0.4 construction loop is complete; the broader build-mode expansion is documented separately in `docs/CONSTRUCTION_BUILD_MODE.md`. The next implementation item must be decomposed into a small, testable slice with explicit dependencies and validation steps.

- [Ant Farm Board](https://github.com/users/jmart985/projects/1)
- [Milestone 0.1: The Ant Farm](https://github.com/jmart985/Unnamed-Project-2026/milestone/1)
- [Milestone 1.0: First Real Game](https://github.com/jmart985/Unnamed-Project-2026/milestone/4)
- [Milestone acceptance scenario](MILESTONE_0_1.md)
- [Milestone 0.3: Creature Core](MILESTONE_0_3.md)
- [Milestone 0.7: Factions](MILESTONE_0_7.md)
- [Milestone 0.8: Economy Prototype](MILESTONE_0_8.md)
- [Milestone 0.9: Vertical Integration](MILESTONE_0_9.md)

## Historical 0.1 implementation sequence

This table records the original 0.1 sequence for historical reference and regression checks. It is not the current queue; use the active GitHub milestone and Ready implementation issues.

| Issue | Area | Task | Depends on |
| --- | --- | --- | --- |
| [#1](https://github.com/jmart985/Unnamed-Project-2026/issues/1) (F01) | Foundation | Create the Godot C# sandbox and build checks | None |
| [#2](https://github.com/jmart985/Unnamed-Project-2026/issues/2) (F02) | Foundation | Add a fixed simulation clock with pause and speed controls | [#1](https://github.com/jmart985/Unnamed-Project-2026/issues/1) |
| [#3](https://github.com/jmart985/Unnamed-Project-2026/issues/3) (E01) | Entity Simulation | Add stable entity IDs, spawn/remove, and state inspection | [#2](https://github.com/jmart985/Unnamed-Project-2026/issues/2) |
| [#4](https://github.com/jmart985/Unnamed-Project-2026/issues/4) (I01) | Inventory/Resources | Implement bounded containers and atomic resource transfers | [#3](https://github.com/jmart985/Unnamed-Project-2026/issues/3) |
| [#5](https://github.com/jmart985/Unnamed-Project-2026/issues/5) (N01) | Navigation | Move entities to targets and report arrival or failure | [#3](https://github.com/jmart985/Unnamed-Project-2026/issues/3) |
| [#6](https://github.com/jmart985/Unnamed-Project-2026/issues/6) (H01) | Needs/Hunger | Decrease satiety on simulation ticks and request food | [#3](https://github.com/jmart985/Unnamed-Project-2026/issues/3) |
| [#7](https://github.com/jmart985/Unnamed-Project-2026/issues/7) (J01) | Jobs | Add action priorities, resource reservations, and cancellation | [#4](https://github.com/jmart985/Unnamed-Project-2026/issues/4), [#6](https://github.com/jmart985/Unnamed-Project-2026/issues/6) |
| [#8](https://github.com/jmart985/Unnamed-Project-2026/issues/8) (H02) | Needs/Hunger | Complete the autonomous find-food, move, and eat loop | [#5](https://github.com/jmart985/Unnamed-Project-2026/issues/5), [#6](https://github.com/jmart985/Unnamed-Project-2026/issues/6), [#7](https://github.com/jmart985/Unnamed-Project-2026/issues/7) |
| [#9](https://github.com/jmart985/Unnamed-Project-2026/issues/9) (J02) | Jobs | Haul wood to storage and resume delivery after eating | [#8](https://github.com/jmart985/Unnamed-Project-2026/issues/8) |
| [#10](https://github.com/jmart985/Unnamed-Project-2026/issues/10) (C01) | Creatures | Add one data-defined creature species that wanders and eats | [#8](https://github.com/jmart985/Unnamed-Project-2026/issues/8) |
| [#11](https://github.com/jmart985/Unnamed-Project-2026/issues/11) (S01) | Save/Load | Save and restore durable world state with validation | [#9](https://github.com/jmart985/Unnamed-Project-2026/issues/9), [#10](https://github.com/jmart985/Unnamed-Project-2026/issues/10) |
| [#12](https://github.com/jmart985/Unnamed-Project-2026/issues/12) (F03) | Foundation | Verify and document the ten-minute Ant Farm acceptance scenario | [#11](https://github.com/jmart985/Unnamed-Project-2026/issues/11) |

## 1.0 First Real Game

The target is the [small player-controlled survival/settlement slice](MILESTONE_1_0.md), not the entire long-term game vision. The existing new-game/session issue is complete; the remaining 1.0 work is:

| Issue | Task | Depends on |
| --- | --- | --- |
| [#321](https://github.com/jmart985/Unnamed-Project-2026/issues/321) | Let the player eat from a nearby compatible food source | Completed [#319](https://github.com/jmart985/Unnamed-Project-2026/issues/319) and [#8](https://github.com/jmart985/Unnamed-Project-2026/issues/8) |
| [#312](https://github.com/jmart985/Unnamed-Project-2026/issues/312) | Add one human squad with move-to, follow-player, and stop orders | Completed [#319](https://github.com/jmart985/Unnamed-Project-2026/issues/319) and [#124](https://github.com/jmart985/Unnamed-Project-2026/issues/124) |
| [#313](https://github.com/jmart985/Unnamed-Project-2026/issues/313) | Add one friendly, capability-gated creature greeting | Completed [#319](https://github.com/jmart985/Unnamed-Project-2026/issues/319) and [#124](https://github.com/jmart985/Unnamed-Project-2026/issues/124); existing relationship rules |
| [#322](https://github.com/jmart985/Unnamed-Project-2026/issues/322) | Verify the integrated loop and document actual validation evidence | [#321](https://github.com/jmart985/Unnamed-Project-2026/issues/321), [#312](https://github.com/jmart985/Unnamed-Project-2026/issues/312), and [#313](https://github.com/jmart985/Unnamed-Project-2026/issues/313) |

Completed session bootstrap, existing food/needs, relationship state, World snapshots, and save-slot behavior are reused rather than reimplemented. Broader promises for construction, combat, factions, capture/taming, advanced creature behavior, and world streaming are explicitly deferred.

The issue audit found overlapping open proposals that are deliberately not separate 1.0 tasks: [#303](https://github.com/jmart985/Unnamed-Project-2026/issues/303) duplicates the selected squad scope in #312; [#297](https://github.com/jmart985/Unnamed-Project-2026/issues/297) and [#306](https://github.com/jmart985/Unnamed-Project-2026/issues/306) propose the broader capability work narrowed for #313; [#318](https://github.com/jmart985/Unnamed-Project-2026/issues/318) duplicates the completed new-session work in [#319](https://github.com/jmart985/Unnamed-Project-2026/issues/319). These remain outside the milestone and should be explicitly triaged before being moved to Ready; this planning pass does not silently close or rewrite them.

## Workflow

Ideas → Backlog → Ready → In Progress → Testing → Done.

Keep each implementation issue focused enough for one reviewable PR. Split work further if needed and preserve links to the parent task. Acceptance checkboxes are not complete until verified. See [CONTRIBUTING.md](../CONTRIBUTING.md) for the AI-assisted workflow.

## Survival vertical slice

The next framework test is documented in [SURVIVAL_VERTICAL_SLICE.md](SURVIVAL_VERTICAL_SLICE.md). Decompose it into independent implementation issues in this order: world tree/rock fixtures, bare-hand gathering, axe recipe/equip, axe harvesting and regrowth, storage delivery, open shelter interiors, bed/sleep, and fire/warmth/cooking. Do not remove the existing starter wood pile until Slice A and its replacement acceptance test are complete.

## Follow-on milestone scope

Issue [#221](https://github.com/jmart985/Unnamed-Project-2026/issues/221) defines the 0.3 Creature Core target and links its focused implementation issues. This work reuses the completed Ant Farm species catalog and wandering foundations; it does not reopen Milestone 0.1 or pull player-driven creature interactions forward from 1.0.

| Issue | Area | Task | Depends on |
| --- | --- | --- | --- |
| [#324](https://github.com/jmart985/Unnamed-Project-2026/issues/324) (C03a) | Creatures | Add validated temperament profiles to creature species | None |
| [#326](https://github.com/jmart985/Unnamed-Project-2026/issues/326) (C03c) | Creatures | Drive deterministic wild behavior from temperament | [#324](https://github.com/jmart985/Unnamed-Project-2026/issues/324) |
| [#323](https://github.com/jmart985/Unnamed-Project-2026/issues/323) (C03b) | Creatures | Model durable capture and captivity state | None |
| [#325](https://github.com/jmart985/Unnamed-Project-2026/issues/325) (C03d) | Validation | Verify the integrated creature-core scenario | [#323](https://github.com/jmart985/Unnamed-Project-2026/issues/323), [#324](https://github.com/jmart985/Unnamed-Project-2026/issues/324), [#326](https://github.com/jmart985/Unnamed-Project-2026/issues/326) |

## Roadmap boundaries

These topics belong to later milestones. They may be implemented when their concrete issues are active and their dependencies are complete; do not use this historical list to block current milestone work. Milestones beyond the currently accepted playable slices remain roadmap horizons, not a promise to ship every idea in the long-term vision. Decompose a later milestone into implementation work only after its objective is reconciled with completed evidence and its scope is made testable.

## 0.7 Factions

The accepted target is a small durable faction foundation in the existing World: fixed faction identities, optional entity allegiance, bounded explicit reputation, directed faction hostility, and optional ownership metadata on existing settlement containers. This does not add faction AI, diplomacy, laws/crime, territorial control, warfare, ownership enforcement, or a new faction framework. See [Milestone 0.7 scope and acceptance](MILESTONE_0_7.md) and planning issue [#222](https://github.com/jmart985/Unnamed-Project-2026/issues/222).

| Issue | Task | Depends on |
| --- | --- | --- |
| [#337](https://github.com/jmart985/Unnamed-Project-2026/issues/337) (F07a) | Add faction identities and durable allegiance | Accepted [#325](https://github.com/jmart985/Unnamed-Project-2026/issues/325); completed #3, #11, #138 |
| [#338](https://github.com/jmart985/Unnamed-Project-2026/issues/338) (F07b) | Track faction reputation and directed hostility | #337; accepted #325 |
| [#339](https://github.com/jmart985/Unnamed-Project-2026/issues/339) (F07c) | Add durable ownership metadata to settlement containers | #337; completed #144; accepted #325 |
| [#340](https://github.com/jmart985/Unnamed-Project-2026/issues/340) (F07d) | Verify the faction foundation and snapshot recovery | #337, #338, #339 |

## 0.8 Economy Prototype

The target is a deterministic, inspectable settlement economy loop: existing farming and workstation systems produce a finite tradeable good, a settlement market exchanges resources at fixed prototype values denominated in one `coin` currency, and one finite-stock caravan completes a visit. Reuse the Basic Settlement and World/inventory foundations; do not make economy depth a 1.0 commitment. See [Milestone 0.8 scope and acceptance](MILESTONE_0_8.md) and planning issue [#223](https://github.com/jmart985/Unnamed-Project-2026/issues/223).

| Issue | Task | Depends on |
| --- | --- | --- |
| [#330](https://github.com/jmart985/Unnamed-Project-2026/issues/330) (E08a) | Define validated fixed-value trade goods | None |
| [#331](https://github.com/jmart985/Unnamed-Project-2026/issues/331) (E08b) | Execute atomic settlement market transactions | [#330](https://github.com/jmart985/Unnamed-Project-2026/issues/330), completed [#4](https://github.com/jmart985/Unnamed-Project-2026/issues/4) and [#144](https://github.com/jmart985/Unnamed-Project-2026/issues/144) |
| [#332](https://github.com/jmart985/Unnamed-Project-2026/issues/332) (E08c) | Complete one finite caravan market visit | [#331](https://github.com/jmart985/Unnamed-Project-2026/issues/331) |
| [#333](https://github.com/jmart985/Unnamed-Project-2026/issues/333) (E08d) | Verify the integrated settlement economy scenario | [#330](https://github.com/jmart985/Unnamed-Project-2026/issues/330), [#331](https://github.com/jmart985/Unnamed-Project-2026/issues/331), [#332](https://github.com/jmart985/Unnamed-Project-2026/issues/332), and completed [#150](https://github.com/jmart985/Unnamed-Project-2026/issues/150), [#152](https://github.com/jmart985/Unnamed-Project-2026/issues/152), [#154](https://github.com/jmart985/Unnamed-Project-2026/issues/154), and [#156](https://github.com/jmart985/Unnamed-Project-2026/issues/156) |

## 0.9 Vertical Integration

The target is one repeatable integration scenario in one test region, composing accepted creature, combat, relationship, faction, settlement, and local economy foundations. Add only scenario harness and evidence; missing gameplay is tracked separately. No multi-settlement or regional simulation is committed here. See [Milestone 0.9 scope and acceptance](MILESTONE_0_9.md) and planning issue [#224](https://github.com/jmart985/Unnamed-Project-2026/issues/224).

| Issue | Task | Depends on |
| --- | --- | --- |
| [#341](https://github.com/jmart985/Unnamed-Project-2026/issues/341) (V09a) | Verify one-region faction and economy integration | Accepted #340, #333, #325, #184, and #197. See [validation evidence](VALIDATION_0_9.md). |

## Future backlog audit

Milestone 1.1 selected human NPC daily work/rest schedules in #200, implemented by #380 and accepted through the dependent #381 gate. See [1.1 validation](VALIDATION_1.1.md). Milestones 1.2 and later remain roadmap horizons: their planning issues do not yet specify implementation-level outcomes, rejection/recovery cases, or validation evidence. Do not mark those planning issues Ready or convert the entire vision into committed delivery. Scope the next slice using the accepted gameplay evidence.

The open, unassigned proposals below were checked while reconciling the later roadmap. Keep them out of Ready until their overlap and scope are resolved; this audit does not silently close or rewrite them:

| Existing issues | Current relationship | Triage |
| --- | --- | --- |
| [#301](https://github.com/jmart985/Unnamed-Project-2026/issues/301), [#310](https://github.com/jmart985/Unnamed-Project-2026/issues/310), [#318](https://github.com/jmart985/Unnamed-Project-2026/issues/318) | Duplicate the completed deterministic new-game/session work in [#319](https://github.com/jmart985/Unnamed-Project-2026/issues/319). | Do not start; close/redirect only through explicit duplicate triage. |
| [#300](https://github.com/jmart985/Unnamed-Project-2026/issues/300) | User-authorized narrow expansion of the completed capture/captivity foundation: add durable captured/captive/domesticated lifecycle transitions and release-to-wild; death remains the existing health state. See D033. | Implement only the entity-level lifecycle state machine; relationship-driven domestication, health persistence, capture mechanics, and player-facing behavior remain deferred. |
| [#309](https://github.com/jmart985/Unnamed-Project-2026/issues/309), [#314](https://github.com/jmart985/Unnamed-Project-2026/issues/314) | Broader future proposals that overlap the completed foundation and ask for additional lifecycle behavior. | Preserve as unassigned future proposals; reconcile scope before moving to Ready. |
| [#307](https://github.com/jmart985/Unnamed-Project-2026/issues/307), [#316](https://github.com/jmart985/Unnamed-Project-2026/issues/316) | Overlap temperament work in [#324](https://github.com/jmart985/Unnamed-Project-2026/issues/324), but list different dimensions and have no dependency ordering. | Do not run in parallel; reconcile any genuinely new traits during a future creature-behavior scope. |
| [#308](https://github.com/jmart985/Unnamed-Project-2026/issues/308), [#317](https://github.com/jmart985/Unnamed-Project-2026/issues/317) | Overlap the bounded deterministic behavior task [#326](https://github.com/jmart985/Unnamed-Project-2026/issues/326) while also proposing additional flee/pursue/return behavior. | Keep unassigned until 1.2 is concretely scoped; do not broaden 0.3. |
| [#302](https://github.com/jmart985/Unnamed-Project-2026/issues/302), [#311](https://github.com/jmart985/Unnamed-Project-2026/issues/311), [#315](https://github.com/jmart985/Unnamed-Project-2026/issues/315) | Repeated player-position persistence proposals overlap existing durable entity-position snapshots; some fields (such as facing) need an explicit product requirement before expansion. | Do not create duplicate save work; verify any claimed gap against current snapshots before assigning. |
| [#296](https://github.com/jmart985/Unnamed-Project-2026/issues/296), [#305](https://github.com/jmart985/Unnamed-Project-2026/issues/305) | Overlapping proposals for smell-range and tracking-capability perception queries; #296 implements the bounded read-only query foundation. | Keep track generation, aging, persistence, and behavior consumers deferred until a bounded 1.2 hunting/tracking behavior defines their requirements. |

- [Future milestone scaffold](ROADMAP_SCAFFOLD.md)
- [Milestone planning checklist](MILESTONE_PLANNING_CHECKLIST.md)

- [Design alignment and scope boundary](DESIGN_ALIGNMENT.md)

- [Construction build mode direction](CONSTRUCTION_BUILD_MODE.md)

- [Linked settlement storage](SETTLEMENT_STORAGE.md)

- [Character creation and body customization](CHARACTER_CREATION_AND_BODY.md)

## Movement safety follow-up

Entity movement must not trap the player or deadlock crowds. The scoped requirements and acceptance tests are in [ENTITY_SAFETY.md](ENTITY_SAFETY.md). This belongs with the navigation/movement foundation before larger squad and settlement automation work.
