# Original Model-Creation Instructions 01: Rigging, Clothing and Inventory

**Source:** User-provided directions in ChatGPT, 2026-10-31.  
**Status:** Owner-supplied reference. Code snippets are examples and must be corrected, integrated, and tested before use.

> **Project platform invariant:** This is a **3D** game built in **Godot 4 .NET / C#**. Use Node3D, CharacterBody3D, Skeleton3D, MeshInstance3D, 3D materials/physics and spatial shaders throughout. Godot's `shader_type spatial;` is the correct 3D shader declaration; `canvas_item` is for 2D. `Texture2D` describes surface image maps used *on 3D meshes* and does not mean the game is 2D. Do not convert this workflow to Node2D, CharacterBody2D, Sprite2D or 2D physics.

## Blender: skeleton, skinning and clothing

In Blender, rigging anatomical characters with clothing requires a specific workflow to prevent the mesh from tearing, clipping, or stretching unnaturally. The recommended workflow uses Blender's built-in Rigify add-on plus transferred garment weights.

### Step 1: Generate the skeleton (Rigify metarig)

1. Enable Rigify in Blender preferences/extensions (the UI differs by Blender version).
2. In the viewport add `Armature > Human (Meta-Rig)`.
3. In Edit Mode align metarig bones to the male/female body. Enable X-axis mirror to make symmetric adjustments, and preserve natural elbow and knee bends to establish intended flex directions.

### Step 2: Generate rig controls

1. Return to Object Mode.
2. Select the metarig and open Armature Data Properties.
3. Click **Generate Rig**.
4. Rigify builds animator-friendly control widgets. Preserve the project's existing Humanoid Rig v1 runtime mapping rather than making application code dependent on Rigify control bones.

### Step 3: Skin anatomy

1. Select body mesh and shift-select generated rig as active.
2. `Ctrl+P > With Automatic Weights`.
3. In Pose Mode test limbs for deformation; refine weights and corrective shapes as necessary.

### Step 4: Rig outfits using transferred skin weights

Garments are separate meshes. Transfer corresponding body skin weights instead of independently painting unrelated automatic weights.

Owner's supplied steps:
1. Select the clothing mesh first, then the body mesh; establish appropriate armature association and vertex groups.
2. Select body as source and clothing as destination.
3. In Weight Paint Mode choose `Weights > Transfer Weights`.
4. Set **Source Layers Selection: By Name** and **Destination Mapping: Nearest Face Interpolated**.
5. Ensure the clothing is associated with the intended armature and Armature modifier.
6. Test clothing under locomotion/poses and refine weights, clearance, collision, garment thickness, and masks.

**Implementation caution:** The owner's original ordering included `Ctrl+P > With Empty Groups` while clothing and body were selected, and later `Ctrl+P > Armature Deform`. Those are not universal Blender operators for that selection and should not be executed mechanically. Verify active objects, transfer mappings and actual armature parenting in the installed Blender version. Preserve the intended effect: clothing deformation matches body deformation.

## Godot clothing mesh visibility

- Export compatible skinned body and garment meshes to Godot, maintaining skeleton attachment and skin mapping.
- Individual garments may appear as `MeshInstance3D` under or alongside `Skeleton3D`.
- Equip and unequip by toggling garment `Visible` or loading compatible parts dynamically; common skeleton binding keeps animation synchronized.

## Loose clothing in Godot

Blender Cloth Physics do not automatically export as a working Godot runtime cloth solver.

**Option A: Jiggle/spring bones, preferred when performance matters.** Add clothing secondary-motion bones in Blender. Godot physical-bone or spring/jiggle systems can add controlled sway to skirts/capes. `Skeleton3D > Create Physical Skeleton` creates physical bones/collision but does not itself implement production-ready spring simulation; evaluate current Godot plugins and APIs.

**Option B: SoftBody3D.** Consider for close-up flowing fabric, with pinned vertices and collision as supported. Verify actual cloth-to-armature behavior, geometry binding, physics costs, collision and pinning in the current Godot version; do not assume that simply replacing `MeshInstance3D` with `SoftBody3D` performs all attachment work.

## Data-driven full outfit system

The owner requested three separate systems rather than hardcoding outfit names in a character controller:

1. `OutfitItem` resource: item name, clothing mesh node IDs, body skin clipping mask.
2. Inventory ownership component: owns/unlocks outfits and requests equip.
3. Character equipment component: toggles matching garment meshes and applies a per-character body mask.

Supplied baseline example:

