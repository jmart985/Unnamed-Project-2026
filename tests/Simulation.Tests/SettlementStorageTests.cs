using System.Numerics;
using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class SettlementStorageTests
{
    [Fact]
    public void StorageAcceptsConfiguredItemsAndReportsCapacity()
    {
        var storage = new SettlementStorage("stockpile", Vector3.Zero, 3, [ItemIds.Wood]);
        Assert.Equal(ResourceMutationResult.Success, storage.TryAdd(ItemIds.Wood, 2));
        Assert.Equal(1, storage.Contents.RemainingCapacity);
        Assert.Equal(ResourceMutationResult.CapacityExceeded, storage.TryAdd(ItemIds.Wood, 2));
        Assert.Equal(2, storage.Contents.UsedCapacity);
    }

    [Fact]
    public void StorageRejectsUnacceptedAndInvalidResourcesWithoutMutation()
    {
        var storage = new SettlementStorage("food-store", Vector3.One, 5, [ItemIds.Food]);
        Assert.Equal(ResourceMutationResult.InvalidDestination, storage.TryAdd(ItemIds.Wood, 1));
        Assert.Equal(ResourceMutationResult.InvalidDestination, storage.TryAdd("unknown", 1));
        Assert.Equal(ResourceMutationResult.InvalidAmount, storage.TryAdd(ItemIds.Food, 0));
        Assert.Equal(0, storage.Contents.UsedCapacity);
    }
}
