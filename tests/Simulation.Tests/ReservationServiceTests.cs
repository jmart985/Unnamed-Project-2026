using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class ReservationServiceTests
{
    [Fact]
    public void ResourceQuantityCannotBeReservedByTwoActiveActions()
    {
        var (world, first, second) = CreateWorldWithTwoEntities();
        var source = new ResourceContainer("wood-pile", 2);
        source.TryAdd(ItemIds.Wood, 1);
        StartAction(first, "haul-1");
        StartAction(second, "haul-2");

        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(first.Id, "haul-1", "pile-1", source, ItemIds.Wood, 1));
        Assert.Equal(
            ReservationResult.InsufficientQuantity,
            world.Reservations.TryReserveResource(second.Id, "haul-2", "pile-1", source, ItemIds.Wood, 1));
    }

    [Fact]
    public void CapacityCannotBeReservedByTwoActiveActions()
    {
        var (world, first, second) = CreateWorldWithTwoEntities();
        var stockpile = new ResourceContainer("stockpile", 3);
        stockpile.TryAdd(ItemIds.Food, 1);
        StartAction(first, "haul-1");
        StartAction(second, "haul-2");

        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(first.Id, "haul-1", "stockpile-1", stockpile, 2));
        Assert.Equal(
            ReservationResult.CapacityExceeded,
            world.Reservations.TryReserveCapacity(second.Id, "haul-2", "stockpile-1", stockpile, 1));
    }

    [Theory]
    [InlineData(ActionFinish.Complete)]
    [InlineData(ActionFinish.Cancel)]
    [InlineData(ActionFinish.Preempt)]
    public void FinishingOrPreemptingAnActionReleasesItsReservations(ActionFinish finish)
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var source = new ResourceContainer("wood-pile", 2);
        var stockpile = new ResourceContainer("stockpile", 2);
        source.TryAdd(ItemIds.Wood, 1);
        StartAction(entity, "haul-1");
        ReserveBoth(world, entity, "haul-1", source, stockpile);

        switch (finish)
        {
            case ActionFinish.Complete:
                Assert.True(entity.Actions.Complete());
                break;
            case ActionFinish.Cancel:
                Assert.True(entity.Actions.Cancel());
                break;
            case ActionFinish.Preempt:
                Assert.Equal(
                    ActionStartResult.Preempted,
                    entity.Actions.TryStart(new ActionCandidate("eat-1", ActionKind.Eat)));
                break;
        }

        var nextEntity = world.SpawnEntity("human", Vector3.One);
        StartAction(nextEntity, "haul-2");
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(nextEntity.Id, "haul-2", "pile-1", source, ItemIds.Wood, 1));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(nextEntity.Id, "haul-2", "stockpile-1", stockpile, 2));
    }

    [Fact]
    public void EntityRemovalCancelsItsActionAndReleasesAllReservations()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var source = new ResourceContainer("wood-pile", 1);
        var stockpile = new ResourceContainer("stockpile", 1);
        source.TryAdd(ItemIds.Wood, 1);
        StartAction(entity, "haul-1");
        ReserveBoth(world, entity, "haul-1", source, stockpile);

        Assert.True(world.TryRemoveEntity(entity.Id));

        var replacement = world.SpawnEntity("human", Vector3.One);
        StartAction(replacement, "haul-2");
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(replacement.Id, "haul-2", "pile-1", source, ItemIds.Wood, 1));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(replacement.Id, "haul-2", "stockpile-1", stockpile, 1));
    }

    [Fact]
    public void ResetReleasesReservationsAndRemovedTargetState()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var source = new ResourceContainer("wood-pile", 1);
        source.TryAdd(ItemIds.Wood, 1);
        StartAction(entity, "haul-1");
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(entity.Id, "haul-1", "pile-1", source, ItemIds.Wood, 1));
        world.Reservations.RemoveTarget("pile-1");

        world.Reset();

        var replacement = world.SpawnEntity("human", Vector3.One);
        StartAction(replacement, "haul-2");
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(replacement.Id, "haul-2", "pile-1", source, ItemIds.Wood, 1));
    }

    [Fact]
    public void RemovingTargetReleasesClaimsAndRejectsFutureReservationsForIt()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var stockpile = new ResourceContainer("stockpile", 1);
        var source = new ResourceContainer("wood-pile", 1);
        source.TryAdd(ItemIds.Wood, 1);
        StartAction(entity, "haul-1");
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(entity.Id, "haul-1", "pile-1", source, ItemIds.Wood, 1));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(entity.Id, "haul-1", "stockpile-1", stockpile, 1));

        Assert.Equal(1, world.Reservations.RemoveTarget("pile-1"));
        Assert.Equal(0, world.Reservations.RemoveTarget("pile-1"));
        Assert.Equal(
            ReservationResult.StaleTarget,
            world.Reservations.TryReserveResource(entity.Id, "haul-1", "pile-1", source, ItemIds.Wood, 1));

        var nextEntity = world.SpawnEntity("human", Vector3.One);
        StartAction(nextEntity, "haul-2");
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(nextEntity.Id, "haul-2", "pile-2", source, ItemIds.Wood, 1));
        Assert.Equal(
            ReservationResult.CapacityExceeded,
            world.Reservations.TryReserveCapacity(nextEntity.Id, "haul-2", "stockpile-1", stockpile, 1));

        Assert.Equal(1, world.Reservations.RemoveTarget("stockpile-1"));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(nextEntity.Id, "haul-2", "stockpile-2", stockpile, 1));
    }

    [Theory]
    [InlineData(RemovalPath.FoodSource)]
    [InlineData(RemovalPath.ResourcePile)]
    public void WorldTargetRemovalReleasesResourceReservationsAndMarksTheTargetStale(RemovalPath removalPath)
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var contents = new ResourceContainer("target-contents", 1);
        var itemId = removalPath == RemovalPath.FoodSource ? ItemIds.Food : ItemIds.Wood;
        contents.TryAdd(itemId, 1);
        StartAction(entity, "eat-1");

        var targetId = removalPath switch
        {
            RemovalPath.FoodSource => world.AddFoodSource("food-1", Vector3.UnitX, contents).Id,
            RemovalPath.ResourcePile => world.AddResourcePile("pile-1", Vector3.UnitX, contents).Id,
            _ => throw new ArgumentOutOfRangeException(nameof(removalPath))
        };

        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(entity.Id, "eat-1", targetId, contents, itemId, 1));

        var removed = removalPath switch
        {
            RemovalPath.FoodSource => world.TryRemoveFoodSource(targetId),
            RemovalPath.ResourcePile => world.TryRemoveResourcePile(targetId),
            _ => false
        };

        Assert.True(removed);

        var replacement = world.SpawnEntity("human", Vector3.One);
        StartAction(replacement, "eat-2");
        Assert.Equal(
            ReservationResult.StaleTarget,
            world.Reservations.TryReserveResource(replacement.Id, "eat-2", targetId, contents, itemId, 1));
    }

    [Fact]
    public void WorldStockpileRemovalReleasesCapacityReservationsAndMarksTheTargetStale()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var stockpile = world.AddStockpile("stockpile-1", Vector3.UnitX, new ResourceContainer("stockpile", 1));
        StartAction(entity, "haul-1");

        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(
                entity.Id,
                "haul-1",
                stockpile.Id,
                stockpile.Contents,
                1));

        Assert.True(world.TryRemoveStockpile(stockpile.Id));

        var replacement = world.SpawnEntity("human", Vector3.One);
        StartAction(replacement, "haul-2");
        Assert.Equal(
            ReservationResult.StaleTarget,
            world.Reservations.TryReserveCapacity(
                replacement.Id,
                "haul-2",
                stockpile.Id,
                stockpile.Contents,
                1));
    }

    [Fact]
    public void StaleActionsAndInvalidRequestsReturnControlledResults()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var source = new ResourceContainer("wood-pile", 1);

        Assert.Equal(
            ReservationResult.StaleAction,
            world.Reservations.TryReserveResource(entity.Id, "missing-action", "pile-1", source, ItemIds.Wood, 1));

        StartAction(entity, "haul-1");
        Assert.Equal(
            ReservationResult.InvalidRequest,
            world.Reservations.TryReserveResource(entity.Id, "haul-1", "pile-1", source, ItemIds.Wood, 0));
        Assert.Equal(
            ReservationResult.InvalidRequest,
            world.Reservations.TryReserveCapacity(entity.Id, "haul-1", "stockpile-1", null, 1));
        Assert.Equal(
            ReservationResult.UnknownItem,
            world.Reservations.TryReserveResource(entity.Id, "haul-1", "pile-1", source, "stone", 1));
    }

    private static (World World, EntityRecord First, EntityRecord Second) CreateWorldWithTwoEntities()
    {
        var world = new World();
        return (
            world,
            world.SpawnEntity("human", Vector3.Zero),
            world.SpawnEntity("human", Vector3.One));
    }

    private static void StartAction(EntityRecord entity, string actionId)
    {
        Assert.Equal(
            ActionStartResult.Started,
            entity.Actions.TryStart(new ActionCandidate(actionId, ActionKind.Haul)));
    }

    private static void ReserveBoth(
        World world,
        EntityRecord entity,
        string actionId,
        ResourceContainer source,
        ResourceContainer stockpile)
    {
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(entity.Id, actionId, "pile-1", source, ItemIds.Wood, 1));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(entity.Id, actionId, "stockpile-1", stockpile, stockpile.Capacity));
    }

    public enum ActionFinish
    {
        Complete,
        Cancel,
        Preempt
    }

    public enum RemovalPath
    {
        FoodSource,
        ResourcePile
    }
}
