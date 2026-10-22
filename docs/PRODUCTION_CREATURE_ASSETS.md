# Production creature asset slice

## Wolf / Canid Rig v1

The first production creature is a project-generated wolf model. It is the
reference implementation for the canid family, not a universal quadruped rig.
The editable Blender source is:

`art_source/blender/generate_creature_canid_v1.py`

The generated source files are kept under:

`art_source/reference/generated_creatures/wolf_canid_rig_v1.*`

The runtime GLB is admitted at:

`game/assets/creatures/wolf/wolf_canid_rig_v1.glb`

## Minimum visual and rig standard

- Shoulder height is approximately 0.70 m and the asset uses 1 Godot unit = 1 m.
- The silhouette uses a narrow running torso, digitigrade legs, a tapered muzzle,
  pointed ears, three-toed paws, and a four-segment tail so it reads as a wolf
  rather than a generic dog or bear.
- The semantic skeleton contains root, pelvis, three spine segments, neck, head,
  jaw, paired front/rear leg chains, digits, ears, eyes, and tail segments.
- Meshes use rigid semantic bone groups in this first slice. The arrangement is
  deliberately easy to refine into smooth weights without changing gameplay
  bone identifiers.
- Included actions are `idle`, `walk`, `trot`, `run`, `eat`, `attack`, and `hurt`.

## Runtime replacement rule

`EntityView` attempts to load the wolf GLB by species. If Godot has not imported
the asset or the file is unavailable, it uses the existing procedural canid body
instead. This fallback is intentional: a missing art import must not prevent
the simulation from running or invalidate saves.

The production asset does not yet implement breeding, pregnancy, or species
behavior. Those remain simulation concerns and will consume semantic anatomy and
species data rather than raw mesh node paths.

## Validation

The generator performs a Blender save, GLB export, metadata export, and rendered
preview. The Blender source contains 32 semantic bones and 44 mesh parts. The
runtime adapter was kept small so asset admission does not change navigation,
collision, or simulation ownership.

## Cow / Ungulate Rig v1

The first ruminant production asset is a project-generated cow. It is a
separate ungulate family implementation; it does not force the canid rig to
fit a different anatomy. The editable Blender source is:

`art_source/blender/generate_creature_ungulate_v1.py`

The generated source files are kept under:

`art_source/reference/generated_creatures/cow_ungulate_rig_v1.*`

The runtime GLB is admitted at:

`game/assets/creatures/cow/cow_ungulate_rig_v1.glb`

The silhouette uses a barrel torso, sloped chest, short neck, muzzle, horns,
ears, four articulated legs, fetlocks, split cloven hooves, and a lowered tail.
The semantic rig exposes root/pelvis/spine/neck/head/jaw, paired front and rear
leg chains, hoof controls, tail, ears, and horns. Included actions are `idle`,
`walk`, `trot`, `eat`, `hurt`, and `turn`.

`EntityView` selects the GLB for cow entities and keeps the procedural ungulate
body as a fallback when the import is unavailable. Collision stays owned by the
simulation adapter rather than by arbitrary mesh geometry. Breeding, milk,
herd behavior, and pregnancy remain future simulation work; this slice supplies
the reusable visual and semantic foundation only.

The cow generator produced 44 mesh parts and 35 semantic bones. The GLB was
re-imported in Blender after export and its six expected action clips were
confirmed before admission.

