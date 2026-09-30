namespace Simulation;

public enum WorkOrderStatus { Queued, Claimed, Completed }

public sealed class SettlementWorkOrder
{
    public SettlementWorkOrder(string id, string type, int priority, string? targetId = null)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Work orders require an ID and type.");
        if (priority < 0) throw new ArgumentOutOfRangeException(nameof(priority));
        Id=id; Type=type; Priority=priority; TargetId=targetId; Status=WorkOrderStatus.Queued;
    }
    public string Id { get; }
    public string Type { get; }
    public int Priority { get; }
    public string? TargetId { get; }
    public WorkOrderStatus Status { get; private set; }
    public string? ClaimedBy { get; private set; }
    public bool TryClaim(string? workerId)
    {
        if (Status != WorkOrderStatus.Queued || string.IsNullOrWhiteSpace(workerId)) return false;
        Status=WorkOrderStatus.Claimed; ClaimedBy=workerId; return true;
    }
    public bool TryComplete(string? workerId)
    {
        if (Status != WorkOrderStatus.Claimed || !string.Equals(ClaimedBy, workerId, StringComparison.Ordinal)) return false;
        Status=WorkOrderStatus.Completed; return true;
    }
}

public sealed class SettlementWorkOrderQueue
{
    private readonly Dictionary<string,SettlementWorkOrder> _orders = new(StringComparer.Ordinal);
    public IReadOnlyCollection<SettlementWorkOrder> Orders => _orders.Values;
    public bool TryEnqueue(SettlementWorkOrder? order)
    {
        if (order is null || !_orders.TryAdd(order.Id, order)) return false;
        return true;
    }
    public SettlementWorkOrder? NextAvailable() => _orders.Values.Where(order => order.Status == WorkOrderStatus.Queued).OrderByDescending(order => order.Priority).ThenBy(order => order.Id, StringComparer.Ordinal).FirstOrDefault();
}
