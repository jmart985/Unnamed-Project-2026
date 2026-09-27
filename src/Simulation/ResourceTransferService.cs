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
}
