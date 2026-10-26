# Starter NPC models

These ten runtime GLBs are generated from the source scenes listed in the matching `.json` sidecars and in `art_source/blender/starter_npc_model_roster_v1.json`. The source scenes remain in the local asset vault; the playable project uses only the GLBs in this folder.

Every model is normalized to 1.75 m and includes generated in-place `human_idle`, `human_walk`, and `human_run` cycles. The exporter maps the source rig's spine, head, arms, and legs, then checks those joints are animated in all three cycles. Non-empty source skeletal actions are preserved. Source images, including nested material-group images and UDIM tiles, are normalized to at most 1024 px per side.

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

The Aloy source contains an empty action placeholder, so it contributes no source clip. Runtime exports omit source shape-key/morph targets; their counts are recorded in each sidecar, and the local source scenes remain unchanged. Add morph export when a runtime feature consumes those targets.

`game/data/starter_npc_presentations.json` binds these models to stable starter NPC IDs. This presentation mapping does not control player character creation. To regenerate models, run `art_source/blender/export_starter_npc_models_v1.py` with the source asset root and this folder as `--output-root`; the roster JSON contains the source scene names, selected meshes, and semantic bone maps.
