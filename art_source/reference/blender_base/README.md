# Bare Blender male base

This checkpoint comes directly from Blender Human Base Meshes v1.4.1,
`human_base_meshes_bundle.blend`, selecting only:

- `GEO-body_male_realistic`
- `GEO-body_male_realistic.eye.L`
- `GEO-body_male_realistic.eye.R`

`male_base.blend` preserves the original body cage and its three-level Multires
sculpt. It contains only those three mesh objects. There is no project rig,
hair, clothing, footwear, animation, or anatomy adaptation. The sole geometric
transform is uniform normalization/centering to approximately 1.75 m.

`male_base.glb` exports that authored evaluated surface: 677,442 body vertices,
plus the original two eyes. This is the full-detail source checkpoint for model
review; it is not wired into the playable character. A later runtime derivative
can be rigged and fitted with separate equipment after the base is accepted.

The front, side, and back PNGs render the **reimported GLB** in neutral clay.
Preview lights, camera, and clay materials are temporary and are not included
in the saved source or export. `male_base.json` records the original source
file hash, cage/evaluated counts, modifier levels, and GLB round-trip result.

To regenerate from the acquired original bundle:

```powershell
blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python art_source/blender/extract_male_base.py -- --source "PATH/TO/human_base_meshes_bundle.blend"
```

The extractor checks that the export has exactly the body and two eyes, no rig
or animations, and all evaluated source positions within 0.00001 m after the
GLB round trip. The `.blend` keeps the editable Multires setup; the GLB contains
its evaluated surface.
