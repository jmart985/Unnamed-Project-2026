using System.Numerics;

namespace Simulation;

public sealed class EntityRecord
{
    internal EntityRecord(
        string id,
        string speciesId,
        Vector3 position,
        double satiety,
        int inventoryCapacity,
        NeedsTuning needsTuning,
        ReservationService reservations)
    {
        Id = id;
        SpeciesId = speciesId;
        Position = position;
        Needs = new NeedsState(needsTuning, satiety);
        InventoryContainer = new ResourceContainer($"inventory-{id}", inventoryCapacity);
        Actions = new ActionLifecycle(id, reservations);
    }

    public string Id { get; }
    public string SpeciesId { get; }
    public Vector3 Position { get; internal set; }
    public NeedsState Needs { get; }
    public ActionLifecycle Actions { get; }
    public double Satiety => Needs.Satiety;
    public IReadOnlyDictionary<string, int> Inventory => InventoryContainer.Counts;
    public int InventoryCapacity => InventoryContainer.Capacity;
    public string Activity { get; internal set; } = "Idle";
    public string? WaitingReason { get; internal set; }
    internal ResourceContainer InventoryContainer { get; }
}
