# External Asset Intake — 2026-10-25

## Scope

This review covers files dated October 25, 2026 in `C:\Users\James Home\Repo` and its newly added `Animations` and Tripo bridge folders. That directory is the parent of the Git checkout, not the project itself. The originals remain there, unchanged. Existing files under `Models/` have older timestamps and are outside this intake.

The goal is to identify reusable project material without importing unrelated mods or allowing external rigs to define project architecture. A file is not approved for project use because it is locally available or described as “game ready.”

## Disposition

| Group | New files | Intake result | Project action |
| --- | --- | --- | --- |
| Elf Swordmaster humanoid | `ElfSwordmaster_Rigged.blend`; `ElfSwordmaster_UE4Project_v4.25.rar`; `RyanReos_ElfSwordmaster.rar` | **Reference Only; adaptation candidate pending license evidence.** The author’s included readme describes a 178 cm model with heels, three body variants, PBR textures, and a full-body/facial rig. Read-only Blender inspection found 264 objects, a 710-bone rig, a 159-bone metarig, and four invalid driver-layer warnings. Its source hierarchy is not Humanoid Rig v1. The readme does not record the license tier acquired for these files. | Do not copy or export yet. If rights are confirmed, retarget/clean the asset to Humanoid Rig v1, remove unusable source controls, normalize scale/orientation, and validate sockets, deformation, and Godot import. It is a female elf, so it is not a replacement for the project male model. |
| Other Ryan Reos models | `RyanReos_BladedancerMercenary.rar`; `RyanReos_GrisSwimsuit.rar`; `RyanReos_Radagon.zip`; `RyanReos_DeluxeBunnysuit.zip`; `RyanReos_BunnyAkali_FullArchive.zip`; `RyanReos_BunnyAkali_Gallery.zip`; `RyanReos_BunnyAkali_UE5.zip`; `RyanReos_BunnyAkali_ZTL.zip` | **Reference Only; license/IP review required.** These contain Blender/FBX/Unreal or ZBrush material, but are not project-ready by extension alone. The local Bunny Akali readme identifies it as League of Legends fan art; Radagon is also a recognizable third-party character. | No conversion or runtime use until source license evidence and underlying character rights are established. Keep fan-art packs out of the shipped game. |
| Sims 4 content and animations | `BTTB_6_Base_by_BANK42n_Update_20231122.zip`; `Mimi Moan Pack 1.zip`; `_Noir__Smooth_Vagina_-_2025-09-09.zip`; `Beebavel_WickedWhimsAnimation.package`; `EachUisge_FelineSkinsFA_*.package`; `MYOBI_nippleslider.package`; `SMSims_*`; `Tibo131_*`; `Animations/Animations_List.zip`; `Animations/OLL_Animations_AP_v47.zip`; `Animations/WW_*` | **Not a Godot asset source.** These are Sims 4 `.package` content or archives containing Sims packages and mod-specific animation data. The `_Noir__` archive’s readme restricts use to personal use and says not to convert it to another game/software without permission. | Keep outside the project. Do not extract, convert, redistribute, or use the embedded mod content as game assets. |
| Tripo authoring bridges | `Tripo3d_Blender_Bridge-latest.zip` and extracted `Tripo3d_Blender_Bridge-latest/`; `Tripo3d_Godot_Bridge-latest.zip` | **Authoring tools, not game assets.** The Blender bridge is available upstream under MIT, but the downloaded archive’s version/provenance has not been verified. The Godot bridge archive has no license statement in its included README; its source includes a WebSocket bridge and import path. | Do not install/vendor either bridge in this change. The Blender add-on needs archive provenance/hash verification. The Godot bridge needs license review and a focused network/security review before enabling it in the project editor. No remote generation or paid provider calls were made. |
| Visual reference GIFs | `Pink Drinking GIF by Pudgy Penguins.gif`; `Water Refreshing GIF by xponentialdesign.gif` | **Reference Only.** These are short visual references, not rigged animation assets. | Keep outside runtime assets. If used later, record the source and license before creating derived reference material. |

## What was and was not done

- Inspected filenames, archive listings, included readmes, and the Elf Swordmaster Blender scene without saving it.
- Did not move, rename, extract, rewrite, or copy downloaded files.
- Did not import third-party assets into `game/` or modify any existing model.
- Did not convert an asset. The required `game-dev` CLI is unavailable in the current environment, and the most promising model lacks evidence of the acquired license tier. The asset-production workflow requires a verified toolchain, a confirmed license, and explicit approval of the exact source and output paths before a normalization/export command is run.

## Sorting and conversion path

For any asset that passes license review, keep the original archive outside the runtime tree and create a project-owned working copy under `art_source/reference/<asset-id>/`. Record the source URL, creator, license evidence, SHA-256, source units, transformations, canonical rig mapping, validation report, and final output paths. Only move a normalized GLB into `game/assets/` after its license is cleared and the canonical-rig, materials, scale, animation, and Godot checks pass.

For this intake the only plausible adaptation candidate is the Elf Swordmaster model, subject to proof of its license tier and explicit approval of an exact conversion invocation. The other model packs are references pending rights checks; the Sims content is incompatible; and the Tripo archives are editor tools requiring separate review.

## External source notes

- [Elf Swordmaster product page](https://ryanreos.artstation.com/store/bO26/elf-swordmaster-game-ready) — license tiers and asset description; the user’s acquired tier still needs verification.
- [Bunny Akali product page](https://ryanreos.artstation.com/store/BLvbD/bunny-akali-game-ready) — product page for a model whose included readme identifies it as fan art.
- [Radagon product page](https://ryanreos.artstation.com/store/1W91v/blender-model-radagon-of-the-golden-order) — product page for the Elden Ring character fan-art model.
- [Tripo Blender bridge source](https://github.com/Tripo-Developer/tripo-3d-for-blender) — upstream licensing/provenance reference; not proof that the downloaded archive is identical to upstream.
- [Tripo Godot bridge guide](https://www.tripo3d.ai/blog/tripo-dcc-bridge-for-godot) — editor integration guide; not a substitute for reviewing the included source or license.
