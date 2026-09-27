using System.Numerics;

namespace Simulation;

public sealed class EntityRecord
{
    internal EntityRecord(
        string id,
        string speciesId,
        Vector3 position,
        double satiety,
        ItemContainer inventory)
    {
        Id = id;
        SpeciesId = speciesId;
        Position = position;
        Satiety = satiety;
        Inventory = inventory;
    }

    public string Id { get; }
    public string SpeciesId { get; }
    public Vector3 Position { get; internal set; }
    public double Satiety { get; internal set; }
    public ItemContainer Inventory { get; }
    public string Activity { get; internal set; } = "Idle";
}
