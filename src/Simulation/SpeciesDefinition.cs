using System.Collections.ObjectModel;

namespace Simulation;

public enum DietaryProfile { Herbivore, Carnivore, Omnivore }

public sealed class SpeciesDefinition
{
    private readonly HashSet<string> _dietItems;

    internal SpeciesDefinition(
        string id,
        string displayName,
        double movementSpeed,
        double hungerRate,
        IEnumerable<string> diet,
        bool canHaul,
        string bodyCategory,
        DietaryProfile dietaryProfile,
        double maxHealth,
        double strength,
        double endurance,
        TemperamentProfile temperament,
        double dexterity = 1,
        double smellRange = 1,
        bool canUseTools = false,
        bool canOpenDoors = false,
        bool canTrack = false,
        bool usesWildTerritory = false,
        bool canAcceptFriendlyInteraction = false,
        bool isDomestic = false,
        bool isPredator = false,
        double baseTrust = 0,
        InteractionAnchorDefinition? anchors = null,
        IEnumerable<string>? preySpeciesIds = null,
        int carcassMeatUnits = 1,
        double threatDetectionRange = 0,
        double carcassFreshnessSeconds = 300)
    {
        Id = id;
        DisplayName = displayName;
        MovementSpeed = movementSpeed;
        HungerRate = hungerRate;
        Diet = Array.AsReadOnly(diet.ToArray());
        CanHaul = canHaul;
        BodyCategory = bodyCategory;
        DietaryProfile = dietaryProfile;
        MaxHealth = maxHealth;
        Strength = strength;
        Endurance = endurance;
        Dexterity = dexterity;
        SmellRange = smellRange;
        CanUseTools = canUseTools;
        CanOpenDoors = canOpenDoors;
        CanTrack = canTrack;
        UsesWildTerritory = usesWildTerritory;
        CanAcceptFriendlyInteraction = canAcceptFriendlyInteraction;
        IsDomestic = isDomestic;
        IsPredator = isPredator;
        BaseTrust = baseTrust;
        Temperament = temperament;
        Anchors = anchors ?? InteractionAnchorDefinition.Empty;
        PreySpeciesIds = Array.AsReadOnly((preySpeciesIds ?? Array.Empty<string>()).ToArray());
        CarcassMeatUnits = carcassMeatUnits;
        ThreatDetectionRange = threatDetectionRange;
        CarcassFreshnessSeconds = carcassFreshnessSeconds;
        _dietItems = new HashSet<string>(Diet, StringComparer.Ordinal);
    }

    public string Id { get; }
    public string DisplayName { get; }
    public double MovementSpeed { get; }
    public double HungerRate { get; }
    public ReadOnlyCollection<string> Diet { get; }
    public bool CanHaul { get; }
    public string BodyCategory { get; }
    public DietaryProfile DietaryProfile { get; }
    public double MaxHealth { get; }
    public double Strength { get; }
    public double Endurance { get; }
    public double Dexterity { get; }
    public double SmellRange { get; }
    public bool CanUseTools { get; }
    public bool CanOpenDoors { get; }
    public bool CanTrack { get; }
    public bool UsesWildTerritory { get; }
    public bool CanAcceptFriendlyInteraction { get; }
    public bool IsDomestic { get; }
    public bool IsPredator { get; }
    public double BaseTrust { get; }
    public TemperamentProfile Temperament { get; }
    public InteractionAnchorDefinition Anchors { get; }
    public ReadOnlyCollection<string> PreySpeciesIds { get; }
    public int CarcassMeatUnits { get; }
    public double ThreatDetectionRange { get; }
    public double CarcassFreshnessSeconds { get; }

    public CharacterStats CreateCharacterStats(CharacterStats defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        return new CharacterStats(
            health: MaxHealth,
            stamina: defaults.MaxStamina,
            movementSpeed: MovementSpeed,
            carryCapacity: defaults.CarryCapacity,
            perception: defaults.Perception,
            defense: defaults.Defense,
            strength: Strength,
            endurance: Endurance);
    }

    public bool Eats(string? itemId)
    {
        return itemId is not null && _dietItems.Contains(itemId);
    }
}
