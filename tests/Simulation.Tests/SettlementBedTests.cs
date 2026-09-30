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
}

