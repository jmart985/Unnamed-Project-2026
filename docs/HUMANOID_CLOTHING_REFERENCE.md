# Humanoid Clothing Reference

This note records measured body-to-clothing clearances and how the male NPC
prototype applies them. It is a modeling guide, not a final clothing pipeline
or a commitment to simulated cloth. The measurements live in
`art_source/reference/generated_humanoids/human_humanoid_clothing_fit_v1.json`.

## Observations from the reference model

The elf-adapted reference has separate body and clothing meshes, so its shirt
can be measured directly. In the saved source pose, rays from evaluated body
surface vertices along their outward normals gave median clearances of 8.4 mm
at the lower chest front, 4.0 mm at the lower chest back, 8.3 mm at the upper
chest front, 5.3 mm at the upper chest back, and 7.3 mm at the clavicles. Its
sleeves are deliberately loose: the upper-arm median is about 32 mm. These
values are recorded with sample counts and percentiles in the fit profile.

The male prototype's trousers were also measured directly against the body.
Across 768 front, back, and side samples at the thighs, knees, and lower legs,
the measured clearance is 14 mm. This is the user-reviewed fitted-clothing
reference used to preserve the trouser silhouette and set a closer 14 mm ease
for the male sleeves. The trouser shell gets 10 mm of thickness inward, leaving
its measured outside silhouette unchanged and retaining 4 mm between the inner
surface and body. It is much closer than the loose source sleeve without
removing the space needed for movement.

The first male pass used a clothing-colored body region, which looked painted
on and provided no measurable body-to-clothing separation. The male shirt now
uses a separate skinned shell. Its clearance varies by chest surface and height
using the measured reference values; its sleeves use the 14 mm fitted reference
clearance. The neckline follows the measured collar profile, and the shell
keeps the exact measured outer fit. Solidify adds 3–10 mm inward, adapting its
thickness to leave at least 1 mm between the inner surface and skin. Its
Solidify modifier runs before the armature modifier, so thickness is part of the
skinned mesh rather than a post-skin surface effect. The GLB export applies
non-armature modifiers while retaining the armature, and the exported shirt and
trousers are checked for closed inner surfaces and rims.

## Modeling guidance for humanoid outfits

- Keep measured ease around shoulders, elbows, hips, and knees so clothing does
  not intersect the body during the existing idle pose. Store those values in
  the fit profile rather than scattering offsets through the builder.
- Taper sleeves toward the wrist. Preserve any intended gathered cuff as a
  deliberate, short flare rather than letting the whole forearm look inflated.
- Let shirts, belts, and trousers overlap enough to cover seams through idle
  and basic movement poses.
- Shape hems with several smooth profile changes. Add only subtle, broad folds
  where fabric hangs; avoid sharp ripples that read as armor or hard plastic.
- Keep clothing skinned to the canonical humanoid rig. Use animation/IK or a
  later bounded cloth solution for movement; do not make the base model rely on
  physics simulation to look assembled.
- Check the front, side, and back in neutral and animated poses. A single
  three-quarter render will not reveal waist gaps, sleeve intersections, or
  asymmetrical weighting.

## Application to the male prototype

The male variant uses the Blender Human Base Meshes v1.4.1 realistic male body
with the project's shared Humanoid Rig v1. The body is unrigged in the source
bundle, so its weights are generated for that existing rig. The shirt is a
separate skinned mesh offset from the body by the measured fit profile. The
trousers are separate geometry with the same measured 14 mm clearance that was
verified at 768 body-surface samples. Boots follow the lower legs, while the
belt remains a separate accessory. Materials are project-owned rather than
inherited from the elf-derived outfit.

No project JSON previously contained clothing-clearance values. The fit profile
is a project-owned measurement record created from the source Blender geometry;
the downloaded Quaternius male preview was inspected, but its body and clothing
are fused into one mesh and cannot provide a body-to-clothing distance. The
builder reads the fit profile for both the shirt and trousers, and the shirt
buttons are placed on the shirt shell's outer surface.

The fit profile also controls Solidify thickness and offset. Both garments
keep the measured outside surface in place. The trouser shell is a uniform
10 mm inward; the shirt shell adds up to 10 mm inward and becomes thinner where
the measured clearance is smaller. Buttons sit relative to the unchanged outer
shirt surface.

The male torso has a visibly broader chest and shoulders than the waist. Keep
the measured shell clearance consistent around the chest and arms while
preserving the male body's proportions. Check the shoulder overlap from the
front and back, and inspect both feet because the source body has a slight pose
asymmetry. The builder writes three-quarter, front, side, and back renders.
This remains prototype art until it has been checked in Godot through idle and
locomotion; it is not selected for NPC spawning by this asset-only change.
