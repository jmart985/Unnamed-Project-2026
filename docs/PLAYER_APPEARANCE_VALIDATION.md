# Player appearance validation

Issue #542 adds selectable clean Blender bases and independent equipment presentations. The simulation owns equipment and inventory; Godot resources describe visuals. See [character creation and authoring](CHARACTER_CREATION_AND_BODY.md) for the resource/mask conventions and source rebuild commands.

## Automated checks

```powershell
dotnet build UnnamedProject2026.slnx --configuration Release
dotnet test UnnamedProject2026.slnx --configuration Release --no-build
dotnet build game/UnnamedProject2026.csproj
godot --headless --editor --path game --quit
godot --headless --path game -- --validate-character-appearance
godot --headless --path game -- --validate-player-workwear
godot --headless --path game -- --validate-session-profiles
```

The registered appearance scenario is included in `scripts/validate-godot-scenarios.sh`. Run that manifest for the complete Godot regression gate, with `GODOT_BIN` set to the pinned .NET engine.

Coverage includes:

- Body-compatible IDs, invalid selections, default-off anatomy and schema-25 migration.
- Independent boots, gloves, torso and jacket equipment, atomic swaps, carried items and hidden-slot persistence.
- Every registered wearable presentation attaching to the single player skeleton.
- Actual world-space hand/eye anchors, common actions and transferred facial movement.
- Combined visible-item coverage, restoration after hiding/unequipping, and material isolation between characters.
- Bounded cloth springs after movement, settling, teleport reset and scaled presentation.
- Moving production PlayerCharacter visuals through equipment changes and save/load, in addition to the creation preview.

`validate_player_wardrobe.py` checks the generated GLBs independently of Godot: one intended mesh and skin, finite attributes, normalized four-weight skinning, wardrobe bounds, separate lower-hem chains and measured starter-shell thickness. Rigify reports in `art_source/characters/player_*_controls.json` measure neutral-pose preservation, hand FK, foot IK, cloth controls and visual baking.

## Rendered inspection

Use a graphical renderer for captures; headless dummy-renderer checks cannot establish material or garment appearance.

```powershell
godot --path game -- --validate-character-appearance --capture-appearance 'docs/evidence/player-creation'
```

Godot resolves that relative directory beneath `game/`. The capture includes a thumbnail of every catalog variant under `wardrobe/`, front/side/back views, creation menus, loose garments and a shirt-plus-jacket view. Review geometry, silhouette, skin coverage, texture colors, hair opacity, garment openings and sleeve placement. A structurally valid rig is insufficient evidence of a usable fit.

## Playtest

1. Start normally. Select each sex and inspect the clean body with all equipment set to None; anatomical attachments must initially be hidden.
2. Choose hair, a work shirt, trousers and compatible footwear/gloves. On the female base, add a jacket independently of the shirt.
3. Turn the preview and play idle, walk, run, wave and expressions. Facial clips should retain a relaxed body posture.
4. Confirm creation, move, look and attack. Open Inventory. Equipped items belong in their slots; unequipped clothing belongs in Items.
5. Toggle individual eye buttons. Hiding boots or a jacket must preserve other slots, inventory ownership and the remaining body coverage.
6. Unequip the jacket, verify it appears in Items and that the shirt remains worn, then equip it again. Save/load and confirm the current equipment and visibility states are restored.

## Practical limits

The source body detail is retained; runtime LODs and a population-performance optimization pass are not part of this change. Facial morphs are approximated on supported control bones. Cloth springs use simplified leg clearance, without fabric self-collision or terrain collision. Source pieces with fused clothing regions remain combined items; they are not automatically cut into independently authored subgarments. Additional proportions and extreme poses still need visual fit review.

## Recorded verification — 2026-10-31

- Release solution build and Debug Godot build: zero warnings and errors.
- Simulation suite: 746 passed, zero failed/skipped.
- All 27 registered headless Godot scenarios passed, including moving-player equipment/save-load and the new appearance scenario.
- Export audit: all 116 wardrobe variants passed mesh, skin, finite-attribute, bounds and catalogue-parity checks. The eight starter shells retained approximately 3.08–3.18 mm median exported fabric thickness.
- Original-body reference alignment: maximum recorded error below 0.000003 m across the 46 NPC-derived variants.
- Rigify: both 91-bone deform rigs passed neutral-pose, hand FK, foot IK, cloth-control and visual-bake checks; each authoring control rig has 544 bones.
- Source orientation was checked across 18 input sets, with anatomical left/right checks wherever paired arm joints exist. This caught the reversed Yuffie intake orientation.
- Graphical validation captured all 116 variants plus front/side/back, menus, loose garments and layered shirt/jacket views. See [inspection images](evidence/player-creation/).

Godot 4.7.2 emitted ObjectDB/RID cleanup warnings at shutdown. Required pass markers and successful process exits were checked separately; these runs are not described as warning-free engine shutdowns.
