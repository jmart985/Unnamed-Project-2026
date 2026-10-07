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
        int inventoryCapacity,
        NeedsTuning needsTuning,
        ReservationService reservations,
        CharacterStats? baseStats = null)
    {
        Id = id;
        SpeciesId = speciesId;
        Position = position;
        Needs = new NeedsState(needsTuning, satiety);
        BaseStats = baseStats ?? CharacterStats.Default;
        Health = CharacterHealth.CreateHumanoid(BaseStats.MaxHealth);
        Skills = new SkillSet();
        InventoryCapacity = inventoryCapacity;
        _inventoryView = new ReadOnlyDictionary<string, int>(_inventory);
        Actions = new ActionLifecycle(id, reservations, () => _inventory.Count > 0);
    }

    public string Id { get; }
    public string SpeciesId { get; }
    public Vector3 Position { get; internal set; }
    public NeedsState Needs { get; }
    public CharacterStats BaseStats { get; }
    public CharacterHealth Health { get; }
    public SkillSet Skills { get; }
    public ActionLifecycle Actions { get; }
    public CreatureCaptivityState CaptivityState { get; internal set; }
    public string? AllegianceFactionId { get; internal set; }
    public double Satiety => Needs.Satiety;
    public IReadOnlyDictionary<string, int> Inventory => _inventoryView;
    public int InventoryCapacity { get; }
    public string Activity { get; internal set; } = "Idle";
    public SleepState SleepState { get; internal set; }
    public string? BedId { get; internal set; }
    public string? WaitingReason { get; internal set; }
    public string? TargetId { get; internal set; }
    public string? EquippedTool { get; internal set; }
    public string? Parent1Id { get; internal set; }
    public string? Parent2Id { get; internal set; }

    internal void SetSleepState(SleepState state, string? bedId)
    {
        SleepState = state;
        BedId = bedId;
    }

    /// <summary>
    /// Atomically adds carried units of a known item to this entity's inventory. Carried
    /// inventory has no bounded capacity limit yet (bounded haul capacity is deferred to the
    /// hauling job that defines it per species; see docs/DECISIONS.md), but an addition that
    /// would overflow the representable <see cref="int"/> count is rejected as
    /// <see cref="ResourceMutationResult.CapacityExceeded"/> rather than wrapping.
    /// </summary>
    internal ResourceMutationResult TryAddCarried(string? itemId, int amount)
    {
        if (!ItemIds.IsKnown(itemId))
        {
            return ResourceMutationResult.UnknownItem;
        }

        if (amount <= 0)
        {
            return ResourceMutationResult.InvalidAmount;
        }

        var knownItemId = itemId!;
        _inventory.TryGetValue(knownItemId, out var current);
        if (amount > int.MaxValue - current)
        {
            return ResourceMutationResult.CapacityExceeded;
        }

        _inventory[knownItemId] = current + amount;
        return ResourceMutationResult.Success;
    }

    /// <summary>
    /// Atomically removes carried units of a known item from this entity's inventory. Never
    /// removes more than is carried; a partial or invalid request leaves inventory unchanged.
    /// </summary>
    internal ResourceMutationResult TryRemoveCarried(string? itemId, int amount)
    {
        if (!ItemIds.IsKnown(itemId))
        {
            return ResourceMutationResult.UnknownItem;
        }

        if (amount <= 0)
        {
            return ResourceMutationResult.InvalidAmount;
        }

        var knownItemId = itemId!;
        if (!_inventory.TryGetValue(knownItemId, out var current) || current < amount)
        {
            return ResourceMutationResult.InsufficientQuantity;
        }

        var remaining = current - amount;
        if (remaining == 0)
        {
            _inventory.Remove(knownItemId);
        }
        else
        {
            _inventory[knownItemId] = remaining;
        }

        return ResourceMutationResult.Success;
    }
}
