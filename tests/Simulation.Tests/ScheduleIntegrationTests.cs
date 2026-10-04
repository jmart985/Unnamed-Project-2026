using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class ScheduleIntegrationTests
{
    [Fact]
    public void DayLoopWithOverridesAndInterruptedCargoIsRepeatableAndAtomic()
    {
        Assert.Equal(RunScenario(), RunScenario());
    }

    private static string RunScenario()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var world = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Ada"), 42).World;
        var npc = world.Entities.First(e => e.SpeciesId == "human" && e.Id != world.PlayerEntityId);
        var species = catalog.GetRequired("human");
        var nav = new FakeNavigationAdapter();
        var hauling = new WoodHaulingService(world, _ => nav);
        var food = new FoodConsumptionService(world, catalog, _ => nav);
        var squad = new SquadOrderService(world, _ => nav);
        var clock = new SimulationClock();
        long tick = 0;
        void AdvanceTo(double time)
        {
            while (clock.SimulationTime < time - 1e-8)
                clock.Advance(0.1, seconds => { tick++; npc.Needs.Advance(seconds, satietyOverride: species.HungerRate); hauling.ReconcileSchedule(npc, clock.SimulationTime); });
        }
        void Arrive(Vector3 target)
        {
            world.TrySetEntityPosition(npc.Id, target);
            foreach (var request in nav.Requests.Where(pair => pair.Value == NavigationOutcome.InProgress))
                Assert.True(nav.CompleteAsArrived(request.Key));
        }

        AdvanceTo(99.9);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, hauling.Advance(npc, species, tick, clock.SimulationTime).Status);
        AdvanceTo(100);
        Assert.Equal("Going home", npc.Activity);
        Assert.Contains(NavigationOutcome.Cancelled, nav.Requests.Values);
        Assert.Contains(NavigationOutcome.InProgress, nav.Requests.Values);
        Assert.Empty(npc.Inventory);

        // Needs stay active at rest; food uses the real selection, reservation and consumption path.
        npc.Needs.Advance((npc.Satiety - 20) / species.HungerRate, satietyOverride: species.HungerRate);
        Assert.Equal(WoodHaulingStatus.NeedsFood, hauling.Advance(npc, species, tick, clock.SimulationTime).Status);
        var eating = food.Advance(npc, species, tick);
        Assert.Equal(FoodConsumptionStatus.Traveling, eating.Status);
        Assert.Equal(ActionKind.Eat, npc.Actions.Current!.Kind);
        hauling.ReconcileSchedule(npc, clock.SimulationTime);
        Arrive(eating.Source!.Position);
        Assert.Equal(FoodConsumptionStatus.Ate, food.Advance(npc, species, ++tick).Status);
        Assert.Equal(399, food.FoodUnitsRemaining);
        Assert.Equal(1, food.FoodUnitsConsumed);

        world.AddSquadMember(npc.Id);
        Assert.Equal(SquadResult.Accepted, squad.Execute(SquadOrderKind.MoveTo, new(4, 0, 3)).Code);
        squad.Advance();
        hauling.ReconcileSchedule(npc, clock.SimulationTime);
        Assert.Equal(ActionKind.SquadMove, npc.Actions.Current!.Kind);
        Arrive(new(4, 0, 3));
        squad.Advance();
        squad.Execute(SquadOrderKind.Stop);
        world.RemoveSquadMember(npc.Id);

        var frozen = WorldSnapshotSerializer.Serialize(world, clock.SimulationTime);
        clock.SetPaused(true);
        clock.Advance(1, _ => throw new InvalidOperationException("Paused tick"));
        Assert.Equal(frozen, WorldSnapshotSerializer.Serialize(world, clock.SimulationTime));
        clock.SetPaused(false);
        clock.SetSpeed(SimulationSpeed.FourX);
        clock.Advance(0.1, seconds => { tick++; npc.Needs.Advance(seconds, satietyOverride: species.HungerRate); });
        Assert.Equal(100.4, clock.SimulationTime, 6);
        clock.SetSpeed(SimulationSpeed.OneX);
        AdvanceTo(240);
        Assert.True(NpcWorkSchedule.Default.Evaluate(clock.SimulationTime).IsWorking);
        var pickup = hauling.Advance(npc, species, ++tick, clock.SimulationTime);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, pickup.Status);
        Arrive(pickup.Source!.Position);
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, hauling.Advance(npc, species, ++tick, clock.SimulationTime).Status);
        var cargo = npc.Inventory[ItemIds.Wood];

        // A direct order interrupts a carried trip; its inventory remains authoritative.
        world.AddSquadMember(npc.Id);
        squad.Execute(SquadOrderKind.MoveTo, new(2, 0, 2));
        squad.Advance();
        Assert.Equal(ActionKind.SquadMove, npc.Actions.Current!.Kind);
        Assert.Equal(cargo, npc.Inventory[ItemIds.Wood]);
        Arrive(new(2, 0, 2));
        squad.Advance();
        squad.Execute(SquadOrderKind.Stop);
        world.RemoveSquadMember(npc.Id);
        AdvanceTo(340);
        Assert.False(NpcWorkSchedule.Default.Evaluate(clock.SimulationTime).IsWorking);
        Assert.Equal(WoodHaulingStatus.NeedsFood, hauling.Advance(npc, species, ++tick, clock.SimulationTime).Status);
        eating = food.Advance(npc, species, tick);
        Arrive(eating.Source!.Position);
        Assert.Equal(FoodConsumptionStatus.Ate, food.Advance(npc, species, ++tick).Status);
        Assert.Equal(cargo, npc.Inventory[ItemIds.Wood]);
        Assert.Equal(398, food.FoodUnitsRemaining);
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, hauling.Advance(npc, species, ++tick, clock.SimulationTime).Status);
        var saved = WorldSnapshotSerializer.Serialize(world, clock.SimulationTime);
        var invalid = JsonNode.Parse(saved)!;
        invalid["WorkSchedules"] = new JsonArray(new JsonObject { ["EntityId"] = npc.Id, ["StartHour"] = 4, ["EndHour"] = 4 });
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(world, invalid.ToJsonString(), catalog, food, hauling, squad));
        Assert.Equal(saved, WorldSnapshotSerializer.Serialize(world, clock.SimulationTime));
        Assert.Equal(ActionKind.Haul, npc.Actions.Current!.Kind);
        clock.Restore(WorldSnapshotSerializer.Load(world, saved, catalog, food, hauling, squad));
        npc = world.Entities.Single(e => e.Id == npc.Id);
        Assert.Equal(cargo, npc.Inventory[ItemIds.Wood]);
        Assert.DoesNotContain(NavigationOutcome.InProgress, nav.Requests.Values);
        Assert.Equal(SquadOrderKind.None, squad.Order);
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, hauling.Advance(npc, species, ++tick, clock.SimulationTime).Status);
        Arrive(world.Stockpiles[0].Position);
        Assert.Equal(WoodHaulingStatus.Delivered, hauling.Advance(npc, species, ++tick, clock.SimulationTime).Status);
        Assert.Equal(WoodHaulingStatus.Resting, hauling.Advance(npc, species, ++tick, clock.SimulationTime).Status);
        Assert.Empty(npc.Inventory);
        Assert.Equal(100, world.ResourcePiles.Sum(p => p.Contents.Counts.GetValueOrDefault(ItemIds.Wood)) + world.Stockpiles.Sum(p => p.Contents.Counts.GetValueOrDefault(ItemIds.Wood)));
        Assert.Equal(0, world.Reservations.ReleaseForEntity(npc.Id));
        return WorldSnapshotSerializer.Serialize(world, clock.SimulationTime);
    }
}
