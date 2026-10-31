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
- Female/Male sex and existing profile choices; the selected value is copied into the new player's required `EntityRecord.Sex` and is validated against the saved profile on load;
- Starter NPCs retain their ten original sex-associated models; the separately controlled player's Female/Male choice selects the clean Blender bases;
- optional advanced body-part X/Y/Z controls after the base sliders are stable.

The form previews the same skinned Blender base used by the player in the game. Confirming creates one authoritative player profile and a player entity with matching durable biological sex; cancel/reset leaves no partial World mutation. The profile and entity value persist through save/load. Player presentation remains independent of the ten original NPC models.

## Implemented modular creation — issue #542

Normal startup opens creation. Female and Male select the original realistic Blender Human Base Meshes bodies, uniformly normalized to 1.75 m before approved profile proportions. Their authored Multires shape is retained in editable native sources. The supplied bundle has anatomical skeleton meshes but no animation armature; each clean body therefore receives a fitted Humanoid Rig v1, full fingers, optional facial controls and separately skinned genital attachments.

**Show genitalia** defaults off. This persisted appearance option controls only the optional meshes; it is not reproductive simulation state. Visible covering garments suppress those attachments. It does not hide or remove the controlled player, equipment ownership, or the underlying anatomical simulation.

Creation offers sex-filtered Hair, Head/glasses, Torso/outfit, Jacket/coat, Legs, Hands/sleeves, Footwear and Accessory selectors, each with None. The left options scroll while the actual animated preview remains beside them. Turn left/right and Play/replay allow inspection of each movement or expression without adding gameplay key bindings. The preview has its own World3D.

Selected wearable items become authoritative equipped items on confirmation. They are not duplicated in carried inventory. Hair and the anatomical-visibility preference live in the saved profile; current equipment and per-slot eye overrides remain on the entity. Schema 26 migrates older saves with no explicit appearance and with genitalia off, preserving their existing equipment. Asset paths, skin bind indices and active preview clips are never saved.

The wardrobe converter accepts separate humanoid wearable meshes from the ten starter NPCs, Bladedancer, Bunny Akali, Gris, Deluxe Bunny, Radagon and Elf Swordmaster, plus the existing workwear presentations. Original sources remain untouched. UVs/materials and evaluated garment thickness are retained; garments are fitted and weighted to the target body and attached to its single skeleton. A combined body/outfit mesh, a creature, or a rig helper is not a wearable merely because it is present in the asset vault. Every future intake still needs technical classification and conversion before it can appear in these selectors.

The common actions include idle/walk/run, left/right attacks, wave and five authored expressions. Dwight and Yuffie each contribute six facial morph expressions, transferred to the supported facial bones with recorded displacement-fit error. This is an approximation; source-specific facial topology and unsupported controls are not duplicated. These previews do not implement dialogue, reproduction behavior, or a full cloth physics system.

### Blender animation and outfit editing

`art_source/characters/player_male_controls.blend` and `player_female_controls.blend` are the Rigify authoring workspaces. Each contains a fitted basic human metarig extended with full fingers and facial/anatomical controls, the generated IK/FK control rig, the canonical deform rig, the original body and separate starter clothes. Enable Blender's bundled Rigify add-on for its authoring UI. The main `player_male.blend` / `player_female.blend` files remain clean canonical build inputs.

Animate **PlayerControls**; do not edit imported game bone names or weight clothes to widgets. A rest-offset bridge drives HumanoidRigV1 without forwarding Rigify stretch/shear to game bones. The body was skinned first. General outfits receive named, nearest-face interpolated body weights; the starter shells inherit the duplicated body surface's weights and have 3 mm thickness. Shirt and trousers use different offsets to avoid overlapping surfaces at the waist.

Tight outfits use reversible coverage. In Blender the separate starter garments each drive an **Under_...** body vertex group and inverted Mask modifier, tied to the garment's monitor/camera visibility. In Godot each converted garment has an alpha-aware UV coverage texture; the renderer unions only currently visible outfit masks. Hiding, unequipping or replacing a garment recomputes that union and restores uncovered skin. Hair cards, glasses and accessories do not erase skin behind their transparent surfaces. Source body geometry is retained.

Loose skirts, dresses and capes use 11 optional shared cloth bones with Rigify tweak controls. Lower hems use those chains instead of thigh/calf weights; the waist or shoulder stays anchored to pelvis/torso. The common gait actions provide restrained secondary motion. A Godot SkeletonModifier3D adds bounded, damped movement to these cloth bones after animation, with simple leg clearance. It works in skeleton space with approved presentation scaling, resets on teleports and stops when the corresponding item is hidden. It is a spring drape layer, not a cloth solver: fabric self-collision, terrain collision and arbitrary garment-to-garment collision are not implemented. The native workspaces contain a pinned loose outfit, disabled optional Cloth modifier and full-body Collision before body masking. No Blender physics cache is assumed to work inside Godot.

