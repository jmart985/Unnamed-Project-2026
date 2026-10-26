# Starter NPC models

These ten runtime GLBs are generated from the source scenes listed in the matching `.json` sidecars and in `art_source/blender/starter_npc_model_roster_v1.json`. The source scenes remain in the local asset vault; the playable project uses only the GLBs in this folder.

Every model is normalized to 1.75 m and includes generated in-place `human_idle`, `human_walk`, and `human_run` cycles. The exporter maps the source rig's spine, head, arms, and legs, then checks those joints are animated in all three cycles. Non-empty source skeletal actions are preserved. The current runtime base appearance is preserved while unused source morph libraries are omitted. Where usable facial shape keys exist, the exporter builds compact reusable `npc_face_*` expression clips from facial deltas. Source-authored facial bone clips are also preserved and mapped to canonical expression labels when recognized. Each sidecar lists the clips and targets available for that model. Source images, including nested material-group images and UDIM tiles, are normalized to at most 1024 px per side.

When refreshing an existing runtime model with new clips, use `--preserve-base-from <existing-runtime-folder>` and write to a separate temporary output folder. This copies the reference model's base vertex positions, node transforms, and inverse bind matrices after verifying node names, mesh topology, and joint order. Review the generated GLB and sidecar before replacing runtime files. This prevents a change to an unused source morph library from shifting the visible rest pose.

| NPC ID | Name | Sex | Preserved source clips |
| --- | --- | --- | ---: |
| `entity-000001` | Dwight | Male | 0 |
| `entity-000002` | Aerith | Female | 37 |
| `entity-000003` | Omni-Man | Male | 0 |
| `entity-000004` | Aloy | Female | 0 |
| `entity-000005` | Quentin | Male | 0 |
| `entity-000006` | Jill | Female | 0 |
| `entity-000007` | Vincent | Male | 0 |
| `entity-000008` | Serena | Female | 0 |
| `entity-000009` | Undead F | Female | 0 |
| `entity-000010` | Yuffie | Female | 1 |

The Aloy source contains an empty action placeholder, so it contributes no source clip. The downloaded `.blend` sources are never modified. Each sidecar records source morphs omitted and compact runtime expression targets generated; unused source morph libraries are excluded from runtime GLBs to keep them compact. The **NPC Test Lab** button in the top HUD lets you select any currently loaded NPC or creature, list and play its imported clips, run short walk/run routes, and try greet, interact/feed/pet, attack, follow, and stay actions. The overlay releases the mouse pointer for clicking while simulation, keyboard movement, and controller movement remain active; press Escape to close it and resume mouse look. Interaction range and gameplay rules still apply. Facial clips currently play as standalone clips; automatic emotion selection and facial/locomotion blending are not implemented.

`game/data/starter_npc_presentations.json` binds these models to stable starter NPC IDs. This presentation mapping does not control player character creation. To regenerate models, run `art_source/blender/export_starter_npc_models_v1.py` with the source asset root and a temporary folder as `--output-root`; optionally pass this folder as `--preserve-base-from` to retain existing rest meshes and skin binds. The roster JSON contains the source scene names, selected meshes, and semantic bone maps.
