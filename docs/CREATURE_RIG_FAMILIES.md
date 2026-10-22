# Creature Rig Families and Species Generation

**Status:** Provisional project standard, issue #430  
**Engine direction:** Godot 4 .NET with C#  
**Asset target:** normalized GLB/glTF after Blender review

## Purpose

Unnamed Project 2026 needs many creatures with different bodies, feet, shoulders, gaits, senses, and work capabilities. The project must generate those species from reusable data without forcing every animal into one visual quadruped skeleton.

The project therefore shares **simulation semantics**, not one universal visual rig.

```text
Species definition
        ↓
Shared creature simulation contract
        ↓
Semantic anatomy and capability map
        ↓
Anatomically appropriate rig family
        ↓
Mesh, collision, animation, and sockets
```

A rig family is a reusable anatomical starting point. A species profile supplies proportions, diet, senses, capabilities, and tuning within that family. A source asset never becomes a gameplay dependency merely because it was downloaded first.

## Shared simulation contract

Every active creature, regardless of rig family, exposes the same domain-facing concepts:

- stable entity identity and species ID;
- position, facing, movement speed, mass, and collision profile;
- health and injury regions;
- needs, diet, metabolism, and exhaustion;
- senses and perception ranges;
- movement capabilities and gait tags;
- work, combat, interaction, capture, and domestication capabilities;
- inventory/carrying and equipment sockets where the species supports them;
- relationships, allegiance, captivity state, and memories;
- save/load representation independent of scene-node paths.

The simulation consumes semantic IDs. It never searches for source bone names such as `Bip001-L-Forearm`, `Arm.L`, or arbitrary Blender prefixes.

## Semantic anatomy contract

The contract is intentionally broad enough for mammals and reptiles while remaining independent of mesh topology.

### Axial regions

```text
Root
Pelvis
Torso
Neck
Head
Jaw
```

A family may split Torso into multiple spine segments or add a shoulder/scapula control. Those additions do not change the required semantic regions.

### Limb regions

```text
FrontUpperLeg_L / FrontLowerLeg_L / FrontFoot_L / FrontDigits_L
FrontUpperLeg_R / FrontLowerLeg_R / FrontFoot_R / FrontDigits_R

RearUpperLeg_L / RearLowerLeg_L / RearFoot_L / RearDigits_L
RearUpperLeg_R / RearLowerLeg_R / RearFoot_R / RearDigits_R
```

Names describe the creature's anatomy, not human world-left/world-right. A family mapping records which source bones drive each region.

### Optional regions and controls

```text
Shoulder_L / Shoulder_R
Scapula_L / Scapula_R
Tail_01 ... Tail_N
Eye_L / Eye_R
Ear_L / Ear_R
Tongue
Claw_*
Wing_*
Horn_*
Fin_*
ToolAnchor
CarryAnchor
BiteAnchor
```

Optional regions may affect animation, targeting, equipment, or interaction. They must not be required by systems that can operate on the shared contract.

## Initial rig-family registry

| Family | Initial species | Structural requirements | Typical gait/pose |
| --- | --- | --- | --- |
| Canid digitigrade | wolf, dog, coyote | mobile scapula, deep chest, digitigrade paws, flexible spine, tail base | walk, trot, run, pounce |
| Vulpine digitigrade | fox | slender torso, shorter limbs, pointed muzzle, upright ears, long bushy tail, non-retractable claws | walk, trot, quick turn, pounce |
| Felid digitigrade | cat, bobcat | flexible spine, powerful shoulder, digitigrade paws, retractable-claw extension | stalk, walk, sprint, pounce |
| Ruminant cloven-hoof | cow, goat, sheep | fused cannon bones, two primary digits, cloven hooves, neck and grazing pose | walk, graze, trot, turn |
| Suiform cloven-hoof | pig, wild boar | heavy torso, shorter legs, functional accessory toes, omnivore head/neck profile | walk, trot, root, charge |
| Single-hoof ungulate | horse | elongated distal limbs, one weight-bearing digit, hoof roll, large stride | walk, trot, gallop |
| Primate extension | ape, gorilla | mobile shoulders, long forelimbs, strong hands, separate knuckle-walk and biped poses | knuckle walk, walk, climb, reach |
| Plantigrade mammal | bear, raccoon | broad flat feet, heel contact, heavy shoulder or manipulative forepaws | walk, stand, climb, forage |
| Sprawling reptile | lizard | splayed shoulder/pelvic limbs, flexible trunk, segmented tail, temperature poses | scurry, bask, climb |
| Crocodilian extension | crocodile, alligator | low stance, strong tail, long snout, distinct fore/hind proportions | walk, swim, ambush |
| Dragon composite | dragon | selected reptile/mammal base plus optional wings, horns, jaw, tail, and flight controls | walk, run, fly, bite |

**Vulpine is deliberately separate from generic canid.** Foxes can share the digitigrade locomotion foundation, but their proportions, head, ears, tail, and behavior must not be generated as a scaled-down wolf or dog.

