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
        double dexterity = 1,
        double smellRange = 1,
        bool canUseTools = false,
        bool canOpenDoors = false,
        bool canTrack = false)
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

    public bool Eats(string? itemId)
    {
        return itemId is not null && _dietItems.Contains(itemId);
    }
}
