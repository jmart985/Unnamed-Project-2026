# Model files and local import tools

Keep editable Blender sources under `art_source/` and the GLBs used by Godot
under `game/assets/`. Export directly to the runtime destination rather than
keeping a second identical GLB next to its Blender source.

| Asset | Editable source | Runtime export |
| --- | --- | --- |
| Player bodies and controls | `art_source/characters/` | `game/assets/humanoids/player/` |
| Generated creatures | `art_source/reference/generated_creatures/*.blend` | `game/assets/creatures/<species>/` |
| Imported NPC originals | External library: `Assets/Models/hum/src/<character>/` | `game/assets/humanoids/human/npc/` |
| Clothing and appearance | `art_source/characters/`, `art_source/reference/clothing/`, external `Assets/Models/cloth/src/` | `game/assets/items/clothing/`, `game/assets/appearance/` |
| Source anatomy and base bodies | `art_source/reference/blender_base/`, `art_source/reference/player_bases/`, external `Assets/Models/ref/` | Only when a runtime consumer needs them |

The six creature generators export one GLB directly to their runtime folders.
Their Blender sources, previews and metadata remain available for editing.
The superseded `human_male_humanoid_rig_v1` output is retired. Some other older
models are still used by fallback presentation or garment conversion and must
remain until those consumers migrate; age alone is not proof of abandonment.

Godot's **Blender Path** and **FBX2glTF Path** are user-level editor settings,
under **Filesystem > Import**, and point to the actual executables. Verify each
executable with `--version`, set both paths, then restart the Project Manager
or editor. Machine-specific absolute paths do not belong in `project.godot`.
Keep Godot's default ufbx importer for ordinary FBX imports; FBX2glTF remains
available for assets that require the legacy converter.

Godot `.import` sidecars for live assets preserve import options and resource
identities. They are not duplicate meshes and must not be removed just because
the matching GLB is present. Remove an orphaned sidecar only after its source
asset has actually been retired. Generated `.godot/imported/` cache data is
separate from source assets.

Before deleting a duplicate, compare its SHA-256 hash with the retained file.
Move source packages with their textures; preserve compatibility paths when
existing Blender scenes refer to them. Keep local cleanup manifests in the
external library's `Assets/References/Docs/` directory. Preserve user edits
and independent Git worktrees rather than treating checkout copies as imports.
