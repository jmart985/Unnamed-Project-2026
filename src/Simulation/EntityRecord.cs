using System.Collections.ObjectModel;
using System.Numerics;

namespace Simulation;

public sealed class EntityRecord
{
    public const int MaxCarriedCountPerItem = 999;
    private readonly Dictionary<string, int> _inventory = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, int> _inventoryView;
    private readonly Dictionary<string, string> _equippedItems = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, string> _equippedItemsView;
    private readonly HashSet<string> _hiddenEquipmentSlots = new(StringComparer.Ordinal);

    internal EntityRecord(
        string id,
        string speciesId,
        Vector3 position,
        double satiety,
        NeedsTuning needsTuning,
        ReservationService reservations,
        BiologicalSex sex,
        CharacterStats? baseStats = null)
    {
        if (!Enum.IsDefined(sex) || sex == BiologicalSex.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(sex), "An entity must have a defined biological sex.");
        Id = id;
        SpeciesId = speciesId;
        Sex = sex;
        Position = position;
        Needs = new NeedsState(needsTuning, satiety);
        BaseStats = baseStats ?? CharacterStats.Default;
        Health = CharacterHealth.CreateHumanoid(BaseStats.MaxHealth);
        Skills = new SkillSet();
        _inventoryView = new ReadOnlyDictionary<string, int>(_inventory);
        _equippedItemsView = new ReadOnlyDictionary<string, string>(_equippedItems);
        Actions = new ActionLifecycle(id, reservations, () => _inventory.Count > 0);
    }

    public string Id { get; }
    public string SpeciesId { get; }
    public BiologicalSex Sex { get; }
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
    public IReadOnlyDictionary<string, string> EquippedItems => _equippedItemsView;
    public IReadOnlyList<string> HiddenEquipmentSlots => _hiddenEquipmentSlots.Order(StringComparer.Ordinal).ToArray();
    public double CarriedWeightKg =>
        _inventory.Sum(item => item.Value * ItemCatalog.GetWeightKg(item.Key))
        + _equippedItems.Values.Sum(ItemCatalog.GetWeightKg);
    public string Activity { get; internal set; } = "Idle";
    public SleepState SleepState { get; internal set; }
    public string? BedId { get; internal set; }
    public string? WaitingReason { get; internal set; }
    public string? TargetId { get; internal set; }
    public string? EquippedTool { get; internal set; }
    public string? Parent1Id { get; internal set; }
    public string? Parent2Id { get; internal set; }
    public EntityOrder? OrderIntent { get; internal set; }

    public void AdvanceNeeds(double simulationSeconds, bool isSleeping = false) =>
        Needs.AdvanceWithLoad(simulationSeconds, CarriedWeightKg, BaseStats, isSleeping);

    internal void SetSleepState(SleepState state, string? bedId)
    {
        SleepState = state;
        BedId = bedId;
    }

    /// <summary>
    /// Atomically adds carried units of a known item, up to the per-item stack limit.
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
        if (amount > MaxCarriedCountPerItem - current)
        {
            return ResourceMutationResult.CapacityExceeded;
        }

        _inventory[knownItemId] = current + amount;
        return ResourceMutationResult.Success;
    }

    internal int RemainingCarriedCapacity(string itemId)
    {
        _inventory.TryGetValue(itemId, out var current);
        return MaxCarriedCountPerItem - current;
    }

    internal bool CanAddCarried(string itemId, int amount) =>
        amount > 0 && amount <= RemainingCarriedCapacity(itemId);

    internal void SetEquippedItem(string slotId, string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        if (!ItemCatalog.IsWearable(itemId)
            || ItemCatalog.GetRequired(itemId).EquipmentSlot != slotId)
            throw new ArgumentException($"Item '{itemId}' cannot be equipped in slot '{slotId}'.", nameof(itemId));
        if (_equippedItems.Values.Contains(itemId, StringComparer.Ordinal)
            && _equippedItems.GetValueOrDefault(slotId) != itemId)
            throw new InvalidOperationException($"Item '{itemId}' is already equipped.");
        _equippedItems[slotId] = itemId;
        _hiddenEquipmentSlots.Remove(slotId);
    }

    internal void ClearEquippedItem(string slotId)
    {
        _equippedItems.Remove(slotId);
        _hiddenEquipmentSlots.Remove(slotId);
    }

    public bool IsEquipmentVisible(string slotId) =>
        !string.IsNullOrWhiteSpace(slotId) && !_hiddenEquipmentSlots.Contains(slotId);

    internal bool HasVisibleEquipment(string slotId) => slotId switch
    {
        EquipmentSlotIds.Tool => EquippedTool is not null,
        EquipmentSlotIds.Torso or EquipmentSlotIds.Legs => _equippedItems.ContainsKey(slotId),
        _ => false
    };

    internal bool SetEquipmentVisible(string slotId, bool visible)
    {
        if (!EquipmentSlotIds.SupportsVisibility(slotId) || !HasVisibleEquipment(slotId))
            return false;
        return visible ? _hiddenEquipmentSlots.Remove(slotId) : _hiddenEquipmentSlots.Add(slotId);
    }

    internal void SetEquippedTool(string? itemId)
    {
        EquippedTool = itemId;
        _hiddenEquipmentSlots.Remove(EquipmentSlotIds.Tool);
    }

    internal void RestoreHiddenEquipmentSlots(IEnumerable<string> slotIds)
    {
        ArgumentNullException.ThrowIfNull(slotIds);
        _hiddenEquipmentSlots.Clear();
        foreach (var slotId in slotIds)
            _hiddenEquipmentSlots.Add(slotId);
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
