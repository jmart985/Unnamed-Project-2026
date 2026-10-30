# Deluxe Bunny player outfit

This is the first clothing-pack outfit wired into the player's equipment and
save/load flow. The source archive remains at
`Repo/Assets/Models/cloth/RyanReos_DeluxeBunnysuit.zip`; the project-side
reference scene and the maps needed to rebuild the adaptation are stored here.

## Runtime adaptation

The outfit uses the existing Humanoid Rig v1 player body and equipment slots.
The pack's source bunny ears are adapted to the canonical `head` bone. Its
jacket, vest, collar, cuffs, leotard, and stockings are preserved in the source
scene for future modeling, but are not used as the player garment surfaces:
their fit did not remain clean across both project body shapes. The runtime
top and leggings instead copy the project-measured skinned shirt and trouser
meshes and use the pack's purple, charcoal, and brass palette. This preserves
the already verified body clearance and skin weights for both player variants.

The simulation item IDs are `deluxe_bunny_suit_top` (torso slot) and
`deluxe_bunny_suit_legs` (legs slot). A new player receives one of each in
carried inventory while the work shirt and trousers remain equipped. Equipping
the outfit returns the replaced garment to inventory. The existing snapshot
fields serialize the item IDs, so no save schema change is needed.

## Files

- `deluxe_bunny_suit_source_v1.blend` — curated source outfit and body reference.
- `textures/` — source maps packed for material review and rebuilding.
- `prepare_source_v1.py` — repeatable source curation from the archive.
- `build_deluxe_bunny_suit_v1.py` — adds the adapted outfit to both player rigs,
  validates skinning, and writes the fit report.
- `deluxe_bunny_suit_fit_v1.json` — source and runtime node mapping.
- `game/assets/items/clothing/deluxe_bunny_suit_{top,legs}_{male,female}.glb`
  — standalone sex-specific equipment assets.
- `game/assets/humanoids/human/player_{male,female}_humanoid_rig_v1.glb` — player
  scenes with the outfit nodes included and hidden until equipped.

Rebuild with Blender 5.x using:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' `
  --background --factory-startup `
  --python art_source/reference/generated_humanoids/build_player_humanoid_assets_v1.py
```

The Godot `--validate-player-workwear` scenario verifies both player variants,
equipment visibility, outfit swapping, and outfit save/load restoration.
