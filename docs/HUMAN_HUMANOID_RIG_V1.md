# Human Humanoid Rig v1 Prototype

**Issue:** #441  
**Status:** Prototype Approved; source licensing is still unresolved.

## Purpose

The first human production slice adapts the reviewed elf/Frieren character into a neutral human prototype. It uses the source mesh as editable geometry while preserving the project's canonical Humanoid Rig v1 compatibility layer.

The original elf archive remains under **Reference Only** status. It is not copied into the runtime asset tree, and this derived asset must not be treated as ship-ready until the source license is verified.

## Outputs

- Editable source: `art_source/reference/generated_humanoids/human_elf_adapted_rig_v1.blend`
- Runtime prototype: `game/assets/humanoids/human/human_humanoid_rig_v1.glb`
- Metadata: `human_elf_adapted_rig_v1.json` and the runtime copy
- Preview: `human_elf_adapted_rig_v1_preview.png`
- Rebuild script: `art_source/blender/adapt_elf_human_rig_v1.py`

The script starts from the locally reviewed `elf.blend`, removes source costume and material dependencies, applies project-owned neutral materials, neutralizes the pointed-ear silhouette, removes cloth/soft-body modifiers, normalizes the mesh to 1.75 m, and exports GLB.

## Rig and animation

The output retains the required Humanoid Rig v1 bones, including:

- root, pelvis, spine_01/02/03, neck_01, head
- clavicle, upperarm, lowerarm, and hand chains
- toe bones
- three-segment fingers on both hands

The source action is renamed `human_idle`. Controller-driven locomotion remains separate from baked root motion.

## Runtime behavior

Human NPC `EntityView` instances try to load the prototype GLB. If the import is missing or invalid, they use the existing procedural human fallback. The player controller and player creation flow are unchanged.

## Male-presenting variant

Issue #483 adds a separate male-presenting variant using the realistic male
body from Blender Human Base Meshes v1.4.1. The selected source body is CC0 and
unrigged. The bundle's anatomical skeleton is a reference mesh, not a deform
armature, so the variant uses the established Humanoid Rig v1 and
`human_idle` animation. This is the shared compatibility rig, not a separate
male-specific skeleton; the existing human/neutral asset remains unchanged.

The Blender source body is normalized to 1.75 m and bound to the canonical rig
with automatic heat weights, then constrained to four normalized influences
per vertex. Palm weights are explicitly assigned against the canonical hand
bones. The male shirt, trousers, belt, and boots are separate skinned geometry.
The shirt and trousers use body-to-clothing clearances and Solidify thickness
recorded in
`art_source/reference/generated_humanoids/human_humanoid_clothing_fit_v1.json`;
the shirt profile is measured from the existing body/shirt reference meshes,
and the trouser profile is verified against 768 points on the user-reviewed
trousers. Both garments preserve their measured outside surface and add
thickness inward. The shirt varies from 3 to 10 mm so its inner surface remains
at least 1 mm off the body; the trouser shell adds 10 mm, leaving 4 mm under its
14 mm fit. Shirt buttons follow the unchanged outer surface. Semantic anchors
are checked in exported GLB world space.

The Blender body is reusable under CC0. The inherited rig and animation still
come from the elf-adapted reference, whose license has not been verified, so
the assembled male GLB remains prototype-only. Its source and provenance are
recorded in `art_source/reference/generated_humanoids/blender_male_base_source_v1.asset.json`
and `art_source/ASSET_REGISTRY.md`.

## Male variant outputs and validation

- Editable source: `art_source/reference/generated_humanoids/human_male_humanoid_rig_v1.blend`
- Runtime prototype: `game/assets/humanoids/human/human_male_humanoid_rig_v1.glb`
- Metadata: `game/assets/humanoids/human/human_male_humanoid_rig_v1.json`
- Rebuild script: `art_source/reference/generated_humanoids/build_human_male_variant.py`
- Previews: three-quarter, front, side, and back renders in `art_source/reference/generated_humanoids/`

The builder checks the 1.75 m body, required rig bones, hand weights, the
measured shirt and trouser clearance profiles, `human_idle`, all 15 anchor
nodes and their bone parents, and each exported anchor's composed world
position. The Blender source body and clothing meshes are editable; visual
deformation and locomotion still need review in Godot before the male variant
is selected for NPC spawning.

## Validation

1. Run the adaptation script with Blender 5.2 or a compatible Blender 4.x/5.x installation.
2. Confirm the JSON reports:
   - 1.75 m post height;
   - armature scale [1, 1, 1];
   - required and finger bones verified;
   - `human_idle`.
3. Open the GLB in Godot and confirm the model imports, the animation player exposes `human_idle`, and the model stands on the floor.
4. Start the sandbox and confirm human NPCs use the asset while creatures and the player retain their existing presentation.
5. If the GLB is unavailable, confirm a human NPC still spawns with the procedural fallback.

The asset remains a prototype until source licensing, deformation, equipment-socket alignment, performance, and final Godot import review are complete.


