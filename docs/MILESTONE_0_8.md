# Milestone 0.8: Economy Prototype

## Objective

Demonstrate one deterministic, inspectable settlement economy loop: finite local resources are produced through existing farming and workstation systems, offered through a settlement market, and exchanged with one finite-stock caravan. This is a simulation prototype, not a regional economy or a player trading career.

Reuse existing World ownership, settlement resources, work orders, recipes, atomic transfers, fixed simulation ticks, and snapshots. Do not add parallel inventory state or speculative infrastructure.

## Included

- A small validated catalog of tradeable resource IDs and fixed positive integer values denominated in one `coin` resource. Add `coin` to the existing known item IDs and reserve it as currency rather than an ordinary priced good. Values do not change with supply, demand, or transaction history.
- A settlement market that operates on authoritative resource containers and supports bounded exchange against available goods and currency.
- Atomic market transactions: every resource and currency leg succeeds together or none of the involved containers change. Reject invalid goods or quantities, insufficient stock or currency, and capacity failures with explicit results.
- One deterministic caravan visit with finite, known cargo and currency that completes at least one exchange at the settlement market. Treat the caravan as a bounded external trade participant; it does not require a regional route network or autonomous travel simulation.
- An integrated fixed-input scenario that uses existing farming and workstation recipe behavior to create or transform a finite tradeable good, performs a market exchange with the visiting caravan, and reports resulting resource and currency totals.
- Focused tests and scenario evidence for success, rejected trades, stable repeatability, and recovery without duplicated or lost resources. Durable settlement resource containers remain covered by existing snapshot behavior; an in-progress caravan trip is not persisted.

## Excluded

- Dynamic prices, supply/demand simulation, debt, taxes, wages, investment, and economic AI.
- Multiple settlements, regional markets, route networks, autonomous caravan scheduling, travel risk, escorts, raids, or combat.
- Player-facing trade controls, a trading career, polished market/caravan UI, and presentation or art expansion.
- Reimplementing farming, recipes, work orders, storage, inventory transfer, or save/load foundations already delivered in earlier milestones.
- Making economy depth a requirement of the 1.0 First Real Game loop.

## Acceptance scenario

1. Load valid fixed trade-good values for known resource IDs and reject malformed, duplicate, unknown, `coin`, non-positive, or otherwise invalid definitions with a clear error and no partially usable catalog.
2. In a fixed-input World, use the existing farm/recipe/workstation behavior to produce a known amount of one tradeable output from finite inputs. Verify exact input consumption and output production.
3. Place the resulting goods and a finite currency balance in authoritative settlement containers, then run one fixed-stock caravan visit. Complete at least one exchange at the configured fixed value and verify the exact goods and currency deltas on both sides.
4. Reject trades for invalid requests, unavailable goods/currency, and insufficient destination capacity. Every rejection leaves all participating containers unchanged; successful repeated requests cannot duplicate or overdraw resources.
5. Run the same scenario with the same inputs and operation order twice. Both runs produce identical transaction results and final resource totals.
6. Save and restore after the visit has ended. Confirm settlement goods and currency retain their totals. Reject invalid snapshot data without replacing the current World.
7. Record exact commands, scenario inputs, actual results, and known limitations in the integrated validation issue. Do not report an unrun test or Godot scenario as passing.

## Handoffs and dependencies

The integrated E08d test inputs, exact totals, rejection/recovery checks, executed
commands, and limits are recorded in [the 0.8 validation record](VALIDATION_0_8.md).

- **0.1 The Ant Farm:** reuse the World, fixed-tick, resource-container, atomic-transfer, and snapshot foundations. Do not reopen its completed acceptance scope.
- **0.2 Character Core, 0.3 Creature Core, 0.5 Combat Prototype, and 0.6 Relationships & Loyalty:** reuse only where applicable; no character, creature, combat, or relationship expansion is required for this economy slice.
- **0.4 Basic Settlement:** reuse completed storage/stockpiles, farming, workstation recipes, work orders, recipe execution, material transfer, and construction-site foundations (including #144, #150, #152, #154, #156, #158, and #160). Do not rebuild these systems.
- **1.0 First Real Game:** economy depth remains explicitly excluded from 1.0 acceptance. The 0.8 prototype may inform later scope but must not block or broaden 1.0.
- **2.1 Regional Economy:** multi-settlement trade, route networks, and regional resource flows remain future work.

## Traceability to milestone issues

| Issue | Task | Depends on |
| --- | --- | --- |
| [#330](https://github.com/jmart985/Unnamed-Project-2026/issues/330) (E08a) | Define validated fixed-value trade goods | None |
| [#331](https://github.com/jmart985/Unnamed-Project-2026/issues/331) (E08b) | Execute atomic settlement market transactions | #330; completed #4 and #144 |
| [#332](https://github.com/jmart985/Unnamed-Project-2026/issues/332) (E08c) | Complete one finite caravan market visit | #331 |
| [#333](https://github.com/jmart985/Unnamed-Project-2026/issues/333) (E08d) | Verify the integrated settlement economy scenario | #330, #331, #332; completed #150, #152, #154, and #156 |

Issue [#223](https://github.com/jmart985/Unnamed-Project-2026/issues/223) tracks milestone scope and backlog. Task issue acceptance criteria are the implementation source of truth; this document defines the shared scenario and boundaries.

The milestone is complete when all linked tasks are accepted, the integrated scenario passes its success and failure/recovery checks, and actual validation evidence is recorded with no unmet criterion lacking an explicit follow-up issue.
