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


