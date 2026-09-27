using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation;

public sealed class ItemCatalog
{
    private readonly Dictionary<string, ItemDefinition> _definitions;
    private readonly ReadOnlyCollection<ItemDefinition> _definitionView;

    public ItemCatalog(IEnumerable<ItemDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (!_definitions.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException($"Duplicate item ID '{definition.Id}'.", nameof(definitions));
            }
        }

        _definitionView = Array.AsReadOnly(_definitions.Values.ToArray());
    }

    public static ItemCatalog Default { get; } = LoadDefault();

    public IReadOnlyCollection<ItemDefinition> Definitions => _definitionView;

    public static ItemCatalog FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        ItemCatalogDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ItemCatalogDocument>(json);
        }
        catch (JsonException exception)
        {
            throw new FormatException("The item catalog JSON is invalid.", exception);
        }

        if (document is null)
        {
            throw new FormatException("The item catalog JSON must contain an object.");
        }
        if (document.Items is null)
        {
            throw new FormatException("The item catalog JSON must contain an 'items' array.");
        }

        return new ItemCatalog(document.Items);
    }

    public bool Contains(string? itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && _definitions.ContainsKey(itemId);
    }

    private static ItemCatalog LoadDefault()
    {
        using var stream = typeof(ItemCatalog).Assembly
            .GetManifestResourceStream("Simulation.items.json")
            ?? throw new InvalidOperationException("The embedded item catalog resource is missing.");
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }

    private sealed class ItemCatalogDocument
    {
        [JsonPropertyName("items")]
        public List<ItemDefinition>? Items { get; init; }
    }
}
