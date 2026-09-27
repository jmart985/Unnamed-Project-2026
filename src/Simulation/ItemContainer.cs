using System.Collections;
using System.Collections.ObjectModel;

namespace Simulation;

public class ItemContainer : IReadOnlyDictionary<string, int>
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, int> _countView;

    public ItemContainer(ItemCatalog catalog, int capacity)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        Catalog = catalog;
        Capacity = capacity;
        _countView = new ReadOnlyDictionary<string, int>(_counts);
    }

    public ItemCatalog Catalog { get; }
    public int Capacity { get; }
    public int TotalCount => _counts.Values.Sum();
    public int Count => _counts.Count;
    public IEnumerable<string> Keys => _countView.Keys;
    public IEnumerable<int> Values => _countView.Values;
    public int this[string itemId] => _countView[itemId];

    public ItemMutationResult TryAdd(string? itemId, int amount)
    {
        if (!Catalog.Contains(itemId))
        {
            return ItemMutationResult.UnknownItem;
        }

        if (amount <= 0)
        {
            return ItemMutationResult.InvalidAmount;
        }

        if (amount > Capacity - TotalCount)
        {
            return ItemMutationResult.CapacityExceeded;
        }

        _counts.TryGetValue(itemId!, out var currentCount);
        _counts[itemId!] = currentCount + amount;
        return ItemMutationResult.Success;
    }

    public ItemMutationResult TryRemove(string? itemId, int amount)
    {
        if (!Catalog.Contains(itemId))
        {
            return ItemMutationResult.UnknownItem;
        }

        if (amount <= 0)
        {
            return ItemMutationResult.InvalidAmount;
        }

        if (!_counts.TryGetValue(itemId!, out var currentCount) || amount > currentCount)
        {
            return ItemMutationResult.InsufficientItems;
        }

        if (amount == currentCount)
        {
            _counts.Remove(itemId!);
        }
        else
        {
            _counts[itemId!] = currentCount - amount;
        }

        return ItemMutationResult.Success;
    }

    public bool TryGetCount(string? itemId, out int count)
    {
        if (!Catalog.Contains(itemId))
        {
            count = 0;
            return false;
        }

        _counts.TryGetValue(itemId!, out count);
        return true;
    }

    public void Clear()
    {
        _counts.Clear();
    }

    public bool ContainsKey(string key) => _countView.ContainsKey(key);
    public bool TryGetValue(string key, out int value) => _countView.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => _countView.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
