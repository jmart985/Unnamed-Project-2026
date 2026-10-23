# Interaction Anchor Standard

**Status:** Provisional implementation standard
**Scope:** Creature and humanoid species definitions
**Engine direction:** Godot 4 .NET with C#

## Purpose

Creatures and humanoids need stable places where simulation actions can align an animation, IK target, medical tool, or carried item. These places are **semantic interaction anchors**: logical transforms or sockets exposed by an imported scene or procedural rig. They are not gameplay state, do not imply that an action is available, and do not have to be deforming bones.

This keeps breeding, medical examination, feeding, nursing, waste handling, grooming, creature handling, social interactions, and other species-specific actions independent from source-model bone names and scene paths.

```text
Simulation anatomy
        ↓
Semantic interaction anchors
        ↓
Animation / IK alignment
        ↓
Breeding, medical, feeding, and social systems
```

## Species-definition contract

`SpeciesDefinition.Anchors` declares five separate collections:

- `AnatomyRegions`: semantic body regions used by simulation and injury systems.
- `LocomotionAnchors`: feet, paws, roots, or other ground/contact transforms.
- `EquipmentAnchors`: hands, carry points, and other equipment attachment transforms.
- `InteractionAnchors`: all sockets that may be targeted by an interaction system.
- `BreedingAnchors`: the subset that can participate in a species' reproductive interaction model.

Every breeding anchor must also be listed as an interaction anchor. This prevents systems from selecting a target that the scene cannot expose. Existing records that omit the optional arrays load with empty collections for compatibility; new species records should declare all applicable collections before being admitted as production content.

Anchor IDs use lowercase `anchor_` names with underscores. The runtime validates duplicates, malformed IDs, and breeding anchors missing from the interaction collection. The collections are data only: an anchor does not grant a creature permission, fertility, consent, health, or a particular animation.

## Canonical IDs

The shared IDs are defined in `InteractionAnchorIds`:

| Category | IDs |
| --- | --- |
| Core anatomy | `anchor_mouth`, `anchor_anus`, `anchor_genital`, `anchor_urethral`, `anchor_navel` |
| Reproductive or nursing | `anchor_vaginal`, `anchor_penile`, `anchor_testicular`, `anchor_udder`, `anchor_teat_l`, `anchor_teat_r`, `anchor_breast_l`, `anchor_breast_r` |
| Egg-laying species | `anchor_cloaca`, `anchor_ovipositor` |
| Locomotion/equipment examples | `anchor_root`, `anchor_foot_l`, `anchor_foot_r`, `anchor_hand_l`, `anchor_hand_r`, `anchor_carry` |

Species extensions are allowed where anatomy requires them, for example `anchor_proboscis`, `anchor_beak`, `anchor_gill`, `anchor_scent_gland`, `anchor_pouch`, and `anchor_egg_laying`.

The presence of an anchor does not require every species to use it. A mammal may expose `anchor_mouth`, `anchor_anus`, `anchor_genital`, and `anchor_udder`; a bird may expose `anchor_beak` and `anchor_cloaca`; and an egg-layer may expose `anchor_mouth` plus `anchor_cloaca` or `anchor_egg_laying`. A humanoid may expose the core set, `anchor_breast_l`, `anchor_breast_r`, and the reproductive anchors modeled by its species and body definition.

## Separation from visual rigs

The anchor contract is separate from both simulation anatomy and the canonical humanoid skeleton. An imported mesh may implement an anchor as a bone, an attachment node, a procedural transform, or a generated IK target. Gameplay code must request the semantic ID and let the species' rig adapter resolve the transform.

This follows the same boundary used by `docs/CREATURE_RIG_FAMILIES.md`:

```text
Source rig or procedural body
        ↓
Semantic anchor map
        ↓
SpeciesDefinition.Anchors
        ↓
Interaction and animation services
```

Source names such as `Bip001-Pelvis` must never become save-data or gameplay dependencies. Save data should retain stable species and interaction identifiers, never raw node paths or bone indices.

## Current data and migration

The authoritative runtime records in `data/species.json` now declare anchor collections for human, generic creature, ape, cow, and wolf. The project-generated humanoid, ape, cat, cow, fox, pig, and wolf metadata now uses the same categorized canonical IDs. Older downloaded asset metadata may still contain source-specific labels; those files remain reference/prototype metadata until they are migrated and validated against the imported rig.

Future breeding, nursing, medical, and adult/social systems must still validate their own state, capability, consent, and animation requirements. This standard supplies alignment targets only; it does not implement those systems.

## Validation checklist

A new species record is ready for implementation when:

1. Anatomy regions map to the simulation's semantic body model.
2. Locomotion and equipment anchors resolve for the selected rig family.
3. Every modeled interaction and breeding anchor has a unique canonical ID.
4. The imported or procedural scene resolves each declared ID without a source-bone dependency.
5. Save/load stores stable species and semantic IDs rather than node paths or bone indices.
6. Interaction systems separately verify capability, range, state, and animation before using an anchor.
