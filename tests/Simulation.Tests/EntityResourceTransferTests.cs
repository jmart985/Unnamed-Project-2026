using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class EntityResourceTransferTests
{
    [Fact]
    public void PickUpMovesTheFullAmountFromContainerToCarriedInventory()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 5);
        groundPile.TryAdd(ItemIds.Wood, 3);

        var result = ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2);

        Assert.Equal(ResourceMutationResult.Success, result);
        Assert.Equal(1, groundPile.Counts[ItemIds.Wood]);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
    }

    [Fact]
    public void DeliverMovesTheFullAmountFromCarriedInventoryToContainer()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 5);
        var stockpile = new ResourceContainer("stockpile", 5);
        groundPile.TryAdd(ItemIds.Wood, 2);
        ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2);

        var result = ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 2);

        Assert.Equal(ResourceMutationResult.Success, result);
        Assert.Empty(entity.Inventory);
        Assert.Equal(2, stockpile.Counts[ItemIds.Wood]);
    }

    [Fact]
    public void PickUpRejectsInsufficientSourceQuantityWithoutMutatingEitherSide()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 5);
        groundPile.TryAdd(ItemIds.Wood, 1);

        var result = ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2);

        Assert.Equal(ResourceMutationResult.InsufficientQuantity, result);
        Assert.Equal(1, groundPile.Counts[ItemIds.Wood]);
        Assert.Empty(entity.Inventory);
    }

    [Fact]
    public void DeliverRejectsInsufficientCarriedQuantityWithoutMutatingEitherSide()
    {
        var entity = CreateEntity();
        var stockpile = new ResourceContainer("stockpile", 5);

        var result = ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 1);

        Assert.Equal(ResourceMutationResult.InsufficientQuantity, result);
        Assert.Empty(entity.Inventory);
        Assert.Empty(stockpile.Counts);
    }

    [Fact]
    public void DeliverRejectsAmountsExceedingDestinationCapacityWithoutMutatingEitherSide()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 5);
        var stockpile = new ResourceContainer("stockpile", 1);
        groundPile.TryAdd(ItemIds.Wood, 2);
        ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2);

        var result = ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 2);

        Assert.Equal(ResourceMutationResult.CapacityExceeded, result);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
        Assert.Empty(stockpile.Counts);
    }

    [Fact]
    public void PickUpAndDeliverRejectInvalidAmountsAndUnknownItems()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 5);
        var stockpile = new ResourceContainer("stockpile", 5);
        groundPile.TryAdd(ItemIds.Wood, 1);

        Assert.Equal(
            ResourceMutationResult.InvalidAmount,
            ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 0));
        Assert.Equal(
            ResourceMutationResult.UnknownItem,
            ResourceTransferService.TryPickUp(groundPile, entity, "stone", 1));
        Assert.Equal(
            ResourceMutationResult.InvalidAmount,
            ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, -1));
        Assert.Equal(
            ResourceMutationResult.UnknownItem,
            ResourceTransferService.TryDeliver(entity, stockpile, "stone", 1));
        Assert.Equal(1, groundPile.Counts[ItemIds.Wood]);
        Assert.Empty(entity.Inventory);
    }

    [Fact]
    public void RepeatedPickUpsAndDeliveriesConserveTotalWood()
    {
        var entity = CreateEntity();
        var groundPile = new ResourceContainer("ground-pile", 10);
        var stockpile = new ResourceContainer("stockpile", 3);
        groundPile.TryAdd(ItemIds.Wood, 5);
        var initialTotal = groundPile.Counts[ItemIds.Wood];

        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 3));
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 3));
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2));
        Assert.Equal(
            ResourceMutationResult.CapacityExceeded,
            ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 2));

        var finalTotal = groundPile.Counts.GetValueOrDefault(ItemIds.Wood)
            + stockpile.Counts.GetValueOrDefault(ItemIds.Wood)
            + entity.Inventory.GetValueOrDefault(ItemIds.Wood);
        Assert.Equal(initialTotal, finalTotal);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
        Assert.Equal(3, stockpile.Counts[ItemIds.Wood]);
        Assert.Equal(0, groundPile.Counts.GetValueOrDefault(ItemIds.Wood));
    }

    [Fact]
    public void PickUpRejectsAnAdditionThatWouldOverflowCarriedInventoryWithoutMutatingEitherSide()
    {
        var entity = CreateEntity();
        var filledSource = new ResourceContainer("ground-pile-full", int.MaxValue);
        filledSource.TryAdd(ItemIds.Wood, int.MaxValue);
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(filledSource, entity, ItemIds.Wood, int.MaxValue));
        Assert.Equal(int.MaxValue, entity.Inventory[ItemIds.Wood]);

        var overflowSource = new ResourceContainer("ground-pile-extra", 5);
        overflowSource.TryAdd(ItemIds.Wood, 1);

        var result = ResourceTransferService.TryPickUp(overflowSource, entity, ItemIds.Wood, 1);

        Assert.Equal(ResourceMutationResult.CapacityExceeded, result);
        Assert.Equal(1, overflowSource.Counts[ItemIds.Wood]);
        Assert.Equal(int.MaxValue, entity.Inventory[ItemIds.Wood]);
    }

    private static EntityRecord CreateEntity()
    {
        var world = new World();
        return world.SpawnEntity("human", Vector3.Zero);
    }
}
