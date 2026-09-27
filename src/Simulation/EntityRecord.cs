using System.Collections.ObjectModel;
using System.Numerics;

namespace Simulation;

public sealed class EntityRecord
{
    private readonly Dictionary<string, int> _inventory = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, int> _inventoryView;

    internal EntityRecord(string id, string speciesId, Vector3 position, double satiety)
    {
        Id = id;
        SpeciesId = speciesId;
        Position = position;
        Satiety = satiety;
        _inventoryView = new ReadOnlyDictionary<string, int>(_inventory);
    }

    public string Id { get; }
    public string SpeciesId { get; }
    public Vector3 Position { get; internal set; }
    public double Satiety { get; internal set; }
    public IReadOnlyDictionary<string, int> Inventory => _inventoryView;
    public string Activity { get; internal set; } = "Idle";
}
