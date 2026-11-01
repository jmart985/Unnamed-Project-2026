namespace Simulation;

public sealed record ItemDefinition(string Id, double WeightKg, string? EquipmentSlot = null);

public static class EquipmentSlotIds
{
    public const string Tool = "tool";
    public const string Torso = "torso";
    public const string Legs = "legs";
    public const string Head = "head";
    public const string Hands = "hands";
    public const string Feet = "feet";
    public const string Accessory = "accessory";
    public const string Outerwear = "outerwear";

    public static bool SupportsVisibility(string? slotId) => slotId is Tool or Torso or Legs or Head or Hands or Feet or Accessory or Outerwear;
}

/// <summary>Simulation-facing physical properties for item types.</summary>
public static class ItemCatalog
{
    private static readonly IReadOnlyDictionary<string, ItemDefinition> Definitions = BuildDefinitions();

    private static IReadOnlyDictionary<string, ItemDefinition> BuildDefinitions()
    {
        var definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
        {
            [ItemIds.Food] = new(ItemIds.Food, 0),
            [ItemIds.Plant] = new(ItemIds.Plant, 0),
            [ItemIds.Meat] = new(ItemIds.Meat, 0),
            [ItemIds.Fruit] = new(ItemIds.Fruit, 0),
            [ItemIds.Wood] = new(ItemIds.Wood, 0),
            [ItemIds.Rock] = new(ItemIds.Rock, 0),
            [ItemIds.Axe] = new(ItemIds.Axe, 1.5),
            [ItemIds.Coin] = new(ItemIds.Coin, 0),
            [ItemIds.WorkShirt] = new(ItemIds.WorkShirt, 0.7, EquipmentSlotIds.Torso),
            [ItemIds.WorkTrousers] = new(ItemIds.WorkTrousers, 0.9, EquipmentSlotIds.Legs),
            [ItemIds.DeluxeBunnySuitTop] = new(ItemIds.DeluxeBunnySuitTop, 0.8, EquipmentSlotIds.Torso),
            [ItemIds.DeluxeBunnySuitLegs] = new(ItemIds.DeluxeBunnySuitLegs, 0.65, EquipmentSlotIds.Legs)
        };
        foreach (var entry in PlayerAppearanceCatalog.Default.Entries.Where(entry => entry.Slot != "hair"))
            definitions.TryAdd(entry.Id, new(entry.Id, entry.WeightKg, entry.Slot));
        return definitions;
    }

    public static bool Contains(string? itemId) =>
        itemId is not null && Definitions.ContainsKey(itemId);

    public static ItemDefinition GetRequired(string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        return Definitions.TryGetValue(itemId, out var definition)
            ? definition
            : throw new ArgumentException($"Unknown item '{itemId}'.", nameof(itemId));
    }

    public static double GetWeightKg(string itemId) => GetRequired(itemId).WeightKg;

    public static bool IsWearable(string? itemId) =>
        itemId is not null && Definitions.TryGetValue(itemId, out var definition)
        && definition.EquipmentSlot is not null;
}
