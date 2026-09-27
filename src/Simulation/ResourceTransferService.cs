namespace Simulation;

public static class ResourceTransferService
{
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
            // Restore the source; carried inventory currently has no capacity ceiling so this
            // path is unreachable in practice, but keep the transfer atomic if that changes.
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
}