Before exporting a new control-rig action, unhide/select HumanoidRigV1, enter Pose Mode, select its bones and Bake Action with Visual Keying and Clear Constraints on a working copy. Export only that baked deform rig, meshes and logical anchors as GLB. Keep the metarig, widgets and control/bridge bones out of runtime assets. The build helper tests neutral-pose preservation, arm FK, foot IK and visual baking for both bodies and records measured errors in `player_*_controls.json`.

Rebuild using Blender 5.2, with source auto-execution disabled:

```powershell
blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python art_source/blender/build_player_bases.py -- --source '<original human_base_meshes_bundle.blend>'
blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python art_source/blender/build_player_wardrobe.py -- --packs '<extracted CreationPacks>' --npc-sources '<Repo/Assets/Models/hum/src>'
blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python art_source/blender/add_player_rigify.py
blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python art_source/blender/validate_player_wardrobe.py
```

The wardrobe pack directory holds the acquired Bladedancer, Bunny Akali ARP Walk Animation and Gris source folders. The NPC-source directory supplies unmasked body geometry for measured garment clearance, plus the original Aerith hair diffuse/opacity maps because its previous GLB contains an empty transparent hair bake. Matching deform-joint positions align each original body reference to the exported NPC frame; the maximum alignment error is checked and recorded. These reference bodies are never exported as clothing. Provenance and conversion counts are recorded in the runtime manifest and `art_source/characters/wardrobe_report.json`; editable rigs and expression-fit reports are beside it. The current conversion contains 112 choices and 116 sex-specific wearable presentations. Each body has 91 exported bones; its Rigify workspace has 544 control/helper/bridge bones which stay out of runtime exports.

Source normalization is explicit. The starter NPC GLBs already use metres, even when a clothing mask removes their body geometry; never infer their scale from remaining hair or clothing. Clothing-only native sources retain their inspected metre-space placement. The active Bunny Akali body provides its scale reference; backup meshes bound to other source rigs, proxy meshes and control widgets are excluded. Limb/finger retargeting maps deform/tweak helpers and measures joint heads, rather than arbitrary imported bone tails. Torso/head controls retain an anatomical frame without scaling garments by display-bone lengths. Fitted pieces use the source body's measured clearance when adapting to the target; free skirt/cape sections retain their drape. Missing source texture paths are resolved within the same source pack. The exported audit rejects nonfinite data, unintended meshes, unnormalized weights, stale generated catalogue entries and geometry outside the player wardrobe envelope; starter fabric thickness is measured from paired exported shell surfaces.

### Independent equipment resources

`game/data/outfits/*.tres` are editor-authored **EquipableItem** resources. Each specifies a stable ItemId, display name, slot, mesh node names, sex-specific model paths, body coverage textures and optional skirt/cape drape. Model paths load on demand. Gloves maps to the existing `hands` simulation slot; Hair remains cosmetic profile data. Torso, outerwear, legs, feet, hands, head and accessory items can be changed or hidden independently. Jackets/coats use outerwear, preserving a shirt in torso. There is one item per slot; multiple layers within one slot require a future extension. Current source jackets support the female base; unsupported body/slot combinations expose None until a matching conversion exists.

Add a new item to the engine-free `data/player_appearance.json` with its ID, slot, supported sexes, weight and coverage classification, then create the matching `.tres` resource. Use the `custom_` ID prefix for independently authored entries; the converter preserves those registered custom items while replacing its own generated entries. Include converted canonical meshes and matching masks for every supported body. Presentation validates the resource against that definition on startup. The converter creates missing resources but preserves existing inspector edits. Resources do not own an inventory, grant items or change save contracts: World owns quantities, equipment and visibility, and equipment changes use the existing atomic transfer service.

Generated masks use **white = hide skin**, black = retain skin. Enable **InvertSkinMask** to use imported masks with the opposite convention. The renderer unions every visible item's coverage and supplies a separate shader texture; it never replaces the skin albedo with a mask. Removing/hiding an item recomputes coverage from remaining visible items, including boots and gloves. Each character receives its own body materials and combined texture. Transparent garment texels do not erase the skin behind them. Body masks hide rendered fragments, rather than changing the simulation anatomy or permanently deleting the source mesh.

Validate with the .NET suite, `--validate-character-appearance`, `--validate-player-workwear`, and `--validate-session-profiles`. A rendered appearance validation accepts `--capture-appearance '<directory>'` for front/side/back viewport images.

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

