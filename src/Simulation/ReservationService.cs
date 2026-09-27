namespace Simulation;

public enum ReservationResult
{
    Reserved,
    InvalidRequest,
    UnknownItem,
    StaleAction,
    StaleTarget,
    InsufficientQuantity,
    CapacityExceeded,
    Conflict
}

public sealed class ReservationService
{
    private readonly Func<string, string, bool> _isActionActive;
    private readonly List<Reservation> _reservations = new();
    private readonly HashSet<string> _removedTargets = new(StringComparer.Ordinal);

    internal ReservationService(Func<string, string, bool> isActionActive)
    {
        _isActionActive = isActionActive;
    }

    public ReservationResult TryReserveResource(
        string? entityId,
        string? actionId,
        string? targetId,
        ResourceContainer? source,
        string? itemId,
        int amount)
    {
        if (!ItemIds.IsKnown(itemId))
        {
            return ReservationResult.UnknownItem;
        }

        var validation = ValidateRequest(entityId, actionId, targetId, source, amount);
        if (validation != ReservationResult.Reserved)
        {
            return validation;
        }

        if (HasDuplicate(entityId!, actionId!, targetId!, source!, ReservationKind.Resource, itemId))
        {
            return ReservationResult.Conflict;
        }

        if (!source!.TryGetCount(itemId, out var count))
        {
            return ReservationResult.UnknownItem;
        }

        var alreadyReserved = _reservations
            .Where(reservation =>
                reservation.Kind == ReservationKind.Resource
                && ReferenceEquals(reservation.Container, source)
                && string.Equals(reservation.ItemId, itemId, StringComparison.Ordinal))
            .Sum(reservation => reservation.Amount);

        if (amount > count - alreadyReserved)
        {
            return ReservationResult.InsufficientQuantity;
        }

        _reservations.Add(new Reservation(
            entityId!,
            actionId!,
            targetId!,
            source,
            ReservationKind.Resource,
            itemId,
            amount));
        return ReservationResult.Reserved;
    }

    public ReservationResult TryReserveCapacity(
        string? entityId,
        string? actionId,
        string? targetId,
        ResourceContainer? destination,
        int amount)
    {
        var validation = ValidateRequest(entityId, actionId, targetId, destination, amount);
        if (validation != ReservationResult.Reserved)
        {
            return validation;
        }

        if (HasDuplicate(entityId!, actionId!, targetId!, destination!, ReservationKind.Capacity, null))
        {
            return ReservationResult.Conflict;
        }

        var alreadyReserved = _reservations
            .Where(reservation =>
                reservation.Kind == ReservationKind.Capacity
                && ReferenceEquals(reservation.Container, destination))
            .Sum(reservation => reservation.Amount);

        if (amount > destination!.RemainingCapacity - alreadyReserved)
        {
            return ReservationResult.CapacityExceeded;
        }

        _reservations.Add(new Reservation(
            entityId!,
            actionId!,
            targetId!,
            destination,
            ReservationKind.Capacity,
            null,
            amount));
        return ReservationResult.Reserved;
    }

    public int ReleaseForAction(string? entityId, string? actionId)
    {
        if (string.IsNullOrWhiteSpace(entityId) || string.IsNullOrWhiteSpace(actionId))
        {
            return 0;
        }

        return RemoveWhere(reservation =>
            string.Equals(reservation.EntityId, entityId, StringComparison.Ordinal)
            && string.Equals(reservation.ActionId, actionId, StringComparison.Ordinal));
    }

    public int ReleaseForEntity(string? entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return 0;
        }

        return RemoveWhere(reservation =>
            string.Equals(reservation.EntityId, entityId, StringComparison.Ordinal));
    }

    public int RemoveTarget(string? targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return 0;
        }

        _removedTargets.Add(targetId);
        return RemoveWhere(reservation =>
            string.Equals(reservation.TargetId, targetId, StringComparison.Ordinal));
    }

    internal void Reset()
    {
        _reservations.Clear();
        _removedTargets.Clear();
    }

    private ReservationResult ValidateRequest(
        string? entityId,
        string? actionId,
        string? targetId,
        ResourceContainer? container,
        int amount)
    {
        if (string.IsNullOrWhiteSpace(entityId)
            || string.IsNullOrWhiteSpace(actionId)
            || string.IsNullOrWhiteSpace(targetId)
            || container is null
            || amount <= 0)
        {
            return ReservationResult.InvalidRequest;
        }

        if (!_isActionActive(entityId, actionId))
        {
            return ReservationResult.StaleAction;
        }

        if (_removedTargets.Contains(targetId))
        {
            return ReservationResult.StaleTarget;
        }

        return ReservationResult.Reserved;
    }

    private bool HasDuplicate(
        string entityId,
        string actionId,
        string targetId,
        ResourceContainer container,
        ReservationKind kind,
        string? itemId)
    {
        return _reservations.Any(reservation =>
            string.Equals(reservation.EntityId, entityId, StringComparison.Ordinal)
            && string.Equals(reservation.ActionId, actionId, StringComparison.Ordinal)
            && string.Equals(reservation.TargetId, targetId, StringComparison.Ordinal)
            && ReferenceEquals(reservation.Container, container)
            && reservation.Kind == kind
            && string.Equals(reservation.ItemId, itemId, StringComparison.Ordinal));
    }

    private int RemoveWhere(Predicate<Reservation> predicate)
    {
        return _reservations.RemoveAll(predicate);
    }

    private enum ReservationKind
    {
        Resource,
        Capacity
    }

    private sealed record Reservation(
        string EntityId,
        string ActionId,
        string TargetId,
        ResourceContainer Container,
        ReservationKind Kind,
        string? ItemId,
        int Amount);
}
