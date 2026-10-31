# Codex Model Creation Directions: Recovered Reference

> **Provenance:** Reconstructed on 2026-10-31 from the repository's specifications, issues and earlier project discussions. These are **not authenticated verbatim copies** of the user's four original Codex messages. Preserve original prompts separately if recovered.

## Direction Set 1: Source assets and model intake
- Use the supplied Blender human, elf, creature and wearable meshes as actual source geometry, preserving original detail rather than replacing them with approximate procedural stand-ins.
- Record source names, license, provenance and reference-only/production status. Never assume downloaded assets are redistributable.
- Preserve original editable Blender source; make source-to-GLB conversions reproducible, validate round trips, and log per-asset failures.
- Keep authoring in `art_source`, runtime-approved exports in `game/assets`. Clothing, accessories and hair remain separate from base bodies.
- See `art_source/ASSET_REGISTRY.md`, `docs/HUMAN_HUMANOID_RIG_V1.md`, issues #540 and #542.

## Direction Set 2: Humanoid body mesh and canonical rig
- Establish/reuse the canonical Humanoid Rig v1, semantic bone names, finger and toe chains, retarget mappings and animation conventions.
- Preserve real male/female model shapes, use consistent Godot scale/orientation, and attach/skinning compatible weights to the shared rig when appropriate.
- Validate mesh deformation, material fidelity, body-to-garment clearances, armature transforms, animation, GLB export, Godot import and anchor positioning.
- Keep different appearances and anatomy as separate compatible geometry instead of baking everything into one mesh.
- See `docs/HUMAN_HUMANOID_RIG_V1.md`, issues #441, #522, #542.

## Direction Set 3: Creature rig families and semantic anatomy anchors
- Use anatomically correct rig families for canids, vulpines, felids, ruminants, suiforms, equines, primates, reptiles, etc., rather than stretching one generic animal skeleton.
- Use data-driven species definitions for proportions, diet, senses, gaits, collision, capabilities, behavior and animation.
- Expose actual supported semantic locomotion, equipment, feeding, medical, social, reproductive and nursing anchors for each anatomy variant. Every applicable orifice/interaction region needs its own correctly placed attachment target; an unsupported region must be reported instead of silently approximated.
- Gameplay references canonical IDs, not arbitrary source bone/node names. Declared anchors must be tested against actual geometry.
- See `docs/CREATURE_RIG_FAMILIES.md`, `docs/INTERACTION_ANCHOR_STANDARD.md`, issue #430.

## Direction Set 4: Player character creation and modular appearances
- Make the original realistic male and female Blender bases selectable in character creation, preview and gameplay.
- Support modular, body-compatible hair, garments, accessories and separately rigged optional anatomical attachments. Optional anatomy is hidden by default, user-toggleable and saved; covering clothing must avoid clipping.
- Reuse and retarget supported motions and expressions across rigs.
- Persist appearance/equipment selections, reject incompatible asset choices, safely migrate old saves, and validate locomotion, rendering and round trips.
- Rigging and anchor creation do not themselves implement adult interactions or breeding systems.
- See issues #522, #540 and #542.

## How Codex should use this
1. Read all four direction sets and their canonical references before work on any mesh, rig, model, animation or character creator.
2. Inspect existing accepted code and assets. Do not overwrite a higher-fidelity source with a placeholder.
3. Preserve licensing boundaries, data-driven semantic IDs, animation compatibility and existing saves.
4. Make small testable changes; run relevant automated and Godot import/preview checks and report unverified behavior honestly.
5. If the user's four original messages become available, store each verbatim in `docs/model-creation/original-prompts/` with source dates, then reference them from this file. Do not mislabel reconstructed text as an original quote.
