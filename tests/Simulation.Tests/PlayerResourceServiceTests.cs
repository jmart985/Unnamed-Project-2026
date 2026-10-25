using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class PlayerResourceServiceTests
{
    private static (NewGameSession Session, PlayerResourceService Service, ResourceContainer Contents) Setup()
    {
        var species = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var session = NewGameSession.Create(species, PlayerCharacterProfile.Create("Collector"));
        var contents = new ResourceContainer("fixture", 100); contents.TryAdd(ItemIds.Food, 20); contents.TryAdd(ItemIds.Wood, 20);
        session.World.AddFoodSource("fixture", session.Player.Position + Vector3.UnitX, contents);
        return (session, new(session.World, species), contents);
    }

    [Fact]
    public void PickupDoesNotConsumeAndUseConsumesOnlyCarriedFoodOnce()
    {
        var (session, service, source) = Setup();
        var satiety = session.Player.Satiety;
        Assert.Equal(PlayerResourceResultCode.Collected, service.Collect(PlayerResourceKind.Food, "fixture").Code);
        Assert.Equal(1, session.Player.Inventory[ItemIds.Food]); Assert.Equal(19, source.Counts[ItemIds.Food]);
        Assert.Equal(satiety, session.Player.Satiety);
        Assert.Equal(PlayerResourceResultCode.Used, service.UseCarriedFood().Code);
        Assert.False(session.Player.Inventory.ContainsKey(ItemIds.Food));
        Assert.Equal(19, source.Counts[ItemIds.Food]);
        Assert.Equal(Math.Min(100, satiety + session.Player.Needs.NutritionPerUnit), session.Player.Satiety);
        Assert.Equal(PlayerResourceResultCode.NotUsable, service.UseCarriedFood().Code);
    }

    [Fact]
    public void MissingDistantEmptyFullAndIncapacitatedRejectionsDoNotMutate()
    {
        var (session, service, source) = Setup();
        Assert.Equal(PlayerResourceResultCode.Missing, service.Collect(PlayerResourceKind.Food, "gone").Code);
        session.World.TrySetEntityPosition(session.Player.Id, session.Player.Position + new Vector3(10, 0, 0));
        Assert.Equal(PlayerResourceResultCode.OutOfRange, service.Collect(PlayerResourceKind.Food, "fixture").Code);
        Assert.Equal(20, source.Counts[ItemIds.Food]); Assert.Empty(session.Player.Inventory);
        session.World.TrySetEntityPosition(session.Player.Id, session.World.FoodSources.Single(x => x.Id == "fixture").Position);
        session.Player.TryAddCarried(ItemIds.Food, 999);
        session.Player.TryAddCarried(ItemIds.Wood, 999);
        var before = source.Counts[ItemIds.Food];
        Assert.Equal(PlayerResourceResultCode.Full, service.Collect(PlayerResourceKind.Food, "fixture").Code);
        Assert.Equal(before, source.Counts[ItemIds.Food]);
        session.Player.Health.TryDamage("Torso", 1000);
        Assert.Equal(PlayerResourceResultCode.Unavailable, service.UseCarriedFood().Code);
        Assert.Equal(PlayerResourceResultCode.Unavailable, service.Collect(PlayerResourceKind.Food, "fixture").Code);
        Assert.Equal(999, session.Player.Inventory[ItemIds.Food]);
        Assert.Equal(999, session.Player.Inventory[ItemIds.Wood]);
        var (emptySession, emptyService, empty) = Setup();
        empty.TryRemove(ItemIds.Food, 20); empty.TryRemove(ItemIds.Wood, 20);
        Assert.Equal(PlayerResourceResultCode.Empty, emptyService.Collect(PlayerResourceKind.Food, "fixture").Code);
        Assert.Empty(emptySession.Player.Inventory);
    }

    [Fact]
    public void PickupHonorsNpcReservationsAndTypedResourceIdentity()
    {
        var (session, service, source) = Setup();
        source.TryRemove(ItemIds.Wood, 20);
        var npc = session.World.Entities.First(entity => entity.Id != session.Player.Id);
        npc.Actions.TryStart(new("reserved", ActionKind.Eat));
        Assert.Equal(ReservationResult.Reserved, session.World.Reservations.TryReserveResource(npc.Id, "reserved", "fixture", source, ItemIds.Food, 20));
        Assert.Equal(PlayerResourceResultCode.Reserved, service.Collect(PlayerResourceKind.Food, "fixture").Code);
        Assert.Equal(20, source.Counts[ItemIds.Food]); Assert.Empty(session.Player.Inventory);
        var wood = new ResourceContainer("wood", 5); wood.TryAdd(ItemIds.Wood, 2);
        session.World.AddResourcePile("wood-fixture", session.Player.Position, wood);
        Assert.Equal(PlayerResourceResultCode.Missing, service.Collect(PlayerResourceKind.Food, "wood-fixture").Code);
        Assert.Equal(PlayerResourceResultCode.Collected, service.Collect(PlayerResourceKind.ResourcePile, "wood-fixture").Code);
        Assert.Equal(1, session.Player.Inventory[ItemIds.Wood]);
        Assert.Equal(PlayerResourceResultCode.NotUsable, service.UseCarriedFood().Code);
        session.World.Reservations.ReleaseForEntity(npc.Id);
        Assert.Equal(PlayerResourceResultCode.Collected, service.Collect(PlayerResourceKind.Food, "fixture").Code);
    }

    [Fact]
    public void FullSatietyDoesNotWasteCarriedFoodAndCollectedInventoryRoundTrips()
    {
        var (session, service, source) = Setup();
        service.Collect(PlayerResourceKind.Food, "fixture");
        session.Player.Needs.AddNutrition(100);
        Assert.Equal(PlayerResourceResultCode.NotUsable, service.UseCarriedFood().Code);
        Assert.Equal(1, session.Player.Inventory[ItemIds.Food]); Assert.Equal(19, source.Counts[ItemIds.Food]);
        var snapshot = WorldSnapshotSerializer.Serialize(session.World, 0);
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var restored = new World();
        WorldSnapshotSerializer.Load(restored, snapshot, catalog);
        Assert.Equal(1, restored.Entities.Single(entity => entity.Id == restored.PlayerEntityId).Inventory[ItemIds.Food]);
    }

    [Fact]
    public void PressReleaseHoldEchoAndCancellationHaveExclusiveOutcomes()
    {
        var gesture = new CollectUseGesture();
        gesture.Press(); gesture.Advance(0.2); gesture.Press();
        Assert.Equal(0.2, gesture.Elapsed);
        Assert.Equal(CollectUseIntent.Collect, gesture.Release());
        Assert.Equal(CollectUseIntent.None, gesture.Release());
        gesture.Press();
        Assert.Equal(CollectUseIntent.Use, gesture.Advance(0.6));
        Assert.Equal(CollectUseIntent.None, gesture.Advance(20));
        Assert.Equal(CollectUseIntent.None, gesture.Release());
        gesture.Press(); gesture.Advance(0.4); gesture.Cancel();
        Assert.Equal(CollectUseIntent.None, gesture.Release());
        Assert.Equal(CollectUseIntent.None, gesture.Advance(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => gesture.Advance(double.NaN));
    }
}
