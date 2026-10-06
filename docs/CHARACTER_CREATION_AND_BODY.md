# Character creation and body customization

This document assigns the requested Sims-style creation work to the correct milestones. It expands the existing name/height/body-type/sex creation form without making free-form mesh editing part of the prototype.

## Milestone assignments

### 0.2 Character Core — authoritative body parameters

Create validated simulation data for:

- height;
- weight/body mass;
- muscle or build definition;
- normalized body-part scale values on X, Y, and Z;
- derived movement, carry, endurance, collision, and animation proportions.

The domain model owns the values and clamps them to documented ranges. It does not render UI or choose art assets. Human and creature definitions may expose different allowed ranges and body-part sets.

### 0.4 Basic Settlement — Build mode entry point

Build mode is accessed through the Settlement menu. This milestone owns the settlement placement workflow:

- open/close Build mode;
- direct-placement hotbar/ribbon selection of approved build definitions while the player moves in the world;
- a later Sims-style planning-camera workflow for settlement layouts, tracked as separate build-mode work;
- ghost blueprint placement;
- structural layer progress;
- linked storage/material delivery;
- completed object menus.

Character creation controls do not belong in the settlement Build mode. Build mode may display the selected builder’s capabilities, but it must not edit body proportions.

### 1.0 First Real Game — player creation experience

The player creation form exposes the validated Character Core parameters through readable sliders and presets:

- weight;
- height;
- muscle/build;
- sex and existing profile choices;
- optional advanced body-part X/Y/Z controls after the base sliders are stable.

The form previews the same player rig used in the simulation. Confirming creates one authoritative player profile; cancel/reset leaves no partial World mutation. The profile persists through save/load.

## Body-part editing rules

- X, Y, and Z controls represent scale or proportion for a named body part, not arbitrary vertex editing.
- Each part has a documented minimum and maximum, symmetry policy, and valid species/body-category set.
- Default human parts include head, neck, torso, upper/lower arms, hands, upper/lower legs, and feet. Species-specific parts such as tails are defined by the species profile.
- The system derives collision and joint positions from the approved proportions. It does not allow body edits that produce zero-sized parts, detached joints, invalid collision, negative scale, or impossible locomotion.
- Rendering, animation, and collision adapters consume the same authoritative proportions. There is no separate UI-only body state.
- Body customization changes appearance and derived physical values within bounded rules; it does not directly grant arbitrary skills or bypass species capabilities.

## Issue sequence

1. **Character body-parameter model (0.2):** validated height, weight, muscle, named body parts, X/Y/Z bounds, symmetry, serialization, and derived stat tests.
2. **Body-profile to rig projection (0.2):** map approved profiles to the existing jointed human/creature rigs, collision, camera height, and animation proportions.
3. **Direct-placement Build mode shell (0.4):** open/close mode from Settlement, player-relative ribbon selection, cancel isolation, and mode/input tests.
4. **Build ghost and staged structure projection (0.4):** preview frames, placement validation, structural layers, progress, and linked storage hooks.
5. **Settlement planning camera (0.4 follow-up):** explicit enter/exit, detached camera bounds, multi-blueprint placement, input capture, and return-to-player tests.
6. **Player creation sliders (1.0):** weight, height, muscle/build, existing profile fields, preview, validation, and atomic confirmation.
7. **Advanced body-part controls (1.0 follow-up):** bounded X/Y/Z controls, symmetry toggle where allowed, reset-to-profile, preview, and persistence.
8. **Character creation acceptance gate (1.0):** repeatable creation/save/load/reset scenario across supported profiles and body categories.

Keep each issue independent and testable. The 0.4 Build mode issues should not be blocked on the full 1.0 character-customization UI; they only consume validated capabilities when needed for placement/work rules.

