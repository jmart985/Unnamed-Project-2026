using Godot;
using Simulation;

namespace UnnamedProject2026;

// Numeric values are serialized in .tres resources; retain existing assignments.
public enum EquipSlot { Head = 0, Torso = 1, Legs = 2, Feet = 3, Gloves = 4, Accessory = 5, Hair = 6, Outerwear = 7 }
public enum GarmentDrape { None = 0, Skirt = 1, Cape = 2 }

/// <summary>Editor-authored presentation data; World owns item quantities and equipment.</summary>
[GlobalClass]
public partial class EquipableItem : Resource
{
    [Export] public string ItemId { get; set; } = string.Empty;
    [Export] public string ItemName { get; set; } = "New item";
    [Export] public EquipSlot Slot { get; set; } = EquipSlot.Torso;
    [Export] public Godot.Collections.Array<string> MeshNodeNames { get; set; } = [];
    [Export(PropertyHint.File, "*.glb,*.tscn")] public string MaleModelPath { get; set; } = string.Empty;
    [Export(PropertyHint.File, "*.glb,*.tscn")] public string FemaleModelPath { get; set; } = string.Empty;
    [Export] public Texture2D? ItemSkinMask { get; set; }
    [Export] public Texture2D? MaleSkinMask { get; set; }
    [Export] public Texture2D? FemaleSkinMask { get; set; }
    [Export] public bool InvertSkinMask { get; set; }
    [Export] public GarmentDrape Drape { get; set; }

    public string SlotId => Slot switch
    {
        EquipSlot.Head => EquipmentSlotIds.Head, EquipSlot.Torso => EquipmentSlotIds.Torso,
        EquipSlot.Legs => EquipmentSlotIds.Legs, EquipSlot.Feet => EquipmentSlotIds.Feet,
        EquipSlot.Gloves => EquipmentSlotIds.Hands, EquipSlot.Accessory => EquipmentSlotIds.Accessory,
        EquipSlot.Hair => "hair", EquipSlot.Outerwear => EquipmentSlotIds.Outerwear,
        _ => throw new InvalidDataException("Unsupported presentation slot.")
    };
    public string ModelPath(PlayerSex sex) => sex == PlayerSex.Male ? MaleModelPath : FemaleModelPath;
    public Texture2D? SkinMask(PlayerSex sex) =>
        (sex == PlayerSex.Male ? MaleSkinMask : FemaleSkinMask) ?? ItemSkinMask;

    internal void Validate(AppearanceDefinition definition)
    {
        if (ItemId != definition.Id || SlotId != definition.Slot || string.IsNullOrWhiteSpace(ItemName)
            || !Enum.IsDefined(Drape) || MeshNodeNames is null || MeshNodeNames.Count == 0
            || MeshNodeNames.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException($"Invalid presentation resource for '{definition.Id}'.");
        foreach (var sex in definition.Sexes)
            if (string.IsNullOrEmpty(ModelPath(sex)) || !ModelPath(sex).StartsWith("res://assets/", StringComparison.Ordinal)
                || !ResourceLoader.Exists(ModelPath(sex)) || SkinMask(sex) is null)
                throw new InvalidDataException($"Missing {sex} mesh or mask for '{ItemId}'.");
    }
}
