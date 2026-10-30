using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class PlayerWorkstationServiceTests
{
    [Fact]
    public void PlayerCanLoadWorkCompleteMealAndCollectOutput()
    {
        var session = CreateSession();
        session.World.TrySetEntityPosition(session.PlayerEntityId, session.World.Workstations.Single().Position);
        session.Player.TryAddCarried(ItemIds.Plant, 1);
        session.Player.TryAddCarried(ItemIds.Wood, 1);
        var service = new PlayerWorkstationService(session.World);

        Assert.True(service.LoadOne("workbench-1", ItemIds.Plant).Succeeded);
        Assert.True(service.LoadOne("workbench-1", ItemIds.Wood).Succeeded);
        for (var step = 0; step < 5; step++) Assert.True(service.ApplyWork("workbench-1").Succeeded);
        Assert.True(service.CollectOutput("workbench-1").Succeeded);

        Assert.Equal(2, session.Player.Inventory[ItemIds.Food]);
        Assert.Empty(session.World.Workstations.Single().InputStorage.Counts);
        Assert.Empty(session.World.Workstations.Single().OutputStorage.Counts);
    }

    [Fact]
    public void RejectedActionsDoNotMutateInventoryOrProgress()
    {
        var session = CreateSession();
        session.World.TrySetEntityPosition(session.PlayerEntityId, session.World.Workstations.Single().Position);
        var station = session.World.Workstations.Single();
        var service = new PlayerWorkstationService(session.World);

        Assert.Equal(WorkstationActionResult.MissingCarriedItem, service.LoadOne(station.Id, ItemIds.Plant).Result);
        Assert.Equal(WorkstationActionResult.MissingInput, service.ApplyWork(station.Id).Result);
        Assert.Equal(2, session.Player.Inventory.Count);
        Assert.DoesNotContain(ItemIds.Plant, session.Player.Inventory.Keys);
        Assert.Empty(station.InputStorage.Counts);
        Assert.Equal(0, station.CurrentRecipe!.WorkProgress);

        session.World.TrySetEntityPosition(session.PlayerEntityId, station.Position + new Vector3(2.01f, 0, 0));
        Assert.Equal(WorkstationActionResult.OutOfRange, service.LoadOne(station.Id, ItemIds.Plant).Result);
    }

    [Fact]
    public void OutputCapacityFailurePreservesInputsAndProgress()
    {
        var session = CreateSession();
        session.World.TrySetEntityPosition(session.PlayerEntityId, session.World.Workstations.Single().Position);
        var station = session.World.Workstations.Single();
        station.OutputStorage.TryAdd(ItemIds.Food, 9);
        session.Player.TryAddCarried(ItemIds.Plant, 1);
        session.Player.TryAddCarried(ItemIds.Wood, 1);
        var service = new PlayerWorkstationService(session.World);
        service.LoadOne(station.Id, ItemIds.Plant);
        service.LoadOne(station.Id, ItemIds.Wood);

        Assert.Equal(WorkstationActionResult.OutputCapacityExceeded, service.ApplyWork(station.Id).Result);
        Assert.Equal(0, station.CurrentRecipe!.WorkProgress);
        Assert.Equal(2, station.InputStorage.UsedCapacity);
    }

    [Fact]
    public void IncapacitatedPlayerCannotMutateWorkbenchState()
    {
        var session = CreateSession();
        session.World.TrySetEntityPosition(session.PlayerEntityId, session.World.Workstations.Single().Position);
        session.Player.TryAddCarried(ItemIds.Plant, 1);
        var head = session.Player.Health.Parts.Single(part => part.Name == "Head");
        session.Player.Health.TryDamage("Head", head.MaximumHealth);

        var result = new PlayerWorkstationService(session.World).LoadOne("workbench-1", ItemIds.Plant);

        Assert.Equal(WorkstationActionResult.PlayerIncapacitated, result.Result);
        Assert.Equal(1, session.Player.Inventory[ItemIds.Plant]);
        Assert.Empty(session.World.Workstations.Single().InputStorage.Counts);
    }

    [Fact]
    public void LoadingStopsAtTheRecipeRequirementSoOtherInputsCannotBeCrowdedOut()
    {
        var session = CreateSession();
        session.World.TrySetEntityPosition(session.PlayerEntityId, session.World.Workstations.Single().Position);
        session.Player.TryAddCarried(ItemIds.Plant, 2);
        var service = new PlayerWorkstationService(session.World);

        Assert.True(service.LoadOne("workbench-1", ItemIds.Plant).Succeeded);
        Assert.Equal(WorkstationActionResult.InputRequirementSatisfied,
            service.LoadOne("workbench-1", ItemIds.Plant).Result);
        Assert.Equal(1, session.Player.Inventory[ItemIds.Plant]);
        Assert.Equal(1, session.World.Workstations.Single().InputStorage.Counts[ItemIds.Plant]);
    }

    [Fact]
    public void OutputCollectionPartiallyFillsTheRemainingItemStack()
    {
        var session = CreateSession();
        session.World.TrySetEntityPosition(session.PlayerEntityId, session.World.Workstations.Single().Position);
        var station = session.World.Workstations.Single();
        station.OutputStorage.TryAdd(ItemIds.Food, 2);
        session.Player.TryAddCarried(ItemIds.Wood, 999);
        session.Player.TryAddCarried(ItemIds.Food, 998);

        var result = new PlayerWorkstationService(session.World).CollectOutput(station.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(999, session.Player.Inventory[ItemIds.Food]);
        Assert.Equal(1, station.OutputStorage.Counts[ItemIds.Food]);

        Assert.Equal(WorkstationActionResult.CarriedItemStackFull,
            new PlayerWorkstationService(session.World).CollectOutput(station.Id).Result);
        Assert.Equal(1, station.OutputStorage.Counts[ItemIds.Food]);
    }

    private static NewGameSession CreateSession() => NewGameSession.Create(
        SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json")),
        PlayerCharacterProfile.Create("Maker"));
}
