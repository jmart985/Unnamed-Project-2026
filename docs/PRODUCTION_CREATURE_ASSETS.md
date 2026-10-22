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

- Shoulder height is approximately 0.88 m and the asset uses 1 Godot unit = 1 m.
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
preview. The refined Blender source contains 32 semantic bones and 45 mesh parts.
Its torso is longer and deeper than the vulpine profile, its rear waist tapers
before the hindquarters, and its upper hind-leg segments are longer. The
jaw/muzzle is broader while retaining a canid silhouette. The
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
ears, four articulated legs, fetlocks, split cloven hooves, a lowered tail, and
a stylized four-quarter udder with four teats for future milking interactions.
The semantic rig exposes root/pelvis/spine/neck/head/jaw, paired front and rear
leg chains, hoof controls, tail, ears, and horns. Included actions are `idle`,
`walk`, `trot`, `eat`, `hurt`, and `turn`.

`EntityView` selects the GLB for cow entities and keeps the procedural ungulate
body as a fallback when the import is unavailable. Collision stays owned by the
simulation adapter rather than by arbitrary mesh geometry. Breeding, milk,
herd behavior, and pregnancy remain future simulation work; this slice supplies
the reusable visual and semantic foundation only.

The cow generator produced 53 mesh parts and 44 semantic bones, including the
udder and teat interaction bones. The GLB was re-imported in Blender after
export and its six expected action clips were confirmed before admission.

## Ape / Primate Rig v1

The first primate production asset is a project-generated ape. It is a distinct
primate family implementation rather than a humanoid or canid mesh reused with
different colors. The editable Blender source is:

`art_source/blender/generate_creature_primate_v1.py`

The generated source files are kept under:

`art_source/reference/generated_creatures/ape_primate_rig_v1.*`

The runtime GLB is admitted at:

`game/assets/creatures/ape/ape_primate_rig_v1.glb`

The silhouette uses a broad chest, heavy brow, short muzzle, rounded ears, long
forelimbs, large hands, separate knuckle pads, plantigrade feet, and a short
tail. The semantic rig contains the canonical torso/head chains plus clavicles,
hands, feet, knuckle controls, and tail controls. Included actions are `idle`,
`walk`, `knuckle_walk`, `run`, `climb_pose`, `eat`, `hurt`, and `turn`.

`EntityView` selects the GLB for ape entities and retains the procedural primate
fallback. Advanced climbing, tool use, and species behavior remain simulation
work; this slice supplies the reusable visual and semantic foundation.

The ape generator produced 39 mesh parts and 30 semantic bones. Blender
re-import confirmed all eight expected action clips before admission.

## Fox / Vulpine Rig v1

The first vulpine production asset is a project-generated fox. It shares the
quadruped semantic contract with the wolf but intentionally uses a separate
family profile. The editable Blender source is:

`art_source/blender/generate_creature_vulpine_v1.py`

The generated source files are kept under:

`art_source/reference/generated_creatures/fox_vulpine_rig_v1.*`

The runtime GLB is admitted at:

`game/assets/creatures/fox/fox_vulpine_rig_v1.glb`

The silhouette uses a narrow light frame, pointed muzzle, large upright ears,
compact paws, and a proportionally long bushy tail with a pale tip. The
semantic rig contains root/pelvis/three-spine/neck/head/jaw, paired limb and
foot chains, ears, and four tail segments. Included actions are `idle`, `walk`,
`trot`, `run`, `sniff`, `eat`, `hurt`, and `turn`.

`EntityView` selects the asset for both `fox` and `vulpine` species IDs and keeps
the procedural canid fallback. Scent tracking remains simulation work; the
model only provides the stable visual and semantic foundation.

The fox generator produced 36 mesh parts and 26 semantic bones. Blender
re-import confirmed all eight expected action clips before admission.

## Cat / Felid Rig v1

The first feline production asset is a project-generated cat. It is a distinct
felid family profile rather than a recolored canid. The editable Blender source
is:

`art_source/blender/generate_creature_felid_v1.py`

The generated source files are kept under:

`art_source/reference/generated_creatures/cat_felid_rig_v1.*`

The runtime GLB is admitted at:

`game/assets/creatures/cat/cat_felid_rig_v1.glb`

The silhouette uses a compact feline body, short muzzle, large triangular ears,
articulated paws, and a segmented tail. The semantic rig contains root/pelvis/
three-spine/neck/head/jaw, ear controls, paired front and rear limb chains,
foot controls, and four tail segments. Included actions are `idle`, `walk`,
`trot`, `run`, `stalk`, `pounce`, `eat`, `hurt`, and `turn`.

`EntityView` selects the GLB for both `cat` and `feline` species IDs and keeps
the procedural quadruped fallback. Hunting, scent, domestication, breeding, and
other feline behavior remain simulation work; this slice supplies the reusable
visual and semantic foundation.

The cat generator produced 36 mesh parts and 26 semantic bones. Blender
re-import confirmed all nine expected action clips before admission.

## Pig / Suiform Rig v1

The first suiform production asset is a project-generated pig. It is a
separate family profile from ruminants: the body is shorter and heavier, the
legs are compact, the snout is broad, and the tail is raised and curled. The
editable Blender source is:

`art_source/blender/generate_creature_suiform_v1.py`

The generated source files are kept under:

`art_source/reference/generated_creatures/pig_suiform_rig_v1.*`

The runtime GLB is admitted at:

`game/assets/creatures/pig/pig_suiform_rig_v1.glb`

The semantic rig contains root/pelvis/three-spine/neck/head/jaw, paired front
and rear limb chains, cloven-hoof controls, ears, and four curled tail
segments. Included actions are `idle`, `walk`, `trot`, `run`, `graze`, `eat`,
`hurt`, and `turn`.

`EntityView` selects the GLB for `pig`, `swine`, and `boar` species IDs and
keeps the procedural quadruped fallback. Omnivore diet tuning, rooting,
herding, breeding, and livestock simulation remain separate domain work.

The pig generator produced 43 mesh parts and 34 semantic bones. Blender
re-import confirmed all eight expected action clips before admission.

