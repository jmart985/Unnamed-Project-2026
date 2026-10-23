# Human Anatomy and Species Breeding Reference

**Status:** Reference and architecture guidance only, issue #432
**Implementation status:** No runtime breeding, pregnancy, genetics, or hybrid systems are implemented by this document.

## Purpose

Humans are one animal species in the world simulation, while also using the project's canonical upright-biped humanoid rig. Human anatomy must therefore be researched and represented with the same discipline used for quadrupeds and reptiles.

The project also needs a future population model that can describe inherited appearance, capabilities, health conditions, fertility, pregnancy or incubation, and offspring outcomes across humans and other animals. That model must remain separate from the visual rig and must not assume that visual similarity or shared locomotion means two species can interbreed.

## Human anatomical reference

Human anatomy is divided into the same two broad skeletal layers used by the project:

- **Axial skeleton:** skull, jaw, vertebral column, ribs, sternum, and trunk.
- **Appendicular skeleton:** shoulder girdle, arms and hands, pelvic girdle, legs, and feet.

The axial skeleton supports the upright body and protects the brain, spinal cord, heart, and lungs. The ribs and vertebral column also contribute to protection of parts of the upper abdomen, while the pelvic girdle and surrounding structures provide much of the protection for pelvic organs. The appendicular skeleton provides limb attachment and movement. Human lower limbs and pelvis prioritize stable bipedal walking; the upper limbs and shoulder girdle prioritize reach, manipulation, carrying, and tool use.

### Human simulation regions

The detailed regions below are the proposed semantic anatomy for a future health and equipment expansion. They are not the current runtime or save identifiers. The current `CharacterHealth.CreateHumanoid` contract remains authoritative and contains exactly `Head`, `Torso`, `LeftArm`, `RightArm`, `LeftLeg`, and `RightLeg`, in that order. Existing combat and save/load code requires those names and count. A later issue must define a versioned migration and compatibility tests before any detailed region is added to live saves.

Future migration target (lowercase semantic region IDs; display labels may be title-cased):

```text
head
jaw
neck
torso
pelvis

clavicle_l / shoulder_l
upper_arm_l / lower_arm_l / hand_l
clavicle_r / shoulder_r
upper_arm_r / lower_arm_r / hand_r

upper_leg_l / lower_leg_l / foot_l / toes_l
upper_leg_r / lower_leg_r / foot_r / toes_r
```

This aligns with Humanoid Rig v1:

```text
root -> pelvis -> spine_01 -> spine_02 -> spine_03 -> neck_01 -> head
clavicle_l -> upperarm_l -> lowerarm_l -> hand_l
clavicle_r -> upperarm_r -> lowerarm_r -> hand_r
thigh_l -> calf_l -> foot_l -> toe_l
thigh_r -> calf_r -> foot_r -> toe_r
```

Visual bones drive deformation and animation. After the migration, simulation regions will own health, injuries, equipment effects, movement penalties, and future pregnancy/body-condition state. Saved games must use stable semantic identifiers, never raw bone indices; until then, preserve the current six-region schema.

### Human variation constraints

Human variation may include:

- height and overall proportions;
- body mass and muscle/build;
- shoulder width, torso length, and leg length;
- age-related proportions;
- skin, hair, and eye appearance;
- scars, injuries, prosthetics, and clothing;
- pregnancy-related body changes when a later system supports them.

Variation must stay inside validated rig, collision, camera, and animation ranges. A body slider cannot arbitrarily stretch a bone or create a new anatomy rule. Species, body type, and health conditions remain separate inputs.

## Breeding and population reference model

The future system should treat breeding as a population and lineage process, not as a mesh button.

A breeding record may eventually contain:

```text
parentAId
parentBId
parentASpeciesId
parentBSpeciesId
offspringSpeciesId
lineage identifiers
sex or body type
age and maturity state
fertility state
pregnancy or incubation state
gestation or incubation duration
litter or clutch outcome
inherited traits
congenital conditions
developmental modifiers
birth or hatch health
```

These fields are separate concerns. A creature may have a valid visual rig while being infertile, pregnant, injured, diseased, too young, too old, or otherwise unable to produce offspring.

Record each parent's species independently of the parent entity reference. This preserves lineage and the exact compatibility pairing if either parent entity is later removed or cannot be loaded.

### Trait categories

The future population model should distinguish:

**Inherited traits**

- height, mass, and body proportions;
- strength, endurance, dexterity, speed, and metabolism;
- coat, skin, scale, feather, hair, eye, horn, ear, tail, and pattern traits;
- temperament, sociability, aggression, fear response, and curiosity;
- senses such as smell, hearing, sight, and tracking;
- fertility, litter/clutch tendencies, growth rate, and disease resistance.

