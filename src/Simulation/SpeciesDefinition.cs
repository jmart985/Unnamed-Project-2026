using System.Collections.ObjectModel;

namespace Simulation;

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
        string bodyCategory)
    {
        Id = id;
        DisplayName = displayName;
        MovementSpeed = movementSpeed;
        HungerRate = hungerRate;
        Diet = Array.AsReadOnly(diet.ToArray());
        CanHaul = canHaul;
        BodyCategory = bodyCategory;
        _dietItems = new HashSet<string>(Diet, StringComparer.Ordinal);
    }

    public string Id { get; }
    public string DisplayName { get; }
    public double MovementSpeed { get; }
    public double HungerRate { get; }
    public ReadOnlyCollection<string> Diet { get; }
    public bool CanHaul { get; }
    public string BodyCategory { get; }

    public bool Eats(string? itemId)
    {
        return itemId is not null && _dietItems.Contains(itemId);
    }
}
