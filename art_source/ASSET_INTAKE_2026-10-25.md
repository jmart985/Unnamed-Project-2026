# External Asset Intake — 2026-10-25

## Scope

This review covers files dated October 25, 2026 in `C:\Users\James Home\Repo` and its newly added `Animations` and Tripo bridge folders. That directory is the parent of the Git checkout, not the project itself. The originals remain there, unchanged. Existing files under `Models/` have older timestamps and are outside this intake.

The goal is to identify reusable project material without importing unrelated mods or allowing external rigs to define project architecture. A file is not approved for project use because it is locally available or described as “game ready.”

## Disposition

| Group | New files | Intake result | Project action |
| --- | --- | --- | --- |
| Elf Swordmaster humanoid | `ElfSwordmaster_Rigged.blend`; `ElfSwordmaster_UE4Project_v4.25.rar`; `RyanReos_ElfSwordmaster.rar` | **Reference Only; commercial-use permission owner-attested, technical adaptation pending.** The author’s included readme describes a 178 cm model with heels, three body variants, PBR textures, and a full-body/facial rig. Read-only Blender inspection found 264 objects, a 710-bone rig, a 159-bone metarig, and four invalid driver-layer warnings. Its source hierarchy is not Humanoid Rig v1. The purchase message lists Elf Swordmaster but does not specify the license tier. | Once the asset workflow is available and the exact export path is approved, retarget/clean it to Humanoid Rig v1, remove unusable source controls, normalize scale/orientation, and validate sockets, deformation, and Godot import. It is a female elf, so it is not a replacement for the project male model. |
| Other Ryan Reos models | `RyanReos_BladedancerMercenary.rar`; `RyanReos_GrisSwimsuit.rar`; `RyanReos_Radagon.zip`; `RyanReos_DeluxeBunnysuit.zip`; `RyanReos_BunnyAkali_FullArchive.zip`; `RyanReos_BunnyAkali_Gallery.zip`; `RyanReos_BunnyAkali_UE5.zip`; `RyanReos_BunnyAkali_ZTL.zip` | **Reference Only; commercial-use permission owner-attested, asset-specific terms/IP review remains.** These contain Blender/FBX/Unreal or ZBrush material, but are not project-ready by extension alone. The local Bunny Akali readme identifies it as League of Legends fan art; Radagon is also a recognizable third-party character. | The original creator's model may be adapted after per-asset technical review. Do not ship recognizable Akali/Radagon fan-art derivatives unless the relevant rights holders' permissions are separately established; they can still inform a distinct, original design. |
| Sims 4 content and animations | `BTTB_6_Base_by_BANK42n_Update_20231122.zip`; `Mimi Moan Pack 1.zip`; `_Noir__Smooth_Vagina_-_2025-09-09.zip`; `Beebavel_WickedWhimsAnimation.package`; `EachUisge_FelineSkinsFA_*.package`; `MYOBI_nippleslider.package`; `SMSims_*`; `Tibo131_*`; `Animations/Animations_List.zip`; `Animations/OLL_Animations_AP_v47.zip`; `Animations/WW_*` | **Not a Godot asset source.** These are Sims 4 `.package` content or archives containing Sims packages and mod-specific animation data. The `_Noir__` archive’s readme restricts use to personal use and says not to convert it to another game/software without permission. | Keep outside the project. Do not extract, convert, redistribute, or use the embedded mod content as game assets. |
| Tripo authoring bridges | `Tripo3d_Blender_Bridge-latest.zip` and extracted `Tripo3d_Blender_Bridge-latest/`; `Tripo3d_Godot_Bridge-latest.zip` | **Authoring tools, not game assets.** The Blender bridge is available upstream under MIT, but the downloaded archive’s version/provenance has not been verified. The Godot bridge archive has no license statement in its included README; its source includes a WebSocket bridge and import path. | Do not install/vendor either bridge in this change. The Blender add-on needs archive provenance/hash verification. The Godot bridge needs license review and a focused network/security review before enabling it in the project editor. No remote generation or paid provider calls were made. |
| Visual reference GIFs | `Pink Drinking GIF by Pudgy Penguins.gif`; `Water Refreshing GIF by xponentialdesign.gif` | **Reference Only.** These are short visual references, not rigged animation assets. | Keep outside runtime assets. If used later, record the source and license before creating derived reference material. |

## License information supplied by the owner

The owner states that the newly supplied assets were acquired with commercial-use permission, and that not every item has a receipt. This is recorded as an owner attestation, not as an independently verified legal opinion. Two local purchase messages were reviewed: `You got Free Bunny Set!.eml` and `You bought Bunny Akali - Game Ready and 6 more products.eml`. The latter lists Bunny Akali, Elf Swordmaster, and several products labeled “Free Model”; the messages establish acquisition details but do not specify the license tier or reproduce the full license terms. No receipt files or personal purchase details were copied into the repository.

The creator's license for a model and permission to use a recognizable third-party character are separate questions. Akali and Radagon remain reference-only for a commercial game unless rights to those underlying characters are confirmed or the project creates a sufficiently distinct original design.

## What was and was not done

- Inspected filenames, archive listings, included readmes, and the Elf Swordmaster Blender scene without saving it.
- Did not move, rename, extract, rewrite, or copy downloaded files.
- Did not import third-party assets into `game/` or modify any existing model.
- Did not convert an asset. The required `game-dev` CLI is unavailable in the current environment. The owner has attested to commercial-use permissions; the local purchase emails do not specify the license tiers. The asset-production workflow requires a verified toolchain and explicit approval of the exact source and output paths before a normalization/export command is run.

## Sorting and conversion path

For any asset that passes license review, keep the original archive outside the runtime tree and create a project-owned working copy under `art_source/reference/<asset-id>/`. Record the source URL, creator, license evidence, SHA-256, source units, transformations, canonical rig mapping, validation report, and final output paths. Only move a normalized GLB into `game/assets/` after its license is cleared and the canonical-rig, materials, scale, animation, and Godot checks pass.

For this intake the first adaptation candidate is the Elf Swordmaster model, based on the owner's commercial-use attestation and subject to technical checks and explicit approval of an exact conversion invocation. The other model packs remain references pending technical review; fan-art characters require separate rights clearance. The Sims content is incompatible; the Tripo archives are editor tools requiring separate review.

## External source notes

- [Elf Swordmaster product page](https://ryanreos.artstation.com/store/bO26/elf-swordmaster-game-ready) — asset description and license choices; the owner attests to commercial-use permission, while the purchase message does not identify a tier.
- [Bunny Akali product page](https://ryanreos.artstation.com/store/BLvbD/bunny-akali-game-ready) — product page for a model whose included readme identifies it as fan art.
- [Radagon product page](https://ryanreos.artstation.com/store/1W91v/blender-model-radagon-of-the-golden-order) — product page for the Elden Ring character fan-art model.
- [Tripo Blender bridge source](https://github.com/Tripo-Developer/tripo-3d-for-blender) — upstream licensing/provenance reference; not proof that the downloaded archive is identical to upstream.
- [Tripo Godot bridge guide](https://www.tripo3d.ai/blog/tripo-dcc-bridge-for-godot) — editor integration guide; not a substitute for reviewing the included source or license.