A new family is justified when changing the base rig would create incorrect joint placement, foot contact, gait, collision, or interaction. A new species within an existing family should normally be data and mesh variation rather than a new runtime architecture.

## Species definition boundary

A species profile owns rules and proportions. The mesh and rig only provide the visual implementation.

Minimum profile fields:

```text
id
familyId
bodyPlan
adultHeightRange
massRange
proportions
gaitSet
diet
metabolism
senses
temperament
capabilities
anatomyRegions
collisionProfile
equipmentSockets
animationOverrides
variationLimits
assetReferences
```

Examples:

- A wolf profile selects the canid family, carnivore diet, strong smell/tracking, pack behavior, bite and pounce actions, and a deep-chest proportion range.
- A fox profile selects the vulpine family, carnivore/omnivore tuning, strong smell/tracking, smaller mass, pointed-head proportions, and a long-tail variation range.
- A cow profile selects the ruminant family, herbivore diet, grazing, hauling strength, cloven-hoof collision, and no fine-tool manipulation.
- An ape profile selects the primate family, omnivore diet, tool and door use, high dexterity, grasp sockets, and a knuckle-walk gait.
- A lizard profile selects the sprawling reptile family, heat tolerance, tail segments, climb capability, and splayed-limb collision.

Species-specific senses, diet, work capabilities, and behavior are data. They must not be inferred from a mesh name or from whichever bones an imported asset happens to contain.

## Semantic mapping examples

A source canid skeleton may use arbitrary names:

```text
Source shoulder_l       -> Shoulder_L
Source upperarm_l       -> FrontUpperLeg_L
Source forearm_l        -> FrontLowerLeg_L
Source paw_l            -> FrontFoot_L
Source thigh_l          -> RearUpperLeg_L
Source shin_l           -> RearLowerLeg_L
Source paw_back_l       -> RearFoot_L
Source tail_01...       -> Tail_01...
```

A source lizard skeleton may have `front_arm_l`, `hind_leg_l`, and `caudal_01`; the same semantic contract still applies. The mapping is stored in asset metadata and remains separate from domain code.

If a source lacks a required region, it cannot be silently admitted as a production species. The importer must report the missing mapping, and the asset remains reference-only or prototype-only until repaired.

## Animation and locomotion boundary

Core systems request semantic animation tags:

```text
idle, walk, trot, run, turn, stop, eat, drink, attack, hurt, sleep, climb
```

Each family supplies the appropriate implementation:

- canids and vulpines use digitigrade foot contact and flexible-spine run cycles;
- ruminants and suiforms use cloven-hoof contact and grazing/rooting poses;
- primates use a separate knuckle-walk and grasp/reach set;
- reptiles use splayed-limb timing and tail-driven turns;
- dragons extend a selected base family with wing, jaw, or fire actions.

Navigation remains controller-driven. Root motion is reserved for attacks, pounces, climbing, mounting, takeoff/landing, and other contextual actions.

## Collision, targeting, and interaction

Collision proxies and hit regions are family data, not a fixed capsule copied to every species.

Each family must define:

- body and head bounds;
- limb and foot clearance;
- tail and optional extension policy;
- player-clearance radius;
- targetable semantic regions;
- bite, grasp, tool, carry, and mount anchors where supported.

The same crowd-safety rules apply to all families. A long tail, horn, wing, or low reptile body may change the proxy shape, but it must not create invisible blocking geometry or allow the player to pass through the creature unintentionally.

## Acceptance gate for a new species

Before a species can spawn as an active near-field entity:

1. Its family is registered and its anatomy is documented.
2. The species profile validates all required IDs, ranges, capabilities, and variation limits.
3. The source asset license and provenance are recorded.
4. The asset is normalized to Godot's 1 unit = 1 meter scale and consistent forward direction.
5. Required semantic mappings and optional socket mappings are present.
6. Idle, locomotion, turn, attack, hurt, and interaction poses are reviewed.
7. Collision, targeting, player clearance, and crowd recovery are tested.
8. A representative near-field crowd test passes without unacceptable deformation or performance cost.
9. A Godot GLB/glTF import succeeds without scale, material, or animation errors.
10. Missing features are explicitly marked unsupported rather than silently approximated.

Until the gate passes, the species may exist as reference data or a distant statistical population, but it must not be presented as a finished active creature.

## Implementation order

1. Keep the semantic contract in the simulation layer.
2. Add family metadata and source-bone mapping outside gameplay code.
3. Build one neutral prototype per family, beginning with canid, vulpine, ruminant, primate, and reptile.
4. Add species profiles and controlled variations within those families.
5. Add family-specific animation, collision, sockets, and interaction adapters.
6. Validate near-field spawning, far-field statistics, save/load identity, and crowd safety.
7. Add final art only after the family gate passes.

This preserves the goal of generating many species like humanoids: species are data-driven and reusable, while the underlying visual rigs remain anatomically credible.