**Congenital conditions**

Conditions present at birth or caused during development. Examples include structural limb differences, organ defects, abnormal growth, and inherited metabolic disorders.

**Acquired conditions**

Injuries, infections, parasites, malnutrition, poisoning, scars, chronic disease, and other conditions that occur after birth or hatch.

**Environmental and maternal effects**

Nutrition, illness, toxins, stress, temperature, crowding, prenatal or incubation conditions, and care can affect outcomes without being inherited as a simple gene.

The simulation should not label every unusual appearance as genetic. It must preserve the distinction between inherited, congenital, acquired, and environmental causes.

### Appearance representation

Appearance should be data-driven and composable:

```text
body proportions -> validated species/body-family ranges
skin/coat/scale palette -> material or texture parameters
markings/patterns -> masks and controlled variation
hair/feather/crest -> approved asset variants
horn/ear/tail shape -> species extension data
scars/injuries -> acquired condition overlays
pregnancy/body condition -> time-varying proportion modifiers
```

A species profile can restrict which traits are valid. A cow should not receive a wolf paw or a human hand merely because a generic appearance generator has that option. A hybrid, if ever supported, needs an explicit body plan and rig policy.

## Inter-breedability policy

Interbreeding is a species-pair rule, not an automatic consequence of sharing a rig family.

The future compatibility record should support:

```text
speciesA
speciesB
compatibility: viable | sterile | incompatible | unknown
requires conditions
offspring species or hybrid profile
fertility of offspring
gestation/incubation policy
known risks
appearance inheritance policy
notes and evidence
```

Initial default policy (design guidance only; no runtime compatibility rules are implemented by this document):

- same species: potentially viable when age, fertility, health, and social rules permit;
- different species in the same family: unknown until explicitly defined;
- different rig families: incompatible by default;
- hybrids: never generated implicitly from visual resemblance;
- unsupported or unknown pairings: reject safely without partial state changes.

A compatible pair still needs a valid reproductive state, suitable anatomy, age, fertility, health, and environmental conditions. A family-level rig is not a biological compatibility claim.

## Conditions and health outcomes

Future breeding results should allow more than success or failure:

- viable offspring or hatchlings;
- miscarriage, stillbirth, or failed incubation;
- small or large litter/clutch;
- congenital condition;
- reduced fertility;
- developmental delay;
- maternal or parental health cost;
- offspring needing care;
- sterile hybrid;
- unknown outcome awaiting later rules.

These are simulation outcomes and should be recorded as durable state when the breeding milestone is implemented. They must not be added to current saves or runtime behavior as part of this reference document.

## Research references

- [OpenStax: Divisions of the Skeletal System](https://openstax.org/books/anatomy-and-physiology/pages/7-1-divisions-of-the-skeletal-system) — axial and appendicular skeleton, human bone groups, and the distinction between stable lower-limb support and mobile upper-limb function.
- [Smithsonian Human Origins: Walking Upright](https://humanorigins.si.edu/human-characteristics/walking-upright) — human spine, pelvis, lower-limb, and skull alignment associated with bipedal locomotion.
- [Smithsonian Human Origins: Homo sapiens](https://humanorigins.si.edu/evidence/human-fossils/species/homo-sapiens) — modern human skeletal reference context.
- [FAO: State of the World’s Animal Genetic Resources](https://www.fao.org/docrep/pdf/010/a1250e/a1250e.pdf) — genetic diversity, selection, effective population size, and inbreeding considerations.
- [FAO: Animal Genetic Resources](https://www.fao.org/4/t0284e/T0284E06.htm) — population size, genetic drift, inbreeding, and adaptation.
- [MSD Veterinary Manual: Congenital and Inherited Anomalies in Animals](https://www.msdvetmanual.com/generalized-conditions/congenital-and-inherited-anomalies/overview-of-congenital-and-inherited-anomalies-in-animals) — inherited patterns, congenital outcomes, carrier traits, and species examples.
- [MSD Veterinary Manual: Breeding Soundness Examination in Dogs and Cats](https://www.msdvetmanual.com/management-and-nutrition/management-of-reproduction-dogs-and-cats/the-breeding-soundness-examination-in-dogs-and-cats) — health, physical, genetic, and behavioral checks relevant to future breeding rules.

## Scope boundary

This document is not a breeding design and does not authorize runtime implementation. Before implementation, a later milestone issue must define:

- the first playable breeding loop;
- valid species pairs;
- pregnancy or incubation state transitions;
- inheritance and appearance rules;
- condition and risk rules;
- offspring body generation and rig selection;
- save/load schema and deterministic tests;
- welfare, failure, and recovery behavior.

Until then, human anatomy and breeding data remain reference material for future rig, species, population, and genetics work.
