using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class NpcScheduleTests
{
    private static SpeciesCatalog Catalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    private static World Session() => NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Ada"), 42).World;
    private static EntityRecord Npc(World world) => world.Entities.First(e => e.SpeciesId == "human" && e.Id != world.PlayerEntityId);

    [Theory]
    [InlineData(0, true, 100, 18)] [InlineData(99.9, true, 0.1, 18)]
    [InlineData(100, false, 140, 8)] [InlineData(239.9, false, 0.1, 8)]
    [InlineData(240, true, 100, 18)] [InlineData(580, false, 140, 8)]
    public void DefaultWindowHasInclusiveStartExclusiveEndAndRepeats(double time, bool working, double until, double next)
    {
        var state = NpcWorkSchedule.Default.Evaluate(time);
        Assert.Equal(working, state.IsWorking);
        Assert.Equal(until, state.SecondsUntilTransition, 6);
        Assert.Equal(next, state.NextTransitionHour);
    }

    [Theory]
    [InlineData(140, true, 80)] [InlineData(160, true, 60)] [InlineData(220, false, 160)]
    public void OvernightWindowCrossesMidnight(double time, bool working, double until)
    {
        var state = new NpcWorkSchedule(22, 6).Evaluate(time);
        Assert.Equal(working, state.IsWorking);
        Assert.Equal(until, state.SecondsUntilTransition);
    }

    [Fact]
    public void FixedStepPauseSpeedAndAccumulatedBoundaryAgree()
    {
        var clock = new SimulationClock();
        var phases = new List<bool>();
        for (var i = 0; i < 1000; i++) clock.Advance(0.1, _ => phases.Add(NpcWorkSchedule.Default.Evaluate(clock.SimulationTime).IsWorking));
        Assert.True(phases[^2]);
        Assert.False(phases[^1]);
        clock.SetPaused(true);
        var time = clock.SimulationTime;
        clock.Advance(5, _ => throw new Exception("paused clock ticked"));
        Assert.Equal(time, clock.SimulationTime);
        clock.SetPaused(false);
        clock.SetSpeed(SimulationSpeed.FourX);
        clock.Advance(0.1, _ => { });
        Assert.Equal(time + 0.4, clock.SimulationTime, 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => NpcWorkSchedule.Default.Evaluate(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => NpcWorkSchedule.Default.Evaluate(-1));
    }

    [Fact]
    public void SettingsRejectAtomicallyAndRemovalResetClearOverrides()
    {
        var world = Session();
        var npc = Npc(world);
        Assert.Equal(ScheduleUpdateResult.Accepted, world.SetWorkSchedule(npc.Id, 22, 6));
        Assert.Equal(ScheduleUpdateResult.Unchanged, world.SetWorkSchedule(npc.Id, 22, 6));
        foreach (var (start, end) in new[] { (-1, 6), (24, 6), (22, 24), (6, 6) })
            Assert.Equal(ScheduleUpdateResult.InvalidHours, world.SetWorkSchedule(npc.Id, start, end));
        Assert.Equal(ScheduleUpdateResult.IneligibleEntity, world.SetWorkSchedule(world.PlayerEntityId, 22, 6));
        Assert.Equal(ScheduleUpdateResult.IneligibleEntity, world.SetWorkSchedule(world.Entities.First(e => e.SpeciesId == "wolf").Id, 22, 6));
        Assert.Equal(ScheduleUpdateResult.InvalidEntity, world.SetWorkSchedule("missing", 22, 6));
        Assert.Equal(new NpcWorkSchedule(22, 6), world.GetWorkSchedule(npc.Id));
        Assert.Single(world.WorkSchedules);
        world.TryRemoveEntity(npc.Id);
        Assert.Empty(world.WorkSchedules);
        npc = Npc(world);
        world.SetWorkSchedule(npc.Id, 6, 12);
        world.SetWorkSchedule(npc.Id, 8, 18);
        Assert.Empty(world.WorkSchedules);
        world.SetWorkSchedule(npc.Id, 6, 12);
        world.Reset();
        Assert.Empty(world.WorkSchedules);
    }

    [Fact]
    public void RestCancelsPickupAndReleasesReservationsThenNextDayResumes()
    {
        var world = Session();
        var npc = Npc(world);
        var nav = new FakeNavigationAdapter();
        var hauling = new WoodHaulingService(world, _ => nav);
        var species = Catalog().GetRequired("human");
        var result = hauling.Advance(npc, species, 1, 99.9);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, result.Status);
        var source = result.Source!;
        var other = world.SpawnEntity("human", Vector3.Zero);
        other.Actions.TryStart(new("competing", ActionKind.Haul));
        Assert.Equal(ReservationResult.InsufficientQuantity, world.Reservations.TryReserveResource(other.Id, "competing", source.Id, source.Contents, ItemIds.Wood, 100));
        Assert.True(hauling.ReconcileSchedule(npc, 100));
        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests.Values.Single());
        Assert.Null(npc.Actions.Current);
        Assert.Equal(ReservationResult.Reserved, world.Reservations.TryReserveResource(other.Id, "competing", source.Id, source.Contents, ItemIds.Wood, 100));
        var stockpile = world.Stockpiles[0];
        Assert.Equal(ReservationResult.Reserved, world.Reservations.TryReserveCapacity(other.Id, "competing", stockpile.Id, stockpile.Contents, stockpile.Contents.RemainingCapacity));
        other.Actions.Cancel();
        Assert.Equal(WoodHaulingStatus.Resting, hauling.Advance(npc, species, 2, 100).Status);
        Assert.Equal("Resting", npc.Activity);
        Assert.Empty(npc.Inventory);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, hauling.Advance(npc, species, 3, 240).Status);
        Assert.Equal(2, nav.Requests.Count);
    }

    [Fact]
    public void RestAllowsCarriedDeliveryButNeverAnotherPickup()
    {
        var world = Session();
        var npc = Npc(world);
        var nav = new FakeNavigationAdapter();
        var hauling = new WoodHaulingService(world, _ => nav);
        var species = Catalog().GetRequired("human");
        var pickup = hauling.Advance(npc, species, 1, 99);
        nav.CompleteAsArrived(nav.Requests.Keys.Single());
        world.TrySetEntityPosition(npc.Id, pickup.Source!.Position);
        var delivery = hauling.Advance(npc, species, 2, 99.9);
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, delivery.Status);
        var amount = npc.Inventory[ItemIds.Wood];
        Assert.False(hauling.ReconcileSchedule(npc, 100));
        npc.Actions.Cancel(); // Interrupted delivery must be recoverable during rest.
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, hauling.Advance(npc, species, 3, 100).Status);
        nav.CompleteAsArrived(nav.Requests.Keys.Last());
        world.TrySetEntityPosition(npc.Id, delivery.Stockpile!.Position);
        Assert.Equal(WoodHaulingStatus.Delivered, hauling.Advance(npc, species, 4, 100).Status);
        Assert.Equal(amount, world.Stockpiles[0].Contents.Counts[ItemIds.Wood]);
        Assert.Empty(npc.Inventory);
        Assert.Equal(100, world.ResourcePiles[0].Contents.Counts[ItemIds.Wood] + world.Stockpiles[0].Contents.Counts[ItemIds.Wood]);
        Assert.Equal(WoodHaulingStatus.Resting, hauling.Advance(npc, species, 5, 100).Status);
        Assert.DoesNotContain(NavigationOutcome.InProgress, nav.Requests.Values);
    }

    [Theory]
    [InlineData(ActionKind.Eat)] [InlineData(ActionKind.SquadMove)]
    public void RestNeverCancelsHigherPriorityActions(ActionKind kind)
    {
        var world = Session();
        var npc = Npc(world);
        var cancelled = false;
        npc.Actions.TryStart(new("override", kind), () => cancelled = true);
        var hauling = new WoodHaulingService(world, _ => new FakeNavigationAdapter());
        Assert.True(hauling.ReconcileSchedule(npc, 100));
        Assert.False(cancelled);
        Assert.Equal(kind, npc.Actions.Current!.Kind);
    }

    [Fact]
    public void RestRequestsFoodForHungryNpcAndDoesNotSchedulePlayerOrCreatures()
    {
        var world = Session();
        var hungry = world.SpawnEntity("human", Vector3.Zero, satiety: 20);
        var service = new WoodHaulingService(world, _ => new FakeNavigationAdapter());
        Assert.Equal(WoodHaulingStatus.NeedsFood, service.Advance(hungry, Catalog().GetRequired("human"), 1, 100).Status);
        foreach (var entity in world.Entities.Where(e => e.Id == world.PlayerEntityId || e.SpeciesId != "human"))
        {
            Assert.False(world.HasDailySchedule(entity));
            Assert.False(service.ReconcileSchedule(entity, 100));
            Assert.NotEqual("Resting", entity.Activity);
        }
    }

    [Fact]
    public void SnapshotRoundTripRetainsHoursClockAndMembership()
    {
        var world = Session();
        var npc = Npc(world);
        world.SetWorkSchedule(npc.Id, 22, 6);
        world.AddSquadMember(npc.Id);
        var json = WorldSnapshotSerializer.Serialize(world, 200);
        world.Reset();
        Assert.Equal(200, WorldSnapshotSerializer.Load(world, json, Catalog()));
        Assert.Equal(json, WorldSnapshotSerializer.Serialize(world, 200));
        Assert.Equal(new NpcWorkSchedule(22, 6), world.GetWorkSchedule(npc.Id));
        Assert.True(world.GetWorkSchedule(npc.Id).Evaluate(200).IsWorking);
        Assert.Contains(npc.Id, world.SquadMemberIds);
    }

    [Theory]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    public void LegacyMigrationDefaultsHoursAndPreservesVersionNineMembership(int version)
    {
        var world = Session();
        var npc = Npc(world);
        world.AddSquadMember(npc.Id);
        world.SetWorkSchedule(npc.Id, 22, 6);
        var json = JsonNode.Parse(WorldSnapshotSerializer.Serialize(world, 0))!;
        json["SchemaVersion"] = version;
        json.AsObject().Remove("WorkSchedules");
        WorldSnapshotSerializer.Load(world, json.ToJsonString(), Catalog());
        Assert.Empty(world.WorkSchedules);
        Assert.Equal(NpcWorkSchedule.Default, world.GetWorkSchedule(npc.Id));
        Assert.Equal(version == 9 ? 1 : 0, world.SquadMemberIds.Count);
        Assert.Equal(10, WorldSnapshotSerializer.Capture(world, 0).SchemaVersion);
    }

    [Theory]
    [InlineData("missing")] [InlineData("player")] [InlineData("creature")]
    [InlineData("duplicate")] [InlineData("hours")] [InlineData("null")] [InlineData("absent")]
    public void InvalidScheduleLoadPreservesWorldAndActiveNavigation(string invalidKind)
    {
        var world = Session();
        var npc = Npc(world);
        world.SetWorkSchedule(npc.Id, 22, 6);
        var nav = new FakeNavigationAdapter();
        var hauling = new WoodHaulingService(world, _ => nav);
        hauling.Advance(npc, Catalog().GetRequired("human"), 1, 200);
        var before = WorldSnapshotSerializer.Serialize(world, 200);
        var json = JsonNode.Parse(before)!;
        var schedules = json["WorkSchedules"]!.AsArray();
        var setting = schedules[0]!;
        switch (invalidKind)
        {
            case "missing": setting["EntityId"] = "missing"; break;
            case "player": setting["EntityId"] = world.PlayerEntityId; break;
            case "creature": setting["EntityId"] = world.Entities.First(e => e.SpeciesId == "wolf").Id; break;
            case "duplicate": schedules.Add(setting.DeepClone()); break;
            case "hours": setting["EndHour"] = 22; break;
            case "null": schedules.Add((JsonNode?)null); break;
            case "absent": json.AsObject().Remove("WorkSchedules"); break;
        }
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(world, json.ToJsonString(), Catalog(), hauling));
        Assert.Equal(before, WorldSnapshotSerializer.Serialize(world, 200));
        Assert.Equal(NavigationOutcome.InProgress, nav.Requests.Values.Single());
        Assert.Equal(ActionKind.Haul, npc.Actions.Current!.Kind);
    }
}
