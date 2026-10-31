# Blender male player body

The male player uses `GEO-body_male_realistic` from Blender Human Base Meshes
v1.4.1, preserved in `blender_male_base_source_v1.blend`. It is the male
counterpart of the female player's realistic Blender base.

The source's 10,582 body vertices receive only uniform normalization to 1.75 m
and centering above the floor. The body is not reshaped to fit the old elf rig.
`player_male_base_v1.py` instead fits the existing canonical bone hierarchy to
the source mesh's sculpt face-set boundary loops, including shoulders, elbows,
wrists, fingers, hips, knees, and ankles. These source face sets are authoring
data, separate from simulation anatomy IDs. Locomotion and attack clips are
rebuilt for the fitted rest skeleton.

The work shirt and trousers are duplicate/offset shells of the source surface.
Shirt cuts operate on a temporary body copy. Source face sets define garment
coverage independently of heat-weight thresholds, preventing detached wrist
fragments and gaps at the waist. The existing fitted fabric thickness remains
3–10 mm for the shirt and 10 mm for trousers. Their item IDs, runtime mesh names,
equipment slots, and saved visibility controls are retained.

Regenerate only the male assets with the installed Blender executable:

```powershell
blender --background --factory-startup --disable-autoexec --python art_source/reference/generated_humanoids/build_player_humanoid_assets_v1.py -- male
```

The builder checks every normalized source vertex before export, then checks the
GLB's world-space body positions against that body. Both checks must pass;
matching filenames or vertex counts alone are insufficient. It also checks
normalized skin weights, exported hand anchors, closed garment shells, and
clothing skin data. A partial rebuild preserves the other body's fit report.

The editable result is `player_human_male_humanoid_rig_v1.blend`; the runtime
result is `game/assets/humanoids/human/player_male_humanoid_rig_v1.glb`. Existing
front, side, and back preview files show the regenerated body and workwear.

After Godot import, run `--validate-player-workwear`. It checks the male body
geometry bounds as well as both bodies' locomotion, attack layering, equipment
swaps, independent visibility, and save/load. In game, create a male player and
check the shoulders and waist while idle, walking, running, and striking with
both arms. Toggle torso/legs visibility and reload a save to inspect the body
and outfit independently.
