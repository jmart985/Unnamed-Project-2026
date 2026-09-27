using System.Collections.ObjectModel;

namespace Simulation;

public sealed class SpeciesDefinition
{
    private readonly HashSet<string> _dietItems;

    internal SpeciesDefinition(
        string id,
        double movementSpeed,
        double hungerRate,
        IEnumerable<string> diet,
        bool canHaul)
    {
        Id = id;
        MovementSpeed = movementSpeed;
        HungerRate = hungerRate;
        Diet = Array.AsReadOnly(diet.ToArray());
        CanHaul = canHaul;
        _dietItems = new HashSet<string>(Diet, StringComparer.Ordinal);
    }

    public string Id { get; }
    public double MovementSpeed { get; }
    public double HungerRate { get; }
    public ReadOnlyCollection<string> Diet { get; }
    public bool CanHaul { get; }

    public bool Eats(string? itemId)
    {
        return itemId is not null && _dietItems.Contains(itemId);
    }
}
