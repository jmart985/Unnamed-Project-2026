using System.Numerics;

namespace Simulation;

/// <summary>
/// Represents a behavioral override order issued to an entity.
/// </summary>
public sealed record EntityOrder(
    string OrderId,
    string Command,
    string? TargetId = null,
    Vector3? TargetPosition = null,
    double ExpiryTime = double.MaxValue);

/// <summary>
/// Manages active orders for entities in the world.
/// </summary>
public sealed class OrderService : ITransientSimulationState
{
    private readonly Dictionary<string, EntityOrder> _activeOrders = new(StringComparer.Ordinal);

    public void IssueOrder(string entityId, string command, string? targetId = null, Vector3? position = null, double currentTime = 0)
    {
        var order = new EntityOrder(
            Guid.NewGuid().ToString(),
            command,
            targetId,
            position,
            currentTime + (command == "stay" ? double.MaxValue : 30.0)); // Example: Orders expire in 30s unless 'stay'

        _activeOrders[entityId] = order;
    }

    public EntityOrder? GetActiveOrder(string entityId)
    {
        if (_activeOrders.TryGetValue(entityId, out var order))
        {
            return order;
        }
        return null;
    }

    public void ClearOrder(string entityId)
    {
        _activeOrders.Remove(entityId);
    }

    public void Update(double currentTime)
    {
        var expired = _activeOrders
            .Where(pair => currentTime > pair.Value.ExpiryTime)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var id in expired)
        {
            _activeOrders.Remove(id);
        }
    }

    public void Reset()
    {
        _activeOrders.Clear();
    }
}
