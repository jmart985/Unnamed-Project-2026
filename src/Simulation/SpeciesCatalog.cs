using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation;

public sealed class SpeciesCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly Dictionary<string, SpeciesDefinition> _byId;
    private readonly ReadOnlyCollection<SpeciesDefinition> _species;

    private SpeciesCatalog(IEnumerable<SpeciesDefinition> species)
    {
        var orderedSpecies = species.ToArray();
        _byId = orderedSpecies.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
        _species = Array.AsReadOnly(orderedSpecies);
    }

    public IReadOnlyCollection<SpeciesDefinition> Species => _species;

    public static SpeciesCatalog LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A species data file path is required.", nameof(path));
        }

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static SpeciesCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        SpeciesData?[]? entries;
        try
        {
            entries = JsonSerializer.Deserialize<SpeciesData?[]>(stream, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Species data must be a JSON array of valid species records.", exception);
        }

        if (entries is null || entries.Length == 0)
        {
            throw new InvalidDataException("Species data must contain at least one species record.");
        }

        var definitions = new List<SpeciesDefinition>(entries.Length);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                throw new InvalidDataException("Species data cannot contain a null species record.");
            }

            var id = ValidateId(entry.Id);
            if (!ids.Add(id))
            {
                throw new InvalidDataException($"Species ID '{id}' is defined more than once.");
            }

            var movementSpeed = entry.MovementSpeed
                ?? throw new InvalidDataException($"Species '{id}' must define movementSpeed.");
            if (!double.IsFinite(movementSpeed) || movementSpeed <= 0)
            {
                throw new InvalidDataException($"Species '{id}' movementSpeed must be finite and greater than zero.");
            }

            var hungerRate = entry.HungerRate
                ?? throw new InvalidDataException($"Species '{id}' must define hungerRate.");
            if (!double.IsFinite(hungerRate) || hungerRate < 0)
            {
                throw new InvalidDataException($"Species '{id}' hungerRate must be finite and nonnegative.");
            }

            var diet = ValidateDiet(id, entry.Diet);
            var canHaul = entry.CanHaul
                ?? throw new InvalidDataException($"Species '{id}' must define canHaul.");
            definitions.Add(new SpeciesDefinition(id, movementSpeed, hungerRate, diet, canHaul));
        }

        return new SpeciesCatalog(definitions);
    }

    public bool TryGet(string? speciesId, out SpeciesDefinition? species)
    {
        if (string.IsNullOrWhiteSpace(speciesId))
        {
            species = null;
            return false;
        }

        return _byId.TryGetValue(speciesId, out species);
    }

    public SpeciesDefinition GetRequired(string? speciesId)
    {
        if (TryGet(speciesId, out var species) && species is not null)
        {
            return species;
        }

        throw new KeyNotFoundException(
            string.IsNullOrWhiteSpace(speciesId)
                ? "A species ID is required."
                : $"Species '{speciesId}' is not defined.");
    }

    public IReadOnlyCollection<SpeciesDefinition> GetCompatibleWithDiet(string? itemId)
    {
        if (!ItemIds.IsKnown(itemId))
        {
            throw new ArgumentException($"Unknown item ID '{itemId}'.", nameof(itemId));
        }

        return Array.AsReadOnly(_species.Where(species => species.Eats(itemId)).ToArray());
    }

    public IReadOnlyCollection<SpeciesDefinition> GetHaulers()
    {
        return Array.AsReadOnly(_species.Where(species => species.CanHaul).ToArray());
    }

    private static string ValidateId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)
            || id[0] == '-'
            || id[^1] == '-')
        {
            throw new InvalidDataException("Each species must have a valid lowercase species ID.");
        }

        var previousWasSeparator = false;
        foreach (var character in id)
        {
            if (character == '-')
            {
                if (previousWasSeparator)
                {
                    throw new InvalidDataException($"Species ID '{id}' is invalid.");
                }

                previousWasSeparator = true;
                continue;
            }

            if (character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9'))
            {
                throw new InvalidDataException($"Species ID '{id}' must use lowercase letters, digits, and single hyphens.");
            }

            previousWasSeparator = false;
        }

        return id;
    }

    private static string[] ValidateDiet(string speciesId, string[]? diet)
    {
        if (diet is null || diet.Length == 0)
        {
            throw new InvalidDataException($"Species '{speciesId}' must define at least one diet item.");
        }

        var uniqueItems = new HashSet<string>(StringComparer.Ordinal);
        foreach (var itemId in diet)
        {
            if (!ItemIds.IsKnown(itemId))
            {
                throw new InvalidDataException(
                    $"Species '{speciesId}' references unknown diet item ID '{itemId}'.");
            }

            if (!uniqueItems.Add(itemId))
            {
                throw new InvalidDataException(
                    $"Species '{speciesId}' defines diet item ID '{itemId}' more than once.");
            }
        }

        return diet;
    }

    private sealed class SpeciesData
    {
        public string? Id { get; init; }
        public double? MovementSpeed { get; init; }
        public double? HungerRate { get; init; }
        public string[]? Diet { get; init; }
        public bool? CanHaul { get; init; }
    }
}
