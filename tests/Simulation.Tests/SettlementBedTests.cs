using System.Numerics;
using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class SettlementBedTests
{
    [Fact]
    public void AvailableBedCanBeReservedOnlyOnceAndReleased()
    {
        var bed = new SettlementBed("bed-1", Vector3.Zero);
        Assert.True(bed.TryReserve("entity-1"));
        Assert.False(bed.TryReserve("entity-2"));
        Assert.False(bed.Release("entity-2"));
        Assert.True(bed.Release("entity-1"));
        Assert.False(bed.IsOccupied);
    }

    [Fact]
    public void SleepRequiresMatchingReservationAndWakeClearsState()
    {
        var bed = new SettlementBed("bed-1", Vector3.Zero);
        var sleep = new CharacterSleep();
        Assert.False(sleep.TryBegin(bed, "entity-1"));
        Assert.True(bed.TryReserve("entity-1"));
        Assert.True(sleep.TryBegin(bed, "entity-1"));
        Assert.Equal(SleepState.Sleeping, sleep.State);
        sleep.Wake();
        Assert.Equal(SleepState.Awake, sleep.State);
        Assert.Null(sleep.BedId);
    }

    [Fact]
    public void RegistryAssignsAvailableBedsAndResetsOccupancy()
    {
        var registry = new SettlementBedRegistry();
        registry.Add(new SettlementBed("bed-1", Vector3.Zero));
        Assert.True(registry.TryAssignAvailable("entity-1", out var bed));
        Assert.Equal("entity-1", bed!.OccupantId);
        Assert.False(registry.TryAssignAvailable("entity-2", out _));
        registry.Reset();
        Assert.True(registry.TryAssignAvailable("entity-2", out _));
    }

    [Fact]
    public void CompletedShelterBedSupportsPlayerAndRejectsDoubleBooking()
    {
        var world = new World();
        var first = world.SpawnEntity("human", Vector3.Zero, 100);
        var second = world.SpawnEntity("human", Vector3.Zero, 100);
        var hut = world.AddStructure("hut", StructureType.Hut, Vector3.Zero);
        hut.AddBed(new Bed("hut-bed-1"));
        var rest = new SettlementRestService(world);

        Assert.True(rest.Sleep(first.Id, hut.Id).Succeeded);
        Assert.Equal(SleepState.Sleeping, first.SleepState);
        Assert.Equal(RestActionResult.NoAvailableBed, rest.Sleep(second.Id, hut.Id).Result);
        Assert.Equal(RestActionResult.Success, rest.Wake(first.Id).Result);
        Assert.Equal(SleepState.Awake, first.SleepState);
        Assert.True(rest.Sleep(second.Id, hut.Id).Succeeded);
    }

    [Fact]
    public void BedAndSleepStateRoundTripThroughSnapshot()
    {
        var world = new World();
        var actor = world.SpawnEntity("human", Vector3.Zero, 100);
        var hut = world.AddStructure("hut", StructureType.Hut, Vector3.Zero);
        hut.AddBed(new Bed("hut-bed-1"));
        Assert.True(new SettlementRestService(world).Sleep(actor.Id, hut.Id).Succeeded);

        var restored = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        WorldSnapshotSerializer.Load(restored, WorldSnapshotSerializer.Serialize(world, 2), catalog);
        var restoredActor = restored.Entities.Single(entity => entity.Id == actor.Id);
        var restoredBed = Assert.Single(Assert.Single(restored.Structures).Beds);
        Assert.Equal(SleepState.Sleeping, restoredActor.SleepState);
        Assert.Equal(restoredBed.Id, restoredActor.BedId);
        Assert.Equal(actor.Id, restoredBed.OccupantId);
    }
}

