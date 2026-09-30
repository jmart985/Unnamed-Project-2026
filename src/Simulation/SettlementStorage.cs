using System.Numerics;

namespace Simulation;

/// <summary>Engine-independent settlement storage with an explicit resource policy.</summary>
public sealed class SettlementStorage
{
    private readonly HashSet<string> _acceptedItems;
    public SettlementStorage(string id, Vector3 position, int capacity, IEnumerable<string> acceptedItems)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A storage ID is required.", nameof(id));
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)) throw new ArgumentOutOfRangeException(nameof(position));
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        ArgumentNullException.ThrowIfNull(acceptedItems);
        _acceptedItems = acceptedItems.Where(ItemIds.IsKnown).ToHashSet(StringComparer.Ordinal);
        if (_acceptedItems.Count == 0) throw new ArgumentException("At least one known item type is required.", nameof(acceptedItems));
        Id = id; Position = position; Contents = new ResourceContainer(id, capacity);
    }
    public string Id { get; }
    public Vector3 Position { get; }
    public ResourceContainer Contents { get; }
    public IReadOnlySet<string> AcceptedItems => _acceptedItems;
    public bool Accepts(string? itemId) => itemId is not null && _acceptedItems.Contains(itemId);
    public ResourceMutationResult TryAdd(string? itemId, int amount) => Accepts(itemId) ? Contents.TryAdd(itemId, amount) : ResourceMutationResult.InvalidDestination;
    public ResourceMutationResult TryRemove(string? itemId, int amount) => Accepts(itemId) ? Contents.TryRemove(itemId, amount) : ResourceMutationResult.InvalidDestination;
}
