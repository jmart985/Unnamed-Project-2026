using System.Collections.ObjectModel;
using System.Numerics;

namespace Simulation;

public sealed class EntityRecord
{
    private readonly Dictionary<string, int> _inventory = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, int> _inventoryView;

    internal EntityRecord(
        string id,
        string speciesId,
        Vector3 position,
        double satiety,
        NeedsTuning needsTuning)
    {
        Id = id;
        SpeciesId = speciesId;
        Position = position;
        Needs = new NeedsState(needsTuning, satiety);
        _inventoryView = new ReadOnlyDictionary<string, int>(_inventory);
    }

    public string Id { get; }
    public string SpeciesId { get; }
    public Vector3 Position { get; internal set; }
    public NeedsState Needs { get; }
    public double Satiety => Needs.Satiety;
    public IReadOnlyDictionary<string, int> Inventory => _inventoryView;
    public string Activity { get; internal set; } = "Idle";
}
