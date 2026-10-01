# Milestone 0.5: Combat Prototype

## Objective

Prove a deterministic combat core that resolves melee intent into clear state changes for targeting, damage, cooldown, incapacitation, and death.

## Included

- Deterministic melee damage resolution against shared character/body-part health state.
- Cooldown gating that prevents repeated attacks until attacker recovery elapses.
- Reach validation that rejects attacks outside configured melee range.
- Combat target selection with deterministic defaulting when no explicit target is provided.
- Distinct incapacitation and death outcomes in combat results.
- Focused automated tests that cover success and rejection paths.

## Excluded

Animation fidelity, AI tactics, weapon variety, equipment modifiers, stamina costs, balancing, loot/removal flows, networking, and final UX are out of scope for this milestone.

## Acceptance checks

1. **Valid in-range attack flow (success path)**
   With two valid entities in range and attacker cooldown ready:
   - target body part is resolved deterministically;
   - bounded damage is applied to that part;
   - cooldown starts for the attacker;
   - result reports whether incapacitation and/or death occurred.

2. **Invalid request handling (failure path)**
   Invalid/missing entities, non-positive damage, invalid/non-finite reach, or invalid explicit targets are rejected with explicit results and **no defender mutation**.

3. **Out-of-range handling (failure path)**
   Attacks beyond configured reach return an out-of-range result and do not change defender health.

4. **Cooldown enforcement (failure path + recovery)**
   Repeated attacks are rejected while cooling down; advancing time eventually returns the attacker to ready state without negative cooldown values.

5. **Death-state behavior**
   Required body-part destruction transitions to death, and death remains monotonic without a revival system.

## Traceability to milestone issues

- [#162](https://github.com/jmart985/Unnamed-Project-2026/issues/162) deterministic melee damage resolution
- [#164](https://github.com/jmart985/Unnamed-Project-2026/issues/164) melee attack cooldowns
- [#166](https://github.com/jmart985/Unnamed-Project-2026/issues/166) melee reach validation
- [#168](https://github.com/jmart985/Unnamed-Project-2026/issues/168) reach integrated into combat resolution
- [#170](https://github.com/jmart985/Unnamed-Project-2026/issues/170) combat death state
- [#172](https://github.com/jmart985/Unnamed-Project-2026/issues/172) deterministic target selection

This milestone is complete when these checks are reproducible through tests and no unmet check lacks an explicit follow-up issue.