```csharp
using Godot;

[GlobalClass]
public partial class OutfitItem : Resource
{
    [Export] public string OutfitName { get; set; } = "New Outfit";
    [Export] public Godot.Collections.Array<string> MeshNodeNames { get; set; } = new();
    [Export] public Texture2D BodySkinMask { get; set; }
}
```

```csharp
using Godot;
using System.Collections.Generic;

public partial class CharacterEquipment : Node3D
{
    [Export] public MeshInstance3D BodyMesh;
    [Export] public Skeleton3D CharacterSkeleton;
    [Export] public OutfitItem EquippedOutfit;

    private List<MeshInstance3D> _allClothingMeshes = new();

    public override void _Ready()
    {
        foreach (var child in CharacterSkeleton.GetChildren())
        {
            if (child is MeshInstance3D clothingMesh && child != BodyMesh)
                _allClothingMeshes.Add(clothingMesh);
        }
        if (EquippedOutfit != null) Equip(EquippedOutfit);
    }

    public void Equip(OutfitItem newOutfit)
    {
        if (newOutfit == null) return;
        EquippedOutfit = newOutfit;

        if (BodyMesh != null && newOutfit.BodySkinMask != null)
        {
            if (BodyMesh.GetActiveMaterial(0) is StandardMaterial3D bodyMat)
            {
                bodyMat.Transparency = BaseMaterial3D.TransparencyMode.AlphaDepthPrepass;
                bodyMat.TextureAlbedo = newOutfit.BodySkinMask;
            }
        }

        foreach (var mesh in _allClothingMeshes)
            mesh.Visible = newOutfit.MeshNodeNames.Contains(mesh.Name);
    }
}
```

**Important:** The supplied baseline overwrites `TextureAlbedo` with a masking texture, thereby replacing albedo color with mask data. Do not adopt that as a completed clipping shader. Use a dedicated alpha-mask channel or ShaderMaterial and per-instance resources.

The proposed inventory manager included a typo in the original: `public List<OutfitItem> OwnedOutfits { get; private float; } = new();`. Correct it to `private set` in any actual implementation, and use the existing authoritative inventory/equipment/save systems instead of creating conflicting duplicate state.

```csharp
using Godot;
using System.Collections.Generic;

public partial class InventoryManager : Node
{
    [Export] public CharacterEquipment EquipmentController;
    public List<OutfitItem> OwnedOutfits { get; private set; } = new();

    public void AddToInventory(OutfitItem item)
    {
        if (!OwnedOutfits.Contains(item))
        {
            OwnedOutfits.Add(item);
            GD.Print($"Added {item.OutfitName} to inventory.");
        }
    }

    public void UseOutfitFromInventory(int index)
    {
        if (index >= 0 && index < OwnedOutfits.Count)
        {
            OutfitItem selectedOutfit = OwnedOutfits[index];
            EquipmentController.Equip(selectedOutfit);
            GD.Print($"Successfully equipped: {selectedOutfit.OutfitName}");
        }
    }
}
```

For initial assembly: create an `OutfitItem` resource such as `CasualOutfit.tres`; specify matching garment mesh identifiers (e.g., `JeansMesh`, `TshirtMesh`) and correct body alpha mask; assign scene `BodyMesh`, `Skeleton3D` and equipment references. Keep character materials unique, avoiding unintended shared-material mutations.

## Upgrade: independent item slots

The owner explicitly directed a transition from monolithic outfits to slots so changing boots, gloves or trousers does not unequip other slots.

```csharp
using Godot;

public enum EquipSlot
{
    Head,
    Torso,
    Legs,
    Feet,
    Gloves
}

[GlobalClass]
public partial class EquipableItem : Resource
{
    [Export] public string ItemName { get; set; } = "New Item";
    [Export] public EquipSlot Slot { get; set; } = EquipSlot.Torso;
    [Export] public string MeshNodeName { get; set; }
    [Export] public Texture2D ItemSkinMask { get; set; }
}
```

Maintain `Dictionary<EquipSlot, EquipableItem>` of equipped items and a cache of garment `MeshInstance3D` nodes by stable ID. When equipping, validate ownership and body compatibility, unequip only the conflicting slot, enable that item's mesh and recompute skin masks. On unequip, disable only that slot's mesh, restore previous visible body regions, and update persistent inventory/equipment state. Reject unsupported IDs and apply changes atomically.

The supplied modular example's skin-mask algorithm only handled the torso, overwrote the base albedo, and cleared it on unequip; it is a *design sketch*, not production-ready behavior.

## Layered body clipping shader

