# External Asset Intake — 2026-10-25

## Scope

This review covers files dated October 25, 2026 in `C:\Users\James Home\Repo` and its newly added `Animations` and Tripo bridge folders. That directory is the parent of the Git checkout, not the project itself. The originals remain there, unchanged. Existing files under `Models/` have older timestamps and are outside this intake.

The goal is to identify reusable project material or rigs. Track availability, technical suitability, and project approval as separate facts:

- **Availability:** the source files are present and can be inspected.
- **Technical suitability:** the asset has passed the checks for its intended role.
- **Project approval:** the owner decides whether the project may use it. The owner has final approval authority; a receipt or separate permission-evidence document is not required for that decision.

Files being present, “game ready” marketing, and technical suitability do not themselves constitute project approval. Record known source and license details as context; leave unknown details marked unknown rather than treating them as verified.

## Disposition

| Group | New files | Intake result | Project action |
| --- | --- | --- | --- |
| Elf Swordmaster humanoid | `ElfSwordmaster_Rigged.blend`; `ElfSwordmaster_UE4Project_v4.25.rar`; `RyanReos_ElfSwordmaster.rar` | **commercial-use permission owner-attested, technical adaptation pending.**Blender inspection found 264 objects, a 710-bone rig, a 159-bone metarig. Its source hierarchy is not Humanoid Rig v1. The purchase message lists Elf Swordmaster. | Once the asset workflow is available and the exact export path is approved, retarget/clean it to Humanoid Rig v1, re-assess source controls, normalize scale/orientation, and validate sockets, deformation, and Godot import. It is a female elf, so it is not a replacement for the project male model. |
| Other Ryan Reos models | `RyanReos_BladedancerMercenary.rar`; `RyanReos_GrisSwimsuit.rar`; `RyanReos_Radagon.zip`; `RyanReos_DeluxeBunnysuit.zip`; `RyanReos_BunnyAkali_FullArchive.zip`; `RyanReos_BunnyAkali_Gallery.zip`; `RyanReos_BunnyAkali_UE5.zip`; `RyanReos_BunnyAkali_ZTL.zip` | **Commercial-use permission.** These contain Blender/FBX/Unreal or ZBrush material, but are not project-ready by extension alone. `BTTB_6_Base_by_BANK42n_Update_20231122.zip`; `Mimi Moan Pack 1.zip`; `_Noir__Smooth_Vagina_-_2025-09-09.zip`; `Beebavel_WickedWhimsAnimation.package`; `EachUisge_FelineSkinsFA_*.package`; `MYOBI_nippleslider.package`; `SMSims_*`; `Tibo131_*`; `Animations/Animations_List.zip`; `Animations/OLL_Animations_AP_v47.zip`; `Animations/WW_*` | **Not a Godot asset source.** These are Sims 4 `.package` content or archives containing Sims packages and mod-specific animation data. Extract, and convert. |
| Tripo authoring bridges | `Tripo3d_Blender_Bridge-latest.zip` and extracted `Tripo3d_Blender_Bridge-latest/`; `Tripo3d_Godot_Bridge-latest.zip` | **Authoring tools, not game assets.** The Blender bridge is available upstream under MIT, but the downloaded archive’s version/provenance has not been verified. 
## License information supplied by the owner

The owner confirms commercial-use purchases and gives final project approval for the assets in this intake. Receipts or separate permission-evidence documents are not a prerequisite for this project approval. This records the owner's project decision, not technical readiness or an independent determination of third-party rights. Keep known source terms as factual notes and leave unknown terms marked unknown.

## What was and was not done

- Inspected filenames, archive listings, included readmes, and the Elf Swordmaster Blender scene without saving it.
- Did not move, rename, extract, rewrite, or copy downloaded files.
- Did not import third-party assets into `game/` or modify any existing model.
- Did not convert an asset. a normalization/export command needs to be run.

## Sorting and conversion path

Keep available original archives outside the runtime tree and create a project-owned working copy under `art_source/reference/<asset-id>/` when needed. Record reproducibility and technical details as they become available. Missing receipts or separate permission-evidence documents do not block owner approval. Keep known source terms as factual notes and label unknown terms accurately.

Owner approval and technical suitability are separate. Only move a normalized GLB into `game/assets/` after the owner approves that intended runtime use and the canonical-rig, materials, scale, animation, and Godot checks pass. Owner approval alone does not make an asset technically ready.

For this intake the first adaptation candidate is the Elf Swordmaster model. The Sims content is incompatible.
