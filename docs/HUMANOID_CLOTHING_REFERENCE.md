# Humanoid Clothing Reference

This note records what the current humanoid source outfit does well and what
the male NPC prototype changes. It is a modeling guide, not a final clothing
pipeline or a commitment to simulated cloth.

## Observations from the reference model

The elf-adapted reference uses a readable layered outfit: a fitted upper-body
garment, soft sleeves with gathered volume and open cuffs, a waist-defined
outer garment, and boots. Its fabric reads as cloth because the silhouette
changes gradually at the torso, sleeves, waist, and hem. The sleeves have more
volume than a close-fitting shirt, but that volume is controlled and visibly
connected to the shoulder and cuff.

The first male pass exposed two problems to avoid: a coarse torso shell looked
like a separate rigid primitive, and strongly compressed sleeves pulled away
from the arm and exposed thin strips of skin. The revised torso layer uses a
smoother profile, a longer overlap at the hips, and a restrained hem fold; the
source sleeves retain enough volume to stay connected to the arms.

## Modeling guidance for humanoid outfits

- Keep a small amount of ease around shoulders, elbows, hips, and knees so
  clothing does not intersect the body during the existing idle pose.
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

The prototype keeps the shared elf-adapted skeleton, blouse sleeves, and source
palette. It gently broadens the upper torso, reduces the pronounced waist
shaping, and tapers sleeve volume a little toward the wrist. A fitted outer
shirt layer hides the source chest contour and overlaps tailored trousers that
replace the source skirt. This remains prototype art and should be evaluated in
Godot during idle and locomotion before it is used for population spawning.
