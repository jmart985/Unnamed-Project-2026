# Elf Swordmaster Humanoid Rig v1 Prototype

## Status and intended use

This is a separate fantasy elf visual prototype adapted from the user-provided
`ElfSwordmaster_Rigged.blend` source. It uses the project's Humanoid Rig v1 and
can be imported by Godot. It does not replace the existing human NPC, register
itself as a runtime species, or spawn in the current settlement scene.

The asset is marked **Prototype Approved**. The owner attests that the model
was purchased for commercial use, but complete receipts are not available.
Keep it out of public or commercial builds until the license evidence has been
recorded in the asset registry. The unmodified source file is preserved outside
the repository; its SHA-256 is recorded in the runtime metadata.

The source character has revealing fantasy armor and high-heeled boots. This
prototype preserves that source appearance and is not a neutral base body or a
replacement for the current human character.

## Files

- Editable adapted Blender scene: `art_source/reference/generated_humanoids/elf_swordmaster_humanoid_rig_v1.blend`
- Godot runtime interchange: `game/assets/humanoids/elf/elf_swordmaster_humanoid_rig_v1.glb`
- Asset metadata: `game/assets/humanoids/elf/elf_swordmaster_humanoid_rig_v1.json`
- Four-angle previews: `art_source/reference/generated_humanoids/elf_swordmaster_humanoid_rig_v1_previews/`
- Rebuild script: `art_source/blender/adapt_elf_swordmaster_humanoid_rig_v1.py`

## Adaptation and validation

The rebuild script preserves the source file and maps the character onto the
project's canonical Humanoid Rig v1 semantic hierarchy. It normalizes the
character to approximately 1.78 m, remaps the skin weights, keeps optional elf
ear and breast bones, and adds 14 semantic anchors. Source control helpers,
alternate exposed meshes, and the attached weapon are not part of the runtime
character scene; weapons remain separate equipment.

Automated Blender checks verify the required bones, mesh bounds, floor contact,
maximum of four skin influences, body weights for both arm and leg chains, and
world-space placement of exported anchors. A deformation probe rotates each
upper arm, forearm, hand, thigh, calf, foot, and toe independently. It disables
the body's armor-coverage mask modifiers during the probe and restores them
afterward.

The asset has 71 bones, 25 mesh objects, 33,787 source mesh vertices, 60,353
triangles, and 14 semantic anchors. The source carries no transferred animation
clips. Facial and hair helper weights are simplified; no secondary hair motion
or facial expression is supplied.

Godot 4.7.2 headless asset import completed successfully. The GLB is available
to the engine as an asset, but NPC presentation selection and a playable elf
species are separate gameplay work.

## Rebuild

The original source stays outside Git. Place the owner-provided source file at
`../ElfSwordmaster_Rigged.blend` relative to the repository, or pass its path
explicitly:

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" `
  --background --disable-autoexec `
  --python art_source/blender/adapt_elf_swordmaster_humanoid_rig_v1.py -- `
  --source "C:\path\to\ElfSwordmaster_Rigged.blend"
```

The default rebuild also writes front, three-quarter, side, and back preview
renders. For a fast structural rebuild, add `--skip-previews`.
