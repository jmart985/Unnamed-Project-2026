namespace Simulation;

public static class ResourceTransferService
{
    /// <summary>Transfers carried items atomically, respecting the recipient's configured capacity.</summary>
    public static ResourceMutationResult TryTransfer(EntityRecord source, EntityRecord destination, string? itemId, int amount)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (ReferenceEquals(source, destination)) return ResourceMutationResult.InvalidDestination;
        if (!ItemIds.IsKnown(itemId)) return ResourceMutationResult.UnknownItem;
        if (amount <= 0) return ResourceMutationResult.InvalidAmount;
        if (source.Inventory.GetValueOrDefault(itemId!) < amount) return ResourceMutationResult.InsufficientQuantity;
        if (destination.Inventory.Values.Sum(count => (long)count) + amount > destination.InventoryCapacity)
            return ResourceMutationResult.CapacityExceeded;
        var added = destination.TryAddCarried(itemId, amount);
        if (added != ResourceMutationResult.Success) return added;
        source.TryRemoveCarried(itemId, amount);
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
    /// Atomically moves units from a resource container into an entity's carried inventory
    /// (e.g. picking up wood from a ground pile). Either the full amount moves or neither
    /// container changes.
    /// </summary>
    public static ResourceMutationResult TryPickUp(
        ResourceContainer source,
        EntityRecord entity,
        string? itemId,
        int amount)
    {
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

        source.RemoveKnown(knownItemId, amount);
        var addResult = entity.TryAddCarried(knownItemId, amount);
        if (addResult != ResourceMutationResult.Success)
        {
            // Restore the source so the transfer stays atomic (e.g. the entity's carried
            // count would otherwise overflow int.MaxValue).
            source.AddKnown(knownItemId, amount);
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