For multiple items equipped simultaneously, individual black/white masks should combine so black in any active masking layer hides the corresponding body region. Suggested project asset: `BodyClippingShader.gdshader`.

Owner-supplied shader concept, with Godot shader syntax corrected (the original wrote `shader_type3d`):

```glsl
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_lambert, specular_schlick_ggx, depth_prepass_alpha;

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap;
uniform vec4 skin_color : source_color = vec4(1.0);

uniform sampler2D torso_mask : hint_default_white;
uniform sampler2D legs_mask : hint_default_white;
uniform sampler2D feet_mask : hint_default_white;

void fragment() {
    vec4 albedo_tex = texture(albedo_texture, UV);
    ALBEDO = albedo_tex.rgb * skin_color.rgb;
    float m_torso = texture(torso_mask, UV).r;
    float m_legs = texture(legs_mask, UV).r;
    float m_feet = texture(feet_mask, UV).r;
    float combined_alpha = m_torso * m_legs * m_feet;
    ALPHA = combined_alpha;
    ALPHA_SCISSOR_THRESHOLD = 0.5;
}
```

Before integrating, validate the actual shader uniforms, hint/filter syntax, render modes, alpha-scissor/depth semantics and performance in the project's Godot version. Use an explicit white mask texture for empty slots, not an untested null sampler binding.

Suggested C# integration:

```csharp
private void UpdateBodyMasks()
{
    if (BodyMesh == null) return;
    if (BodyMesh.GetActiveMaterial(0) is not ShaderMaterial shaderMat) return;

    Texture2D torsoTex = _currentEquipment[EquipSlot.Torso]?.ItemSkinMask;
    shaderMat.SetShaderParameter("torso_mask", torsoTex);

    Texture2D legsTex = _currentEquipment[EquipSlot.Legs]?.ItemSkinMask;
    shaderMat.SetShaderParameter("legs_mask", legsTex);

    Texture2D feetTex = _currentEquipment[EquipSlot.Feet]?.ItemSkinMask;
    shaderMat.SetShaderParameter("feet_mask", feetTex);
}
```

This was proposed so boots and armor could hide independent body skin regions simultaneously. Complete the missing glove/head slots, no-item defaults, per-instance shader isolation and correct UV mapping before production use.

## Interactive inventory grid UI

The owner additionally requested an interactive grid menu for equippable inventory items.

- Reusable scene: `Button` with child `TextureRect` for icon and `Label` for name/stack count.
- Script: `InventorySlotUI.cs`.
- Associate a slot with its `EquipableItem` and raise a signal on click.
- Empty slots show an empty label and no icon; item slots expose item name, equipment slot and optional icon/tooltip.

Supplied C# sketch:

```csharp
using Godot;

public partial class InventorySlotUI : Button
{
    [Export] public TextureRect IconTexture;
    [Export] public Label ItemNameLabel;

    public EquipableItem AssociatedItem { get; private set; }

    [Signal] public delegate void ItemPressedEventHandler(EquipableItem item);

    public override void _Ready()
    {
        Pressed += OnPressed;
    }

    public void DisplayItem(EquipableItem item)
    {
        AssociatedItem = item;
        if (item == null)
        {
            IconTexture.Texture = null;
            ItemNameLabel.Text = "Empty";
            TooltipText = "Empty Slot";
            return;
        }

        ItemNameLabel.Text = item.ItemName;
        TooltipText = $"{item.ItemName} ({item.Slot})";
        // IconTexture.Texture = item.Icon; // add explicit icon property if desired
    }

    private void OnPressed()
    {
        if (AssociatedItem != null)
            EmitSignal(SignalName.ItemPressed, AssociatedItem);
    }
}
```

The grid controller itself remains to be implemented and must use existing authoritative inventory/equipment logic. The `ItemPressed` signal's resource argument and C# variant/marshalling compatibility should be tested.

## Acceptance expectations

- Rig/skin base models without destructive geometry loss.
- Garments deform correctly with the body and avoid visible collision/clipping in reference poses.
- Skinned attachments and clothing are modular and independently equippable.
- Layered clipping hides only covered skin without overwriting body albedo.
- Full-body and single-slot outfit choices persist correctly across save/load.
- UI inventory grids reflect real owned items, and clicking items calls validated equipment behavior.
- Reference snippets **must not** be copied verbatim into production without correcting compile issues and Godot/Blender version assumptions.

See also `docs/HUMAN_HUMANOID_RIG_V1.md`, `docs/INTERACTION_ANCHOR_STANDARD.md`, and GitHub issues #522 and #542.
