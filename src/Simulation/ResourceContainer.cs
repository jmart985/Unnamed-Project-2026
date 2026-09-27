using System.Collections.ObjectModel;

namespace Simulation;

public enum ResourceMutationResult
{
    Success,
    UnknownItem,
    InvalidAmount,
    CapacityExceeded,
    InsufficientQuantity
}

public sealed class ResourceContainer
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, int> _countsView;

    public ResourceContainer(string name, int capacity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A container name is required.", nameof(name));
        }

        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be negative.");
        }

        Name = name;
        Capacity = capacity;
        _countsView = new ReadOnlyDictionary<string, int>(_counts);
    }

    public string Name { get; }
    public int Capacity { get; }
    public int UsedCapacity { get; private set; }
    public int RemainingCapacity => Capacity - UsedCapacity;
    public IReadOnlyDictionary<string, int> Counts => _countsView;

    public ResourceMutationResult TryAdd(string? itemId, int amount)
    {
        if (!ItemIds.IsKnown(itemId))
        {
            return ResourceMutationResult.UnknownItem;
        }

        if (amount <= 0)
        {
            return ResourceMutationResult.InvalidAmount;
        }

        if (amount > RemainingCapacity)
        {
            return ResourceMutationResult.CapacityExceeded;
        }

        var knownItemId = itemId!;
        _counts.TryGetValue(knownItemId, out var current);
        _counts[knownItemId] = current + amount;
        UsedCapacity += amount;
        return ResourceMutationResult.Success;
    }

    public ResourceMutationResult TryRemove(string? itemId, int amount)
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
        if (!_counts.TryGetValue(knownItemId, out var current) || current < amount)
        {
            return ResourceMutationResult.InsufficientQuantity;
        }

        var remaining = current - amount;
        if (remaining == 0)
        {
            _counts.Remove(knownItemId);
        }
        else
        {
            _counts[knownItemId] = remaining;
        }

        UsedCapacity -= amount;
        return ResourceMutationResult.Success;
    }

    public bool TryGetCount(string? itemId, out int count)
    {
        if (!ItemIds.IsKnown(itemId))
        {
            count = 0;
            return false;
        }

        count = _counts.GetValueOrDefault(itemId!);
        return true;
    }
}
