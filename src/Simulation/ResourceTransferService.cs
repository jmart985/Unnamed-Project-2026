namespace Simulation;

public static class ResourceTransferService
{
    /// <summary>Transfers carried items, accepting only the quantity that fits in the recipient's item stack.</summary>
    public static ResourceMutationResult TryTransfer(EntityRecord source, EntityRecord destination, string? itemId, int amount)
        => TryTransfer(source, destination, itemId, amount, out _);

    public static ResourceMutationResult TryTransfer(
        EntityRecord source,
        EntityRecord destination,
        string? itemId,
        int amount,
        out int transferred)
    {
        transferred = 0;
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (ReferenceEquals(source, destination)) return ResourceMutationResult.InvalidDestination;
        if (!ItemIds.IsKnown(itemId)) return ResourceMutationResult.UnknownItem;
        if (amount <= 0) return ResourceMutationResult.InvalidAmount;
        if (source.Inventory.GetValueOrDefault(itemId!) < amount) return ResourceMutationResult.InsufficientQuantity;
        transferred = Math.Min(amount, destination.RemainingCarriedCapacity(itemId!));
        if (transferred == 0) return ResourceMutationResult.CapacityExceeded;
        var added = destination.TryAddCarried(itemId, transferred);
        if (added != ResourceMutationResult.Success)
        {
            transferred = 0;
            return added;
        }
        source.TryRemoveCarried(itemId, transferred);
        return ResourceMutationResult.Success;
    }

    public static ResourceMutationResult TryTransfer(
        ResourceContainer source,
        ResourceContainer destination,
        string? itemId,
        int amount)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        if (ReferenceEquals(source, destination))
        {
            return ResourceMutationResult.InvalidDestination;
        }

        if (!ItemIds.IsKnown(itemId))
        {
            return ResourceMutationResult.UnknownItem;
        }

        if (amount <= 0)
        {
            return ResourceMutationResult.InvalidAmount;
        }

        var knownItemId = itemId!;
        if (!source.HasQuantity(knownItemId, amount))
        {
            return ResourceMutationResult.InsufficientQuantity;
        }

        if (amount > destination.RemainingCapacity)
        {
            return ResourceMutationResult.CapacityExceeded;
        }

        destination.AddKnown(knownItemId, amount);
        source.RemoveKnown(knownItemId, amount);
        return ResourceMutationResult.Success;
    }

    /// <summary>
    /// Moves units from a resource container into carried inventory (e.g. picking up wood).
    /// The entity accepts only the amount that fits in its per-item stack; the remainder stays
    /// in the source container. Invalid requests do not change either inventory.
    /// </summary>
    public static ResourceMutationResult TryPickUp(
        ResourceContainer source,
        EntityRecord entity,
        string? itemId,
        int amount)
        => TryPickUp(source, entity, itemId, amount, out _);

    public static ResourceMutationResult TryPickUp(
        ResourceContainer source,
        EntityRecord entity,
        string? itemId,
        int amount,
        out int transferred)
    {
        transferred = 0;
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entity);

        if (!ItemIds.IsKnown(itemId))
        {
            return ResourceMutationResult.UnknownItem;
        }

        if (amount <= 0)
        {
            return ResourceMutationResult.InvalidAmount;
        }

        var knownItemId = itemId!;
        if (!source.HasQuantity(knownItemId, amount))
        {
            return ResourceMutationResult.InsufficientQuantity;
        }

        transferred = Math.Min(amount, entity.RemainingCarriedCapacity(knownItemId));
        if (transferred == 0) return ResourceMutationResult.CapacityExceeded;

        source.RemoveKnown(knownItemId, transferred);
        var addResult = entity.TryAddCarried(knownItemId, transferred);
        if (addResult != ResourceMutationResult.Success)
        {
            source.AddKnown(knownItemId, transferred);
            transferred = 0;
            return addResult;
        }

        return ResourceMutationResult.Success;
    }

    /// <summary>
    /// Atomically moves units from an entity's carried inventory into a resource container
    /// (e.g. depositing wood into a stockpile). Either the full amount moves or neither
    /// container changes.
    /// </summary>
    public static ResourceMutationResult TryDeliver(
        EntityRecord entity,
        ResourceContainer destination,
        string? itemId,
        int amount)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(destination);

        if (!ItemIds.IsKnown(itemId))
        {
            return ResourceMutationResult.UnknownItem;
        }

        if (amount <= 0)
        {
            return ResourceMutationResult.InvalidAmount;
        }

        var knownItemId = itemId!;
        if (amount > destination.RemainingCapacity)
        {
            return ResourceMutationResult.CapacityExceeded;
        }

        var removeResult = entity.TryRemoveCarried(knownItemId, amount);
        if (removeResult != ResourceMutationResult.Success)
        {
            return removeResult;
        }

        destination.AddKnown(knownItemId, amount);
        return ResourceMutationResult.Success;
    }

    /// <summary>
    /// Atomically swaps two different items between two containers: <paramref name="firstItemId"/>
    /// moves from <paramref name="first"/> to <paramref name="second"/> while
    /// <paramref name="secondItemId"/> moves the opposite way. Every leg is validated, including
    /// capacity after both legs net out, before any container is mutated.
    /// </summary>
    public static ResourceExchangeResult TryExchange(
        ResourceContainer first,
        ResourceContainer second,
        string? firstItemId,
        int firstAmount,
        string? secondItemId,
        int secondAmount)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (ReferenceEquals(first, second))
        {
            return ResourceExchangeResult.InvalidDestination;
        }

        if (!ItemIds.IsKnown(firstItemId) || !ItemIds.IsKnown(secondItemId))
        {
            return ResourceExchangeResult.UnknownItem;
        }

        if (firstAmount <= 0 || secondAmount <= 0 || firstItemId == secondItemId)
        {
            return ResourceExchangeResult.InvalidAmount;
        }

        var firstItem = firstItemId!;
        var secondItem = secondItemId!;
        if (!first.HasQuantity(firstItem, firstAmount))
        {
            return ResourceExchangeResult.InsufficientFirst;
        }

        if (!second.HasQuantity(secondItem, secondAmount))
        {
            return ResourceExchangeResult.InsufficientSecond;
        }

        // Compare in long so the net capacity check cannot itself overflow.
        if ((long)secondAmount - firstAmount > first.RemainingCapacity)
        {
            return ResourceExchangeResult.CapacityExceededFirst;
        }

        if ((long)firstAmount - secondAmount > second.RemainingCapacity)
        {
            return ResourceExchangeResult.CapacityExceededSecond;
        }

        first.RemoveKnown(firstItem, firstAmount);
        second.RemoveKnown(secondItem, secondAmount);
        first.AddKnown(secondItem, secondAmount);
        second.AddKnown(firstItem, firstAmount);
        return ResourceExchangeResult.Success;
    }}
