using System.Numerics;
using Simulation;

namespace Simulation.Tests;

/// <summary>
/// Covers the generic cargo-retention mechanism that any interruptible pickup/deliver job
/// (e.g. the future wood-hauling job) builds on: carried inventory survives preemption,
/// cancellation, and reservation cleanup, and a new pickup cannot start until carried cargo
/// has been delivered.
/// </summary>
public sealed class CargoRetentionTests
{
    [Fact]
    public void HungerPreemptingAHaulActionPreservesCarriedCargo()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var groundPile = new ResourceContainer("ground-pile", 5);
        groundPile.TryAdd(ItemIds.Wood, 3);

        Assert.Equal(
            ActionStartResult.Started,
            entity.Actions.TryStart(new ActionCandidate("haul-1", ActionKind.Haul)));
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2));
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);

        Assert.Equal(
            ActionStartResult.Preempted,
            entity.Actions.TryStart(new ActionCandidate("eat-1", ActionKind.Eat)));

        Assert.Equal("eat-1", entity.Actions.Current?.Id);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
        Assert.Equal(1, groundPile.Counts[ItemIds.Wood]);
    }

    [Fact]
    public void CancellingAHaulActionPreservesCarriedCargo()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var groundPile = new ResourceContainer("ground-pile", 5);
        groundPile.TryAdd(ItemIds.Wood, 1);

        entity.Actions.TryStart(new ActionCandidate("haul-1", ActionKind.Haul));
        ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 1);

        Assert.True(entity.Actions.Cancel());

        Assert.Null(entity.Actions.Current);
        Assert.Equal(1, entity.Inventory[ItemIds.Wood]);
    }

    [Fact]
    public void ReservationReleaseOnCleanupDoesNotTouchCarriedInventory()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var groundPile = new ResourceContainer("ground-pile", 5);
        var stockpile = new ResourceContainer("stockpile", 5);
        groundPile.TryAdd(ItemIds.Wood, 1);

        entity.Actions.TryStart(new ActionCandidate("haul-1", ActionKind.Haul));
        ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 1);
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(entity.Id, "haul-1", "stockpile-1", stockpile, 1));

        Assert.True(entity.Actions.Cancel());

        // Cleanup releases the destination-capacity reservation but must never discard the
        // wood the entity is still physically carrying.
        Assert.Equal(1, entity.Inventory[ItemIds.Wood]);
        var nextEntity = world.SpawnEntity("human", Vector3.One);
        nextEntity.Actions.TryStart(new ActionCandidate("haul-2", ActionKind.Haul));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(nextEntity.Id, "haul-2", "stockpile-1", stockpile, 1));
    }

    [Fact]
    public void EntityRemovalIsTheOnlyPathThatDiscardsCarriedInventory()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var groundPile = new ResourceContainer("ground-pile", 5);
        groundPile.TryAdd(ItemIds.Wood, 1);
        entity.Actions.TryStart(new ActionCandidate("haul-1", ActionKind.Haul));
        ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 1);

        Assert.True(world.TryRemoveEntity(entity.Id));

        Assert.False(world.TryGetEntity(entity.Id, out _));
        // The wood left the world alongside the removed entity; it was not silently zeroed
        // out on a still-present record.
        Assert.Equal(1, entity.Inventory[ItemIds.Wood]);
    }

    [Fact]
    public void ANewPickupCannotStartWhileCargoIsPendingDelivery()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var groundPile = new ResourceContainer("ground-pile", 5);
        groundPile.TryAdd(ItemIds.Wood, 2);
        ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 1);

        Assert.Equal(
            ActionStartResult.CargoPending,
            entity.Actions.TryStart(new ActionCandidate("haul-2", ActionKind.Haul, requiresEmptyCargo: true)));
        Assert.Null(entity.Actions.Current);
    }

    [Fact]
    public void DeliveringCarriedCargoAllowsTheNextPickupToStart()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var groundPile = new ResourceContainer("ground-pile", 5);
        var stockpile = new ResourceContainer("stockpile", 5);
        groundPile.TryAdd(ItemIds.Wood, 2);
        ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 1);

        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 1));

        Assert.Equal(
            ActionStartResult.Started,
            entity.Actions.TryStart(new ActionCandidate("haul-2", ActionKind.Haul, requiresEmptyCargo: true)));
        Assert.Empty(entity.Inventory);
        Assert.Equal(1, stockpile.Counts[ItemIds.Wood]);
    }

    [Fact]
    public void FullInterruptionAndResumptionCycleConservesTotalWood()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero, satiety: 60);
        var groundPile = new ResourceContainer("ground-pile", 5);
        var stockpile = new ResourceContainer("stockpile", 5);
        groundPile.TryAdd(ItemIds.Wood, 3);
        var initialTotal = TotalWood(groundPile, stockpile) + CarriedWood(entity);

        // Pick up wood, then get interrupted by hunger before delivering.
        entity.Actions.TryStart(new ActionCandidate("haul-1", ActionKind.Haul));
        ResourceTransferService.TryPickUp(groundPile, entity, ItemIds.Wood, 2);
        entity.Actions.TryStart(new ActionCandidate("eat-1", ActionKind.Eat));
        Assert.Equal(2, CarriedWood(entity));

        // Eating finishes; the entity must resume delivery before claiming new haul work.
        Assert.True(entity.Actions.Complete());
        Assert.Equal(
            ActionStartResult.CargoPending,
            entity.Actions.TryStart(new ActionCandidate("haul-2", ActionKind.Haul, requiresEmptyCargo: true)));

        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryDeliver(entity, stockpile, ItemIds.Wood, 2));

        Assert.Equal(
            ActionStartResult.Started,
            entity.Actions.TryStart(new ActionCandidate("haul-3", ActionKind.Haul, requiresEmptyCargo: true)));
        Assert.Equal(initialTotal, TotalWood(groundPile, stockpile) + CarriedWood(entity));
        Assert.Equal(2, stockpile.Counts[ItemIds.Wood]);
        Assert.Equal(1, groundPile.Counts[ItemIds.Wood]);
    }

    private static int TotalWood(params ResourceContainer[] containers)
    {
        return containers.Sum(container => container.Counts.GetValueOrDefault(ItemIds.Wood));
    }

    private static int CarriedWood(EntityRecord entity)
    {
        return entity.Inventory.GetValueOrDefault(ItemIds.Wood);
    }
}
