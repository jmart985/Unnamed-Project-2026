using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation;

public sealed class SpeciesCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
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
            throw new InvalidDataException(
                $"Species data must be a JSON array of valid species records (at '{exception.Path}').",
                exception);
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
            var displayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? id : entry.DisplayName.Trim();
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
            var bodyCategory = string.IsNullOrWhiteSpace(entry.BodyCategory) ? "unspecified" : entry.BodyCategory.Trim();
            var dietaryProfile = entry.DietaryProfile ?? InferDietaryProfile(diet);
            var maxHealth = ValidateStat(id, entry.MaxHealth ?? 100, "maxHealth");
            var strength = ValidateStat(id, entry.Strength ?? 1, "strength");
            var endurance = ValidateStat(id, entry.Endurance ?? 1, "endurance");
            var dexterity = ValidateStat(id, entry.Dexterity ?? 1, "dexterity");
            var smellRange = ValidateStat(id, entry.SmellRange ?? 1, "smellRange");
            var threatDetectionRange = entry.ThreatDetectionRange ?? 0;
            if (!double.IsFinite(threatDetectionRange) || threatDetectionRange < 0)
                throw new InvalidDataException($"Species '{id}' threatDetectionRange must be finite and nonnegative.");
            var temperament = ValidateTemperament(id, entry.Temperament);
            var baseTrust = entry.BaseTrust ?? 0;
            if (!double.IsFinite(baseTrust) || baseTrust is < 0 or > 100)
                throw new InvalidDataException($"Species '{id}' baseTrust must be finite and within [0, 100].");
            var anchors = ValidateAnchors(id, entry);
            var preySpeciesIds = ValidatePreySpeciesIds(id, entry.PreySpeciesIds);
            var carcassMeatUnits = entry.CarcassMeatUnits ?? 1;
            if (carcassMeatUnits <= 0 || carcassMeatUnits > 1000)
                throw new InvalidDataException($"Species '{id}' carcassMeatUnits must be within [1, 1000].");
            var carcassFreshnessSeconds = entry.CarcassFreshnessSeconds ?? 300;
            if (!double.IsFinite(carcassFreshnessSeconds) || carcassFreshnessSeconds <= 0)
                throw new InvalidDataException($"Species '{id}' carcassFreshnessSeconds must be finite and greater than zero.");
            definitions.Add(new SpeciesDefinition(
                id,
                displayName,
                movementSpeed,
                hungerRate,
                diet,
                canHaul,
                bodyCategory,
                dietaryProfile,
                maxHealth,
                strength,
                endurance,
                temperament,
                dexterity,
                smellRange,
                entry.CanUseTools ?? false,
                entry.CanOpenDoors ?? false,
                entry.CanTrack ?? false,
                entry.CanAcceptFriendlyInteraction ?? false,
                entry.IsDomestic ?? false,
                entry.IsPredator ?? dietaryProfile == DietaryProfile.Carnivore,
                baseTrust,
                anchors,
                preySpeciesIds,
                carcassMeatUnits,
                threatDetectionRange,
                carcassFreshnessSeconds));
        }

        var knownSpeciesIds = ids;
        foreach (var definition in definitions)
        foreach (var preySpeciesId in definition.PreySpeciesIds)
            if (!knownSpeciesIds.Contains(preySpeciesId))
                throw new InvalidDataException($"Species '{definition.Id}' references unknown prey species ID '{preySpeciesId}'.");

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

    private static string[] ValidatePreySpeciesIds(string speciesId, string[]? preySpeciesIds)
    {
        if (preySpeciesIds is null || preySpeciesIds.Length == 0)
            return Array.Empty<string>();

        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var preySpeciesId in preySpeciesIds)
        {
            var validatedId = ValidateId(preySpeciesId);
            if (!uniqueIds.Add(validatedId))
                throw new InvalidDataException($"Species '{speciesId}' lists prey species ID '{validatedId}' more than once.");
        }

        return preySpeciesIds;
    }

    private static TemperamentProfile ValidateTemperament(string speciesId, TemperamentData? temperament)
    {
        if (temperament is null)
        {
            throw new InvalidDataException($"Species '{speciesId}' must define temperament.");
        }

        return new TemperamentProfile(
            ValidateTemperamentValue(speciesId, "boldness", temperament.Boldness),
            ValidateTemperamentValue(speciesId, "aggression", temperament.Aggression),
            ValidateTemperamentValue(speciesId, "fear", temperament.Fear),
            ValidateTemperamentValue(speciesId, "curiosity", temperament.Curiosity),
            ValidateTemperamentValue(speciesId, "sociability", temperament.Sociability),
            ValidateTemperamentValue(speciesId, "territoriality", temperament.Territoriality));
    }

    private static double ValidateTemperamentValue(string speciesId, string fieldName, double? value)
    {
        if (value is null)
        {
            throw new InvalidDataException($"Species '{speciesId}' must define temperament.{fieldName}.");
        }

        if (!double.IsFinite(value.Value) || value.Value is < 0 or > 1)
        {
            throw new InvalidDataException(
                $"Species '{speciesId}' temperament.{fieldName} must be finite and between 0 and 1.");
        }

        return value.Value;
    }

    private static double ValidateStat(string speciesId, double value, string fieldName)
    {
        if (!double.IsFinite(value) || value <= 0) throw new InvalidDataException($"Species '{speciesId}' {fieldName} must be finite and greater than zero.");
        return value;
    }

    private static InteractionAnchorDefinition ValidateAnchors(string speciesId, SpeciesData entry)
    {
        try
        {
            var anchors = new InteractionAnchorDefinition(
                entry.AnatomyRegions,
                entry.LocomotionAnchors,
                entry.EquipmentAnchors,
                entry.InteractionAnchors,
                entry.BreedingAnchors);

            return anchors;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"Species '{speciesId}' has invalid interaction anchors: {exception.Message}", exception);
        }
    }

    private static DietaryProfile InferDietaryProfile(IEnumerable<string> diet)
    {
        var plant = diet.Any(ItemIds.IsPlantFood);
        var meat = diet.Any(ItemIds.IsMeat);
        return (plant, meat) switch
        {
            (true, false) => DietaryProfile.Herbivore,
            (false, true) => DietaryProfile.Carnivore,
            _ => DietaryProfile.Omnivore
        };
    }

    private static string ValidateRequiredText(string speciesId, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Species '{speciesId}' must define {fieldName}.");
        }

        return value.Trim();
    }

    private sealed class SpeciesData
    {
        public string? Id { get; init; }
        public string? DisplayName { get; init; }
        public double? MovementSpeed { get; init; }
        public double? HungerRate { get; init; }
        public string[]? Diet { get; init; }
        public bool? CanHaul { get; init; }
        public string? BodyCategory { get; init; }
        public DietaryProfile? DietaryProfile { get; init; }
        public double? MaxHealth { get; init; }
        public double? Strength { get; init; }
        public double? Endurance { get; init; }
        public double? Dexterity { get; init; }
        public double? SmellRange { get; init; }
        public double? ThreatDetectionRange { get; init; }
        public bool? CanUseTools { get; init; }
        public bool? CanOpenDoors { get; init; }
        public bool? CanTrack { get; init; }
        public bool? CanAcceptFriendlyInteraction { get; init; }
        public bool? IsDomestic { get; init; }
        public bool? IsPredator { get; init; }
        public double? BaseTrust { get; init; }
        public string[]? PreySpeciesIds { get; init; }
        public int? CarcassMeatUnits { get; init; }
        public double? CarcassFreshnessSeconds { get; init; }
        public string[]? AnatomyRegions { get; init; }
        public string[]? LocomotionAnchors { get; init; }
        public string[]? EquipmentAnchors { get; init; }
        public string[]? InteractionAnchors { get; init; }
        public string[]? BreedingAnchors { get; init; }
        public TemperamentData? Temperament { get; init; }
    }

    private sealed class TemperamentData
    {
        [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
        public double? Boldness { get; init; }

        [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
        public double? Aggression { get; init; }

        [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
        public double? Fear { get; init; }

        [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
        public double? Curiosity { get; init; }

        [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
        public double? Sociability { get; init; }

        [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
        public double? Territoriality { get; init; }
    }
}
