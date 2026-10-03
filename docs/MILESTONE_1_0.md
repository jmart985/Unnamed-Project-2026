# Milestone 1.0: First Real Game

## Objective

Deliver a small, coherent player-controlled survival/settlement loop on one fixed map. The player can move, meet their own hunger needs, direct a small human squad, and perform one friendly capability-gated interaction with a creature. The existing camp, finite resources, stockpile, World state, and save/load form the settlement foundation; construction is not required.

This is a playable vertical slice, not a promise to implement the entire long-term vision. Godot 4 .NET + C# remains provisional.

## Included

- A deterministic new session using the existing fixed map, player profile, 10 human NPCs, one ape, one wolf, one cow, species-compatible starter food sources, wood, and a stockpile.
- Direct player movement, inspection, and an explicit action to eat compatible food from a nearby source. Player hunger, consumed food, and resource counts use the authoritative World state.
- One human squad with membership plus move-to, follow-player, and stop orders.
- One friendly creature greeting, invoked with the dedicated Interact action (`G`) for the selected target within 2 world units and permitted by an explicit species capability. A successful greeting applies the validated `greeted` relationship profile (+5 familiarity, zero other deltas) only to the directed player-to-creature relationship; an unsupported or invalid interaction changes nothing.
- Paused save/load for the durable state exercised by this loop, including player position and needs, squad membership, relationship values, simulation time, and resource quantities.
- Basic visible success, waiting, and rejection feedback; deterministic domain tests and a reproducible Godot integration scenario.

## Excluded

Settlement construction, combat, injuries/death, faction play, capture, captivity, taming, recruitment, breeding, advanced creature behavior, autonomous social simulation, crafting, economy depth, procedural maps, world streaming, multiplayer, polished art/audio, and mod support.

Existing systems may be reused where they satisfy the included behavior. Do not add framework or infrastructure work without a direct acceptance requirement.

## Acceptance scenario

1. Start a new session with a nonblank player profile and a known seed. Confirm the existing setup contains 10 human NPCs, one ape, one wolf, one cow, one separately identified human player, finite human and species-compatible creature food, finite wood, and one stockpile.
2. Move and inspect the player. Advance simulation ticks and verify the player's satiety changes in the World-owned record, not a presentation-only copy.
3. Move within the documented range of a compatible food source and request one eating action. Exactly one food unit is consumed and the configured nutrition is applied once, clamped to the existing satiety range. Out-of-range, empty-source, and incompatible-food attempts report rejection and change neither satiety nor resources.
4. Add at least two human NPCs to the single squad. Issue move-to, follow-player, and stop orders. Verify membership does not duplicate, positions and order results remain authoritative, and invalid or removed targets cannot leave partial membership or stale orders.
5. Select a creature whose species profile permits the interaction and press `G` within 2 world units. Verify the directed player-to-creature familiarity increases by 5 and the reverse relationship is unchanged. Unsupported, out-of-range, removed, self, and invalid targets produce visible rejection without relationship mutation.
6. Pause and save, reset the presentation/session, and load. Verify simulation time, player identity/profile/position/satiety, squad membership, directed relationship values, and resource totals match the saved state. Invalid snapshots leave the current World unchanged.
7. Run the documented automated tests and Godot integration check. Record exact engine/SDK versions, commands, scenario inputs, observed results, and known limits in `docs/VALIDATION_1_0.md`. No unrun check is described as passing.

## Traceability and dependency order

| Issue | Work | Depends on |
| --- | --- | --- |
| [#319](https://github.com/jmart985/Unnamed-Project-2026/issues/319) (completed) | Deterministic new-game/session foundation | Existing simulation foundation |
| [#321](https://github.com/jmart985/Unnamed-Project-2026/issues/321) | Player-directed eating from nearby food | [#319](https://github.com/jmart985/Unnamed-Project-2026/issues/319) (completed); [#8](https://github.com/jmart985/Unnamed-Project-2026/issues/8) (completed) |
| [#312](https://github.com/jmart985/Unnamed-Project-2026/issues/312) | Human squad membership and movement orders | [#319](https://github.com/jmart985/Unnamed-Project-2026/issues/319) and [#124](https://github.com/jmart985/Unnamed-Project-2026/issues/124) (completed) |
| [#313](https://github.com/jmart985/Unnamed-Project-2026/issues/313) | One capability-gated friendly creature greeting | [#319](https://github.com/jmart985/Unnamed-Project-2026/issues/319) and [#124](https://github.com/jmart985/Unnamed-Project-2026/issues/124) (completed); existing directed relationship rules |
| [#322](https://github.com/jmart985/Unnamed-Project-2026/issues/322) | Integrated deterministic acceptance and validation evidence | [#321](https://github.com/jmart985/Unnamed-Project-2026/issues/321), [#312](https://github.com/jmart985/Unnamed-Project-2026/issues/312), and [#313](https://github.com/jmart985/Unnamed-Project-2026/issues/313) |

The three feature tasks are implemented and merged. The integration scenario is the final gate; see [the recorded 1.0 validation](VALIDATION_1_0.md) for deterministic assertions, actual engine outcomes, and the player-food wiring recovery. See [the backlog index](BACKLOG.md) for the current issue sequence.
