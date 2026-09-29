using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class WoodHaulingServiceTests
{
    [Fact]
    public void HaulerPicksUpAndDepositsOnlyReservedWoodWithinCapacity()
    {
        var world = new World(inventoryCapacity: 3);
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var source = AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 10, wood: 5);
        var stockpile = AddLocation(world.AddStockpile, "stockpile", new Vector3(2, 0, 0), capacity: 2);
        var navigator = new FakeNavigationAdapter();
        var service = new WoodHaulingService(world, _ => navigator);
        var species = GetSpecies("human");
        var initialWood = TotalWood(source, entity, stockpile);

        var toSource = service.Advance(entity, species, simulationTick: 0);

        Assert.Equal(WoodHaulingStatus.TravelingToSource, toSource.Status);
        Assert.Equal(2, toSource.Amount);
        Assert.Equal(5, source.Contents.Counts[ItemIds.Wood]);
        Assert.Empty(entity.Inventory);
        Assert.Equal(2, stockpile.Contents.RemainingCapacity);

        var sourceRequest = navigator.Requests.Keys.Single();
        navigator.CompleteAsArrived(sourceRequest);
        world.TrySetEntityPosition(entity.Id, source.Position);
        var toStockpile = service.Advance(entity, species, simulationTick: 1);

        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, toStockpile.Status);
        Assert.Equal(3, source.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
        Assert.Equal(2, stockpile.Contents.RemainingCapacity);

        var stockpileRequest = navigator.Requests.Keys.Single(id => id != sourceRequest);
        navigator.CompleteAsArrived(stockpileRequest);
        world.TrySetEntityPosition(entity.Id, stockpile.Position);
        var delivered = service.Advance(entity, species, simulationTick: 2);

        Assert.Equal(WoodHaulingStatus.Delivered, delivered.Status);
        Assert.Equal(2, delivered.Amount);
        Assert.Empty(entity.Inventory);
        Assert.Equal(2, stockpile.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(initialWood, TotalWood(source, entity, stockpile));
        Assert.Null(entity.Actions.Current);
    }

    [Fact]
    public void CompetingHaulersCannotReserveTheSameFinalWoodOrStorageCapacity()
    {
        var world = new World(inventoryCapacity: 2);
        var first = world.SpawnEntity("human", Vector3.Zero);
        var second = world.SpawnEntity("human", Vector3.One);
        var source = AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 1, wood: 1);
        AddLocation(world.AddStockpile, "stockpile", new Vector3(2, 0, 0), capacity: 1);
        var navigators = new Dictionary<string, FakeNavigationAdapter>(StringComparer.Ordinal)
        {
            [first.Id] = new FakeNavigationAdapter(),
            [second.Id] = new FakeNavigationAdapter()
        };
        var service = new WoodHaulingService(world, entity => navigators[entity.Id]);

        var firstTrip = service.Advance(first, GetSpecies("human"), simulationTick: 0);
        var secondTrip = service.Advance(second, GetSpecies("human"), simulationTick: 0);

        Assert.Equal(WoodHaulingStatus.TravelingToSource, firstTrip.Status);
        Assert.Equal(WoodHaulingStatus.Waiting, secondTrip.Status);
        Assert.Equal("Wood or stockpile capacity is already reserved.", secondTrip.WaitingReason);
        Assert.Equal(1, source.Contents.Counts[ItemIds.Wood]);
        Assert.Empty(first.Inventory);
        Assert.Empty(second.Inventory);
    }

    [Fact]
    public void CompetingHaulersCannotOverreserveStockpileCapacity()
    {
        var world = new World(inventoryCapacity: 2);
        var first = world.SpawnEntity("human", Vector3.Zero);
        var second = world.SpawnEntity("human", Vector3.One);
        var source = AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 5, wood: 5);
        var stockpile = AddLocation(world.AddStockpile, "stockpile", new Vector3(2, 0, 0), capacity: 2);
        var navigators = new Dictionary<string, FakeNavigationAdapter>(StringComparer.Ordinal)
        {
            [first.Id] = new FakeNavigationAdapter(),
            [second.Id] = new FakeNavigationAdapter()
        };
        var service = new WoodHaulingService(world, entity => navigators[entity.Id]);

        var firstTrip = service.Advance(first, GetSpecies("human"), simulationTick: 0);
        var secondTrip = service.Advance(second, GetSpecies("human"), simulationTick: 0);

        Assert.Equal(WoodHaulingStatus.TravelingToSource, firstTrip.Status);
        Assert.Equal(2, firstTrip.Amount);
        Assert.Equal(WoodHaulingStatus.Waiting, secondTrip.Status);
        Assert.Equal(5, source.Contents.Counts[ItemIds.Wood]);
        Assert.Empty(first.Inventory);
        Assert.Empty(second.Inventory);
        Assert.Empty(stockpile.Contents.Counts);
    }

    [Fact]
    public void PickupReleasesTheSourceClaimWhileRetainingTheStockpileReservation()
    {
        var world = new World(inventoryCapacity: 2);
        var first = world.SpawnEntity("human", Vector3.Zero);
        var second = world.SpawnEntity("human", Vector3.One);
        var source = AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 10, wood: 6);
        var firstStockpile = AddLocation(
            world.AddStockpile,
            "stockpile-near",
            new Vector3(2, 0, 0),
            capacity: 2);
        var secondStockpile = AddLocation(
            world.AddStockpile,
            "stockpile-far",
            new Vector3(5, 0, 0),
            capacity: 2);
        var navigators = new Dictionary<string, FakeNavigationAdapter>(StringComparer.Ordinal)
        {
            [first.Id] = new FakeNavigationAdapter(),
            [second.Id] = new FakeNavigationAdapter()
        };
        var service = new WoodHaulingService(world, entity => navigators[entity.Id]);
        var species = GetSpecies("human");

        var firstTrip = service.Advance(first, species, simulationTick: 0);
        var firstSourceRequest = navigators[first.Id].Requests.Keys.Single();
        navigators[first.Id].CompleteAsArrived(firstSourceRequest);
        world.TrySetEntityPosition(first.Id, source.Position);
        var firstDelivery = service.Advance(first, species, simulationTick: 1);

        var secondTrip = service.Advance(second, species, simulationTick: 1);

        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, firstDelivery.Status);
        Assert.Equal(firstStockpile, firstDelivery.Stockpile);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, secondTrip.Status);
        Assert.Equal(secondStockpile, secondTrip.Stockpile);
        Assert.Equal(4, source.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(2, first.Inventory[ItemIds.Wood]);
        Assert.Empty(second.Inventory);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, firstTrip.Status);
    }

    [Fact]
    public void NonHaulerNeverClaimsWoodOrStartsAnAction()
    {
        var world = new World();
        var entity = world.SpawnEntity("creature", Vector3.Zero);
        var source = AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 1, wood: 1);
        var service = new WoodHaulingService(world, _ => new FakeNavigationAdapter());

        var result = service.Advance(entity, GetSpecies("creature"), simulationTick: 0);

        Assert.Equal(WoodHaulingStatus.NotHauler, result.Status);
        Assert.Null(entity.Actions.Current);
        Assert.Equal(1, source.Contents.Counts[ItemIds.Wood]);
    }

    [Fact]
    public void HungryHaulerKeepsFoodWaitingStateInsteadOfStartingWoodWork()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero, satiety: 20);
        var source = AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 2, wood: 2);
        var stockpile = AddLocation(world.AddStockpile, "stockpile", new Vector3(2, 0, 0), capacity: 2);
        var foodService = new FoodConsumptionService(world, _ => new FakeNavigationAdapter());
        var hauling = new WoodHaulingService(world, _ => new FakeNavigationAdapter());
        var species = GetSpecies("human");

        var hungryWaiting = foodService.Advance(entity, species, simulationTick: 0);
        var haulingResult = hauling.Advance(entity, species, simulationTick: 0);

        Assert.Equal(FoodConsumptionStatus.Waiting, hungryWaiting.Status);
        Assert.Equal("No available food.", hungryWaiting.WaitingReason);
        Assert.Equal(WoodHaulingStatus.NeedsFood, haulingResult.Status);
        Assert.Equal("No available food.", haulingResult.WaitingReason);
        Assert.Equal("Waiting", entity.Activity);
        Assert.Equal("No available food.", entity.WaitingReason);
        Assert.Null(entity.Actions.Current);
        Assert.Equal(2, source.Contents.Counts[ItemIds.Wood]);
        Assert.Empty(stockpile.Contents.Counts);
    }

    [Fact]
    public void FailedPickupLeavesBothContainersUnchangedAndReleasesClaims()
    {
        var world = new World(inventoryCapacity: 2);
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var source = AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 2, wood: 2);
        var stockpile = AddLocation(world.AddStockpile, "stockpile", new Vector3(2, 0, 0), capacity: 2);
        var navigator = new FakeNavigationAdapter();
        var service = new WoodHaulingService(world, _ => navigator);

        Assert.Equal(
            WoodHaulingStatus.TravelingToSource,
            service.Advance(entity, GetSpecies("human"), simulationTick: 0).Status);
        source.Contents.TryRemove(ItemIds.Wood, 2);
        var woodBeforeFailure = TotalWood(source, entity, stockpile);
        var requestId = navigator.Requests.Keys.Single();
        navigator.CompleteAsArrived(requestId);
        world.TrySetEntityPosition(entity.Id, source.Position);

        var failed = service.Advance(entity, GetSpecies("human"), simulationTick: 1);

        Assert.Equal(WoodHaulingStatus.Waiting, failed.Status);
        Assert.Contains("Wood pickup failed", failed.WaitingReason);
        Assert.Empty(entity.Inventory);
        Assert.Empty(source.Contents.Counts);
        Assert.Empty(stockpile.Contents.Counts);
        Assert.Null(entity.Actions.Current);
        var competitor = world.SpawnEntity("human", Vector3.Zero);
        competitor.Actions.TryStart(new ActionCandidate("test-haul", ActionKind.Haul));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(
                competitor.Id,
                "test-haul",
                stockpile.Id,
                stockpile.Contents,
                2));
        Assert.Equal(woodBeforeFailure, TotalWood(source, entity, stockpile));
    }

    [Fact]
    public void FullStockpileRetainsCargoReleasesReservationAndRecoversAfterSpaceIsFreed()
    {
        var world = new World(inventoryCapacity: 2);
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var source = AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 2, wood: 2);
        var stockpile = AddLocation(world.AddStockpile, "stockpile", new Vector3(2, 0, 0), capacity: 2);
        var navigator = new FakeNavigationAdapter();
        var service = new WoodHaulingService(world, _ => navigator, retryDelayTicks: 10);
        var species = GetSpecies("human");
        var initialWood = TotalWood(source, entity, stockpile);

        Assert.Equal(
            WoodHaulingStatus.TravelingToSource,
            service.Advance(entity, species, simulationTick: 0).Status);
        var sourceRequest = navigator.Requests.Keys.Single();
        Assert.True(navigator.CompleteAsArrived(sourceRequest));
        Assert.True(world.TrySetEntityPosition(entity.Id, source.Position));
        Assert.Equal(
            WoodHaulingStatus.TravelingToStockpile,
            service.Advance(entity, species, simulationTick: 1).Status);

        Assert.Equal(ResourceMutationResult.Success, stockpile.Contents.TryAdd(ItemIds.Wood, 2));
        var stockpileRequest = navigator.Requests.Single(pair => pair.Value == NavigationOutcome.InProgress).Key;
        Assert.True(navigator.CompleteAsArrived(stockpileRequest));
        Assert.True(world.TrySetEntityPosition(entity.Id, stockpile.Position));

        var failed = service.Advance(entity, species, simulationTick: 2);

        Assert.Equal(WoodHaulingStatus.Waiting, failed.Status);
        Assert.Contains("Wood deposit failed: CapacityExceeded", failed.WaitingReason);
        Assert.Equal("Waiting", entity.Activity);
        Assert.Equal(failed.WaitingReason, entity.WaitingReason);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
        Assert.Equal(2, stockpile.Contents.Counts[ItemIds.Wood]);
        Assert.Empty(source.Contents.Counts);
        Assert.Null(entity.Actions.Current);

        Assert.Equal(ResourceMutationResult.Success, stockpile.Contents.TryRemove(ItemIds.Wood, 2));
        var competitor = world.SpawnEntity("human", Vector3.Zero);
        Assert.Equal(
            ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("test-haul", ActionKind.Haul)));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(
                competitor.Id,
                "test-haul",
                stockpile.Id,
                stockpile.Contents,
                2));
        Assert.True(competitor.Actions.Cancel());

        Assert.Equal(
            WoodHaulingStatus.RetryDeferred,
            service.Advance(entity, species, simulationTick: 11).Status);
        Assert.Equal(
            WoodHaulingStatus.TravelingToStockpile,
            service.Advance(entity, species, simulationTick: 12).Status);
        var retryRequest = navigator.Requests.Single(pair => pair.Value == NavigationOutcome.InProgress).Key;
        Assert.True(navigator.CompleteAsArrived(retryRequest));
        Assert.True(world.TrySetEntityPosition(entity.Id, stockpile.Position));

        var delivered = service.Advance(entity, species, simulationTick: 13);

        Assert.Equal(WoodHaulingStatus.Delivered, delivered.Status);
        Assert.Equal(2, delivered.Amount);
        Assert.Empty(entity.Inventory);
        Assert.Equal(2, stockpile.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(initialWood, TotalWood(source, entity, stockpile));
        Assert.Null(entity.Actions.Current);
    }

    [Fact]
    public void RemovedWoodSourceReleasesReservationsAndSelectsAnAlternateSource()
    {
        var world = new World(inventoryCapacity: 2);
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var removedSource = AddLocation(world.AddResourcePile, "wood-near", Vector3.UnitX, capacity: 2, wood: 2);
        var alternateSource = AddLocation(
            world.AddResourcePile,
            "wood-far",
            new Vector3(2, 0, 0),
            capacity: 2,
            wood: 2);
        var stockpile = AddLocation(world.AddStockpile, "stockpile", new Vector3(3, 0, 0), capacity: 4);
        var navigator = new FakeNavigationAdapter();
        var service = new WoodHaulingService(world, _ => navigator, retryDelayTicks: 10);
        var species = GetSpecies("human");
        const int initialWood = 4;

        var started = service.Advance(entity, species, simulationTick: 0);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, started.Status);
        Assert.Same(removedSource, started.Source);
        Assert.True(world.TryRemoveResourcePile(removedSource.Id));

        var removed = service.Advance(entity, species, simulationTick: 1);

        Assert.Equal(WoodHaulingStatus.Waiting, removed.Status);
        Assert.Equal("The wood source was removed.", removed.WaitingReason);
        Assert.Equal("Waiting", entity.Activity);
        Assert.Equal(removed.WaitingReason, entity.WaitingReason);
        Assert.Empty(entity.Inventory);
        Assert.Equal(2, removedSource.Contents.Counts[ItemIds.Wood]);
        Assert.Null(entity.Actions.Current);

        var competitor = world.SpawnEntity("human", Vector3.Zero);
        Assert.Equal(
            ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("capacity-check", ActionKind.Haul)));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveCapacity(
                competitor.Id,
                "capacity-check",
                stockpile.Id,
                stockpile.Contents,
                2));
        Assert.True(competitor.Actions.Cancel());

        Assert.Equal(
            WoodHaulingStatus.RetryDeferred,
            service.Advance(entity, species, simulationTick: 10).Status);
        var replacement = service.Advance(entity, species, simulationTick: 11);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, replacement.Status);
        Assert.Same(alternateSource, replacement.Source);

        var sourceRequest = navigator.Requests.Single(pair => pair.Value == NavigationOutcome.InProgress).Key;
        Assert.True(navigator.CompleteAsArrived(sourceRequest));
        Assert.True(world.TrySetEntityPosition(entity.Id, alternateSource.Position));
        Assert.Equal(
            WoodHaulingStatus.TravelingToStockpile,
            service.Advance(entity, species, simulationTick: 12).Status);
        Assert.True(world.TryRemoveResourcePile(alternateSource.Id));

        var stockpileRequest = navigator.Requests.Single(pair => pair.Value == NavigationOutcome.InProgress).Key;
        Assert.True(navigator.CompleteAsArrived(stockpileRequest));
        Assert.True(world.TrySetEntityPosition(entity.Id, stockpile.Position));
        var delivered = service.Advance(entity, species, simulationTick: 13);

        Assert.Equal(WoodHaulingStatus.Delivered, delivered.Status);
        Assert.Empty(entity.Inventory);
        Assert.Equal(2, stockpile.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(
            initialWood,
            removedSource.Contents.Counts.GetValueOrDefault(ItemIds.Wood)
                + alternateSource.Contents.Counts.GetValueOrDefault(ItemIds.Wood)
                + entity.Inventory.GetValueOrDefault(ItemIds.Wood)
                + stockpile.Contents.Counts.GetValueOrDefault(ItemIds.Wood));
    }

    [Fact]
    public void RemovedStockpilesReleaseReservationsRetainCargoAndSelectAReplacement()
    {
        var world = new World(inventoryCapacity: 2);
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var source = AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 2, wood: 2);
        var removedBeforePickup = AddLocation(
            world.AddStockpile,
            "stockpile-near",
            new Vector3(2, 0, 0),
            capacity: 2);
        var removedAfterPickup = AddLocation(
            world.AddStockpile,
            "stockpile-far",
            new Vector3(3, 0, 0),
            capacity: 2);
        var navigator = new FakeNavigationAdapter();
        var service = new WoodHaulingService(world, _ => navigator, retryDelayTicks: 10);
        var species = GetSpecies("human");

        var started = service.Advance(entity, species, simulationTick: 0);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, started.Status);
        Assert.Same(removedBeforePickup, started.Stockpile);
        Assert.True(world.TryRemoveStockpile(removedBeforePickup.Id));

        var firstRemoval = service.Advance(entity, species, simulationTick: 1);

        Assert.Equal(WoodHaulingStatus.Waiting, firstRemoval.Status);
        Assert.Equal("The stockpile target was removed.", firstRemoval.WaitingReason);
        Assert.Equal("Waiting", entity.Activity);
        Assert.Equal(firstRemoval.WaitingReason, entity.WaitingReason);
        Assert.Empty(entity.Inventory);
        Assert.Equal(2, source.Contents.Counts[ItemIds.Wood]);
        Assert.Null(entity.Actions.Current);

        var competitor = world.SpawnEntity("human", Vector3.Zero);
        Assert.Equal(
            ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("resource-check", ActionKind.Haul)));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(
                competitor.Id,
                "resource-check",
                source.Id,
                source.Contents,
                ItemIds.Wood,
                2));
        Assert.True(competitor.Actions.Cancel());

        var replacementTrip = service.Advance(entity, species, simulationTick: 11);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, replacementTrip.Status);
        Assert.Same(removedAfterPickup, replacementTrip.Stockpile);
        var sourceRequest = navigator.Requests.Single(pair => pair.Value == NavigationOutcome.InProgress).Key;
        Assert.True(navigator.CompleteAsArrived(sourceRequest));
        Assert.True(world.TrySetEntityPosition(entity.Id, source.Position));
        Assert.Equal(
            WoodHaulingStatus.TravelingToStockpile,
            service.Advance(entity, species, simulationTick: 12).Status);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);

        Assert.True(world.TryRemoveStockpile(removedAfterPickup.Id));
        var secondRemoval = service.Advance(entity, species, simulationTick: 13);

        Assert.Equal(WoodHaulingStatus.Waiting, secondRemoval.Status);
        Assert.Equal("The stockpile target was removed.", secondRemoval.WaitingReason);
        Assert.Equal("Waiting", entity.Activity);
        Assert.Equal(secondRemoval.WaitingReason, entity.WaitingReason);
        Assert.Equal(2, entity.Inventory[ItemIds.Wood]);
        Assert.Empty(source.Contents.Counts);
        Assert.Null(entity.Actions.Current);

        var replacementStockpile = AddLocation(
            world.AddStockpile,
            "stockpile-replacement",
            new Vector3(4, 0, 0),
            capacity: 2);
        Assert.Equal(
            WoodHaulingStatus.RetryDeferred,
            service.Advance(entity, species, simulationTick: 22).Status);
        var resumed = service.Advance(entity, species, simulationTick: 23);
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, resumed.Status);
        Assert.Same(replacementStockpile, resumed.Stockpile);

        var stockpileRequest = navigator.Requests.Single(pair => pair.Value == NavigationOutcome.InProgress).Key;
        Assert.True(navigator.CompleteAsArrived(stockpileRequest));
        Assert.True(world.TrySetEntityPosition(entity.Id, replacementStockpile.Position));
        var delivered = service.Advance(entity, species, simulationTick: 24);

        Assert.Equal(WoodHaulingStatus.Delivered, delivered.Status);
        Assert.Empty(entity.Inventory);
        Assert.Equal(2, replacementStockpile.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(2, TotalWood(source, entity, replacementStockpile));
    }

    [Fact]
    public void WaitingDefersRetryUntilConfiguredDelayElapses()
    {
        var world = new World(inventoryCapacity: 2);
        var entity = world.SpawnEntity("human", Vector3.Zero);
        AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 5, wood: 2);
        var service = new WoodHaulingService(world, _ => new FakeNavigationAdapter(), retryDelayTicks: 10);
        var species = GetSpecies("human");

        var waiting = service.Advance(entity, species, simulationTick: 0);
        var deferred = service.Advance(entity, species, simulationTick: 1);

        Assert.Equal(WoodHaulingStatus.Waiting, waiting.Status);
        Assert.Equal("No stockpile is available.", waiting.WaitingReason);
        Assert.Equal(WoodHaulingStatus.RetryDeferred, deferred.Status);
        Assert.Equal("No stockpile is available.", deferred.WaitingReason);

        AddLocation(world.AddStockpile, "stockpile-1", new Vector3(2, 0, 0), capacity: 2);
        Assert.Equal(
            WoodHaulingStatus.RetryDeferred,
            service.Advance(entity, species, simulationTick: 9).Status);

        var resumed = service.Advance(entity, species, simulationTick: 10);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, resumed.Status);
        Assert.Equal("SeekingWood", entity.Activity);
        Assert.Null(entity.WaitingReason);
    }

    [Fact]
    public void PendingPickupRetryDoesNotBlockDeliveryWhenCargoExists()
    {
        var world = new World(inventoryCapacity: 2);
        var entity = world.SpawnEntity("human", Vector3.Zero);
        AddLocation(world.AddResourcePile, "wood-pile", Vector3.UnitX, capacity: 5, wood: 2);
        var carriedSource = new ResourceContainer("carried-source", 2);
        Assert.Equal(ResourceMutationResult.Success, carriedSource.TryAdd(ItemIds.Wood, 1));
        var navigator = new FakeNavigationAdapter();
        var service = new WoodHaulingService(world, _ => navigator, retryDelayTicks: 10);
        var species = GetSpecies("human");

        Assert.Equal(WoodHaulingStatus.Waiting, service.Advance(entity, species, simulationTick: 0).Status);
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(carriedSource, entity, ItemIds.Wood, 1));
        AddLocation(world.AddStockpile, "stockpile-1", new Vector3(2, 0, 0), capacity: 2);

        var resumedDelivery = service.Advance(entity, species, simulationTick: 1);

        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, resumedDelivery.Status);
        Assert.Equal(1, resumedDelivery.Amount);
        Assert.Equal("DeliveringWood", entity.Activity);
        Assert.Null(entity.WaitingReason);
    }

    private static SpeciesDefinition GetSpecies(string id)
    {
        return SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"))
            .GetRequired(id);
    }

    private static ResourceLocation AddLocation(
        Func<string, Vector3, ResourceContainer, ResourceLocation> addLocation,
        string id,
        Vector3 position,
        int capacity,
        int wood = 0)
    {
        var contents = new ResourceContainer(id, capacity);
        if (wood > 0)
        {
            Assert.Equal(ResourceMutationResult.Success, contents.TryAdd(ItemIds.Wood, wood));
        }

        return addLocation(id, position, contents);
    }

    private static int TotalWood(
        ResourceLocation source,
        EntityRecord entity,
        ResourceLocation stockpile)
    {
        return source.Contents.Counts.GetValueOrDefault(ItemIds.Wood)
            + entity.Inventory.GetValueOrDefault(ItemIds.Wood)
            + stockpile.Contents.Counts.GetValueOrDefault(ItemIds.Wood);
    }
}
