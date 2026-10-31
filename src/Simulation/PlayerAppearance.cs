using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation;

/// <summary>Stable content IDs only. Rig nodes, file paths and animation state belong to presentation.</summary>
public sealed record PlayerAppearance(
    bool ShowGenitalia = false,
    string Hair = "none",
    string Head = "none",
    string Torso = ItemIds.WorkShirt,
    string Legs = ItemIds.WorkTrousers,
    string Hands = "none",
    string Feet = "none",
    string Accessory = "none",
    string Outerwear = "none")
{
    public static PlayerAppearance Bare { get; } = new(Torso: "none", Legs: "none");
    [JsonIgnore]
    public IReadOnlyDictionary<string, string> Equipment => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [EquipmentSlotIds.Head] = Head, [EquipmentSlotIds.Torso] = Torso,
        [EquipmentSlotIds.Legs] = Legs, [EquipmentSlotIds.Hands] = Hands,
        [EquipmentSlotIds.Feet] = Feet, [EquipmentSlotIds.Accessory] = Accessory,
        [EquipmentSlotIds.Outerwear] = Outerwear
    }.Where(pair => pair.Value != "none").ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    public bool IsValid(PlayerSex sex) => PlayerAppearanceCatalog.Default.Supports(Hair, "hair", sex)
        && PlayerAppearanceCatalog.Default.Supports(Head, EquipmentSlotIds.Head, sex)
        && PlayerAppearanceCatalog.Default.Supports(Torso, EquipmentSlotIds.Torso, sex)
        && PlayerAppearanceCatalog.Default.Supports(Legs, EquipmentSlotIds.Legs, sex)
        && PlayerAppearanceCatalog.Default.Supports(Hands, EquipmentSlotIds.Hands, sex)
        && PlayerAppearanceCatalog.Default.Supports(Feet, EquipmentSlotIds.Feet, sex)
        && PlayerAppearanceCatalog.Default.Supports(Accessory, EquipmentSlotIds.Accessory, sex)
        && PlayerAppearanceCatalog.Default.Supports(Outerwear, EquipmentSlotIds.Outerwear, sex);
}

public sealed record AppearanceDefinition(
    string Id, string Name, string Slot, PlayerSex[] Sexes, bool CoversGenitalia = false, double WeightKg = 0.5);

public sealed class PlayerAppearanceCatalog
{
    private readonly IReadOnlyDictionary<string, AppearanceDefinition> _entries;
    public static PlayerAppearanceCatalog Default { get; } = LoadEmbedded();
    public IReadOnlyCollection<AppearanceDefinition> Entries => _entries.Values.ToArray();

    private PlayerAppearanceCatalog(IEnumerable<AppearanceDefinition> entries)
    {
        var result = new Dictionary<string, AppearanceDefinition>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || entry.Id == "none" || string.IsNullOrWhiteSpace(entry.Name)
                || (entry.Slot != "hair" && !EquipmentSlotIds.SupportsVisibility(entry.Slot))
                || entry.Sexes is not { Length: > 0 } || entry.Sexes.Any(sex => !Enum.IsDefined(sex))
                || entry.Sexes.Distinct().Count() != entry.Sexes.Length
                || !double.IsFinite(entry.WeightKg) || entry.WeightKg < 0 || !result.TryAdd(entry.Id, entry))
                throw new InvalidDataException($"Invalid or duplicate appearance definition '{entry.Id}'.");
        }
        _entries = result;
    }

    public bool Supports(string? id, string slot, PlayerSex sex) => id == "none"
        || (id is not null && _entries.TryGetValue(id, out var definition)
            && definition.Slot == slot && definition.Sexes.Contains(sex));

    public bool TryGet(string id, out AppearanceDefinition? definition) => _entries.TryGetValue(id, out definition);
    public IReadOnlyList<AppearanceDefinition> ForSlot(string slot, PlayerSex sex) =>
        _entries.Values.Where(entry => entry.Slot == slot && entry.Sexes.Contains(sex))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray();

    private static PlayerAppearanceCatalog LoadEmbedded()
    {
        using var stream = typeof(PlayerAppearanceCatalog).Assembly.GetManifestResourceStream("Simulation.PlayerAppearance.json")
            ?? throw new InvalidDataException("The player appearance catalog is missing.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return new(JsonSerializer.Deserialize<AppearanceDefinition[]>(stream, options)
            ?? throw new InvalidDataException("The player appearance catalog is empty."));
    }
}
