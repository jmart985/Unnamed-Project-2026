using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class FoodConsumptionServiceTests
{
    [Fact]
    public void ConsumesOneReservedUnitOnlyAfterArrivalAndAppliesNutritionOnce()
    {
        var scenario = CreateScenario();

        var started = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 0);

        Assert.Equal(FoodConsumptionStatus.Traveling, started.Status);
        Assert.Equal(2, scenario.Source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(20, scenario.Entity.Satiety);
        Assert.Equal("SeekingFood", scenario.Entity.Activity);

        Assert.True(scenario.Navigator.CompleteAsArrived(scenario.Navigator.Requests.Keys.Single()));
        scenario.World.TrySetEntityPosition(scenario.Entity.Id, scenario.Source.Position);
        var ate = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Ate, ate.Status);
        Assert.Equal(scenario.Source, ate.Source);
        Assert.False(ate.IsHungryAfterEating);
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(2,
            scenario.Source.Contents.Counts[ItemIds.Food] + scenario.Service.FoodUnitsConsumed);
        Assert.Equal(70, scenario.Entity.Satiety);
        Assert.Equal(1, scenario.Service.FoodUnitsConsumed);
        Assert.Null(scenario.Entity.Actions.Current);
        Assert.Equal("Idle", scenario.Entity.Activity);

        Assert.Equal(
            FoodConsumptionStatus.NotHungry,
            scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 2).Status);
        Assert.Equal(70, scenario.Entity.Satiety);
        Assert.Equal(1, scenario.Service.FoodUnitsConsumed);
    }

    [Fact]
    public void InProgressNavigationCannotConsumeFoodOrApplyNutrition()
    {
        var scenario = CreateScenario(amount: 1);

        var started = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 0);
        var traveling = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Traveling, started.Status);
        Assert.Equal(FoodConsumptionStatus.Traveling, traveling.Status);
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(20, scenario.Entity.Satiety);
        Assert.Equal(0, scenario.Service.FoodUnitsConsumed);
    }

    [Fact]
    public void UnreachableCandidateReleasesReservationAndStartsTravelToAlternateSource()
    {
        var scenario = CreateScenario(amount: 1);
        var alternate = AddSource(scenario.World, "alternate", new Vector3(2, 0, 0), amount: 1);
        var started = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 0);
        var firstRequestId = scenario.Navigator.Requests.Keys.Single();

        Assert.True(scenario.Navigator.MarkUnreachable(firstRequestId));
        var retried = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Traveling, started.Status);
        Assert.Equal(FoodConsumptionStatus.Traveling, retried.Status);
        Assert.Equal(alternate, retried.Source);
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(1, alternate.Contents.Counts[ItemIds.Food]);
        Assert.Equal(20, scenario.Entity.Satiety);
        Assert.Equal(0, scenario.Service.FoodUnitsConsumed);

        var competitor = CreateHungryEntity(scenario.World);
        var competitorNavigator = new FakeNavigationAdapter();
        var competitorService = new FoodConsumptionService(
            scenario.World,
            _ => competitorNavigator);
        var competitorResult = competitorService.Advance(competitor, scenario.Species, simulationTick: 0);

        Assert.Equal(FoodConsumptionStatus.Traveling, competitorResult.Status);
        Assert.Equal(scenario.Source, competitorResult.Source);
    }

    [Fact]
    public void ConsumesTheReservedCompatibleDietItemFromAFoodSource()
    {
        var world = new World();
        var entity = world.SpawnEntity("wood-eater", Vector3.Zero, satiety: 20);
        var species = LoadSpecies(
            """[{"id":"wood-eater","movementSpeed":1,"hungerRate":0.2,"diet":["wood"],"canHaul":false}]""",
            "wood-eater");
        var source = AddSource(world, "logpile", Vector3.UnitX, (ItemIds.Wood, 2));
        var navigator = new FakeNavigationAdapter();
        var service = new FoodConsumptionService(world, _ => navigator);

        var started = service.Advance(entity, species, simulationTick: 0);

        Assert.Equal(FoodConsumptionStatus.Traveling, started.Status);
        navigator.CompleteAsArrived(navigator.Requests.Keys.Single());
        world.TrySetEntityPosition(entity.Id, source.Position);

        var ate = service.Advance(entity, species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Ate, ate.Status);
        Assert.Equal(1, source.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(70, entity.Satiety);
    }

    [Fact]
    public void UnreachableOnlySourceWaitsAndRetriesAfterBoundedDelay()
    {
        var scenario = CreateScenario(amount: 1);
        var started = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 0);
        var requestId = scenario.Navigator.Requests.Keys.Single();
        Assert.True(scenario.Navigator.MarkUnreachable(requestId));

        var waiting = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Traveling, started.Status);
        Assert.Equal(FoodConsumptionStatus.Waiting, waiting.Status);
        Assert.Equal("No reachable food available.", waiting.WaitingReason);
        Assert.Equal("Waiting", scenario.Entity.Activity);
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(20, scenario.Entity.Satiety);
        Assert.Equal(0, scenario.Service.FoodUnitsConsumed);
        Assert.Null(scenario.Entity.Actions.Current);

        var competitor = CreateHungryEntity(scenario.World);
        var competitorService = new FoodConsumptionService(
            scenario.World,
            _ => new FakeNavigationAdapter());
        var competitorResult = competitorService.Advance(
            competitor,
            scenario.Species,
            simulationTick: 0);

        Assert.Equal(FoodConsumptionStatus.Traveling, competitorResult.Status);
        Assert.Equal(scenario.Source, competitorResult.Source);
        Assert.True(competitor.Actions.Cancel());

        var deferred = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 10);
        Assert.Equal(FoodConsumptionStatus.RetryDeferred, deferred.Status);
        Assert.Equal("No reachable food available.", deferred.WaitingReason);

        var retried = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 11);
        Assert.Equal(FoodConsumptionStatus.Traveling, retried.Status);
        Assert.Equal(scenario.Source, retried.Source);
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(20, scenario.Entity.Satiety);
        Assert.Equal(0, scenario.Service.FoodUnitsConsumed);
    }

    [Fact]
    public void ArrivalOutsideToleranceDoesNotConsumeAndReleasesTheReservation()
    {
        var scenario = CreateScenario(amount: 1);
        var started = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 0);
        var requestId = scenario.Navigator.Requests.Keys.Single();

        Assert.True(scenario.Navigator.CompleteAsArrived(requestId));
        var result = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Waiting, result.Status);
        Assert.Equal("No reachable food available.", result.WaitingReason);
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(20, scenario.Entity.Satiety);
        Assert.Equal(0, scenario.Service.FoodUnitsConsumed);
        Assert.Null(scenario.Entity.Actions.Current);

        var competitor = CreateHungryEntity(scenario.World);
        var competitorNavigator = new FakeNavigationAdapter();
        var competitorService = new FoodConsumptionService(scenario.World, _ => competitorNavigator);
        Assert.Equal(
            FoodConsumptionStatus.Traveling,
            competitorService.Advance(competitor, scenario.Species, simulationTick: 0).Status);
        Assert.Equal(FoodConsumptionStatus.Traveling, started.Status);
    }

    [Fact]
    public void DepletedSourceIsNotConsumedTwiceAndAlternateCanBeUsed()
    {
        var scenario = CreateScenario(amount: 1);
        var alternate = AddSource(scenario.World, "alternate", new Vector3(2, 0, 0), amount: 1);
        scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 0);
        scenario.Source.Contents.TryRemove(ItemIds.Food, 1);
        scenario.Navigator.CompleteAsArrived(scenario.Navigator.Requests.Keys.Single());
        scenario.World.TrySetEntityPosition(scenario.Entity.Id, scenario.Source.Position);

        var result = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Traveling, result.Status);
        Assert.Equal(alternate, result.Source);
        Assert.Empty(scenario.Source.Contents.Counts);
        Assert.Equal(20, scenario.Entity.Satiety);
        Assert.Equal(0, scenario.Service.FoodUnitsConsumed);

        Assert.Equal(ResourceMutationResult.Success, scenario.Source.Contents.TryAdd(ItemIds.Food, 1));
        var competitor = CreateHungryEntity(scenario.World);
        var competitorService = new FoodConsumptionService(
            scenario.World,
            _ => new FakeNavigationAdapter());
        var competitorResult = competitorService.Advance(
            competitor,
            scenario.Species,
            simulationTick: 0);

        Assert.Equal(FoodConsumptionStatus.Traveling, competitorResult.Status);
        Assert.Equal(scenario.Source, competitorResult.Source);
    }

    [Fact]
    public void RemovedFoodSourceReleasesTravelAndLeavesNeedsAndFoodUntouched()
    {
        var scenario = CreateScenario(amount: 1);
        scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 0);
        var requestId = scenario.Navigator.Requests.Keys.Single();

        Assert.True(scenario.World.TryRemoveFoodSource(scenario.Source.Id));
        var result = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Waiting, result.Status);
        Assert.Equal("No available food.", result.WaitingReason);
        Assert.Equal("Waiting", scenario.Entity.Activity);
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(20, scenario.Entity.Satiety);
        Assert.Equal(NavigationOutcome.Cancelled, scenario.Navigator.Poll(requestId, 0));
        Assert.Equal(0, scenario.Service.FoodUnitsConsumed);
        Assert.Null(scenario.Entity.Actions.Current);

        var competitor = CreateHungryEntity(scenario.World);
        Assert.Equal(
            ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("removed-target-check", ActionKind.Eat)));
        Assert.Equal(
            ReservationResult.StaleTarget,
            scenario.World.Reservations.TryReserveResource(
                competitor.Id,
                "removed-target-check",
                scenario.Source.Id,
                scenario.Source.Contents,
                ItemIds.Food,
                1));
        competitor.Actions.Cancel();

        var deferred = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 10);
        Assert.Equal(FoodConsumptionStatus.RetryDeferred, deferred.Status);
        Assert.Equal("No available food.", deferred.WaitingReason);

        var retried = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 11);
        Assert.Equal(FoodConsumptionStatus.Waiting, retried.Status);
        Assert.Equal("No available food.", retried.WaitingReason);
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(20, scenario.Entity.Satiety);
        Assert.Equal(0, scenario.Service.FoodUnitsConsumed);
    }

    [Fact]
    public void RemovedFoodSourceSelectsAlternateWithoutRemoteConsumption()
    {
        var scenario = CreateScenario(amount: 1);
        var alternate = AddSource(scenario.World, "alternate", new Vector3(2, 0, 0), amount: 1);
        scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 0);
        var requestId = scenario.Navigator.Requests.Keys.Single();

        Assert.True(scenario.World.TryRemoveFoodSource(scenario.Source.Id));
        var result = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Traveling, result.Status);
        Assert.Equal(alternate, result.Source);
        Assert.Equal(NavigationOutcome.Cancelled, scenario.Navigator.Poll(requestId, 0));
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(1, alternate.Contents.Counts[ItemIds.Food]);
        Assert.Equal(20, scenario.Entity.Satiety);
        Assert.Equal(0, scenario.Service.FoodUnitsConsumed);

        var competitor = CreateHungryEntity(scenario.World);
        Assert.Equal(
            ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("removed-target-check", ActionKind.Eat)));
        Assert.Equal(
            ReservationResult.StaleTarget,
            scenario.World.Reservations.TryReserveResource(
                competitor.Id,
                "removed-target-check",
                scenario.Source.Id,
                scenario.Source.Contents,
                ItemIds.Food,
                1));
        competitor.Actions.Cancel();
    }

    [Fact]
    public void CancellingEatingCancelsTravelAndAllowsTheHungryEntityToResume()
    {
        var scenario = CreateScenario(amount: 1);
        scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 0);
        var firstRequestId = scenario.Navigator.Requests.Keys.Single();

        Assert.True(scenario.Entity.Actions.Cancel());
        Assert.Equal(NavigationOutcome.Cancelled, scenario.Navigator.Poll(firstRequestId, 0));
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);

        var resumed = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Traveling, resumed.Status);
        Assert.Equal(scenario.Source, resumed.Source);
        Assert.Equal(2, scenario.Navigator.Requests.Count);
        Assert.Equal(1, scenario.Source.Contents.Counts[ItemIds.Food]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalFoodUnitCannotBeClaimedOrConsumedByTwoHungryEntities(bool reverseClaimOrder)
    {
        var world = new World();
        var first = CreateHungryEntity(world);
        var second = CreateHungryEntity(world);
        var source = AddSource(world, "last-food", Vector3.UnitX, amount: 1);
        var navigators = new Dictionary<string, FakeNavigationAdapter>(StringComparer.Ordinal)
        {
            [first.Id] = new FakeNavigationAdapter(),
            [second.Id] = new FakeNavigationAdapter()
        };
        var service = new FoodConsumptionService(world, entity => navigators[entity.Id]);
        var species = GetHumanSpecies();
        var claimants = reverseClaimOrder ? new[] { second, first } : new[] { first, second };
        var winner = claimants[0];
        var waitingClaimant = claimants[1];
        var initialFood = service.FoodUnitsRemaining;

        Assert.Equal(
            FoodConsumptionStatus.Traveling,
            service.Advance(winner, species, simulationTick: 0).Status);
        var waiting = service.Advance(waitingClaimant, species, simulationTick: 0);
        Assert.Equal(FoodConsumptionStatus.Waiting, waiting.Status);
        Assert.Equal("No available food.", waiting.WaitingReason);
        Assert.Equal("Waiting", waitingClaimant.Activity);
        Assert.Null(waitingClaimant.Actions.Current);
        Assert.Equal(initialFood, service.FoodUnitsRemaining + service.FoodUnitsConsumed);

        Assert.True(navigators[winner.Id].CompleteAsArrived(navigators[winner.Id].Requests.Keys.Single()));
        Assert.True(world.TrySetEntityPosition(winner.Id, source.Position));
        var ate = service.Advance(winner, species, simulationTick: 1);

        Assert.Equal(FoodConsumptionStatus.Ate, ate.Status);
        Assert.Equal(0, source.Contents.Counts.GetValueOrDefault(ItemIds.Food));
        Assert.Equal(1, service.FoodUnitsConsumed);
        Assert.Equal(20, waitingClaimant.Satiety);
        Assert.Equal(FoodConsumptionStatus.RetryDeferred,
            service.Advance(waitingClaimant, species, simulationTick: 9).Status);
        var retried = service.Advance(waitingClaimant, species, simulationTick: 10);
        Assert.Equal(FoodConsumptionStatus.Waiting, retried.Status);
        Assert.Equal("No available food.", retried.WaitingReason);
        Assert.Equal(1, service.FoodUnitsConsumed);
        Assert.Equal(initialFood, service.FoodUnitsRemaining + service.FoodUnitsConsumed);
    }

    [Fact]
    public void RemainingAndConsumedCountersReconcileAcrossFoodSources()
    {
        var world = new World();
        var first = CreateHungryEntity(world);
        var second = CreateHungryEntity(world);
        var nearSource = AddSource(world, "near", Vector3.UnitX, amount: 2);
        AddSource(world, "far", new Vector3(0, 0, 4), amount: 1);
        var firstNavigator = new FakeNavigationAdapter();
        var secondNavigator = new FakeNavigationAdapter();
        var navigators = new Dictionary<string, FakeNavigationAdapter>(StringComparer.Ordinal)
        {
            [first.Id] = firstNavigator,
            [second.Id] = secondNavigator
        };
        var service = new FoodConsumptionService(
            world,
            entity => navigators[entity.Id]);
        var species = GetHumanSpecies();
        var initialFood = service.FoodUnitsRemaining;

        var firstTrip = service.Advance(first, species, simulationTick: 0);
        var secondTrip = service.Advance(second, species, simulationTick: 0);

        Assert.Equal(nearSource, firstTrip.Source);
        Assert.Equal(nearSource, secondTrip.Source);
        Assert.Equal(3, service.FoodUnitsRemaining);
        Assert.Equal(0, service.FoodUnitsConsumed);

        firstNavigator.CompleteAsArrived(firstNavigator.Requests.Keys.Single());
        secondNavigator.CompleteAsArrived(secondNavigator.Requests.Keys.Single());
        world.TrySetEntityPosition(first.Id, nearSource.Position);
        world.TrySetEntityPosition(second.Id, nearSource.Position);

        Assert.Equal(FoodConsumptionStatus.Ate, service.Advance(first, species, simulationTick: 1).Status);
        Assert.Equal(FoodConsumptionStatus.Ate, service.Advance(second, species, simulationTick: 1).Status);
        Assert.Equal(1, service.FoodUnitsRemaining);
        Assert.Equal(2, service.FoodUnitsConsumed);
        Assert.Equal(initialFood, service.FoodUnitsRemaining + service.FoodUnitsConsumed);
    }

    [Fact]
    public void EmptySourceRetriesAfterDelayAndRecoversWhenReplenished()
    {
        var scenario = CreateScenario(amount: 0);

        var waiting = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 0);
        Assert.Equal(FoodConsumptionStatus.Waiting, waiting.Status);
        Assert.Equal("No available food.", waiting.WaitingReason);
        Assert.Equal(0, scenario.Source.Contents.Counts.GetValueOrDefault(ItemIds.Food));
        Assert.Equal(
            ResourceMutationResult.Success,
            scenario.Source.Contents.TryAdd(ItemIds.Food, 1));
        Assert.Equal(
            FoodConsumptionStatus.RetryDeferred,
            scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 9).Status);
        var recovered = scenario.Service.Advance(scenario.Entity, scenario.Species, simulationTick: 10);

        Assert.Equal(FoodConsumptionStatus.Traveling, recovered.Status);
        Assert.Equal(scenario.Source, recovered.Source);
    }

    [Fact]
    public void StillHungryAfterNutritionSelectsAnotherEatingActionBeforeWorkResumes()
    {
        var world = new World(new NeedsTuning(initialSatiety: 0, hungryThreshold: 60));
        var entity = world.SpawnEntity("human", Vector3.Zero, satiety: 0);
        var source = AddSource(world, "food", Vector3.UnitX, amount: 2);
        AddLocation(world.AddResourcePile, "wood-pile", new Vector3(2, 0, 0), capacity: 2, wood: 2);
        AddLocation(world.AddStockpile, "stockpile", new Vector3(3, 0, 0), capacity: 2);
        var navigator = new FakeNavigationAdapter();
        var service = new FoodConsumptionService(world, _ => navigator);
        var hauling = new WoodHaulingService(world, _ => new FakeNavigationAdapter());
        var species = GetHumanSpecies();

        Assert.Equal(FoodConsumptionStatus.Traveling,
            service.Advance(entity, species, simulationTick: 0).Status);
        var firstRequestId = navigator.Requests.Keys.Single();
        navigator.CompleteAsArrived(firstRequestId);
        world.TrySetEntityPosition(entity.Id, source.Position);

        var ate = service.Advance(entity, species, simulationTick: 1);
        Assert.Equal("Hungry", entity.Activity);
        var haulingWhileHungry = hauling.Advance(entity, species, simulationTick: 1);
        var reevaluated = service.Advance(entity, species, simulationTick: 2);

        Assert.Equal(FoodConsumptionStatus.Ate, ate.Status);
        Assert.True(ate.IsHungryAfterEating);
        Assert.Equal(WoodHaulingStatus.NeedsFood, haulingWhileHungry.Status);
        Assert.Null(entity.WaitingReason);
        Assert.Equal(FoodConsumptionStatus.Traveling, reevaluated.Status);
        Assert.Equal("SeekingFood", entity.Activity);
        Assert.Equal(ActionKind.Eat, entity.Actions.Current?.Kind);
        Assert.Equal(1, source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(1, service.FoodUnitsConsumed);
    }

    [Fact]
    public void NavigationRequestFailureCancelsActionAndReleasesReservation()
    {
        var world = new World();
        var entity = CreateHungryEntity(world);
        var source = AddSource(world, "food", Vector3.UnitX, amount: 1);
        var service = new FoodConsumptionService(world, _ => new ThrowingNavigationAdapter());

        Assert.Throws<InvalidOperationException>(() =>
            service.Advance(entity, GetHumanSpecies(), simulationTick: 0));

        Assert.Null(entity.Actions.Current);
        Assert.Equal(1, source.Contents.Counts[ItemIds.Food]);
        var competitor = CreateHungryEntity(world);
        var competitorService = new FoodConsumptionService(world, _ => new FakeNavigationAdapter());
        Assert.Equal(
            FoodConsumptionStatus.Traveling,
            competitorService.Advance(competitor, GetHumanSpecies(), simulationTick: 0).Status);
    }

    [Fact]
    public void MissingNavigationAdapterCancelsActionAndReleasesReservation()
    {
        var world = new World();
        var entity = CreateHungryEntity(world);
        var source = AddSource(world, "food", Vector3.UnitX, amount: 1);
        var service = new FoodConsumptionService(
            world,
            _ => throw new InvalidOperationException("No navigation adapter is available."));

        Assert.Throws<InvalidOperationException>(() =>
            service.Advance(entity, GetHumanSpecies(), simulationTick: 0));

        Assert.Null(entity.Actions.Current);
        Assert.Equal(1, source.Contents.Counts[ItemIds.Food]);
        var competitor = CreateHungryEntity(world);
        var competitorService = new FoodConsumptionService(world, _ => new FakeNavigationAdapter());
        Assert.Equal(
            FoodConsumptionStatus.Traveling,
            competitorService.Advance(competitor, GetHumanSpecies(), simulationTick: 0).Status);
    }

    private static Scenario CreateScenario(int amount = 2)
    {
        var world = new World();
        var entity = CreateHungryEntity(world);
        var source = AddSource(world, "food", Vector3.UnitX, amount);
        var navigator = new FakeNavigationAdapter();
        var service = new FoodConsumptionService(world, _ => navigator);
        return new Scenario(world, entity, GetHumanSpecies(), source, navigator, service);
    }

    private static EntityRecord CreateHungryEntity(World world)
    {
        return world.SpawnEntity("human", Vector3.Zero, satiety: 20);
    }

    private static FoodSource AddSource(World world, string id, Vector3 position, int amount)
    {
        return AddSource(world, id, position, (ItemIds.Food, amount));
    }

    private static FoodSource AddSource(
        World world,
        string id,
        Vector3 position,
        params (string ItemId, int Amount)[] contentsByItem)
    {
        var capacity = Math.Max(
            contentsByItem.Where(entry => entry.Amount > 0).Sum(entry => entry.Amount),
            1);
        var contents = new ResourceContainer(id, capacity);
        foreach (var (itemId, amount) in contentsByItem)
        {
            if (amount <= 0)
            {
                continue;
            }

            Assert.Equal(ResourceMutationResult.Success, contents.TryAdd(itemId, amount));
        }

        return world.AddFoodSource(id, position, contents);
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

    private static SpeciesDefinition GetHumanSpecies()
    {
        return SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"))
            .GetRequired("human");
    }

    private static SpeciesDefinition LoadSpecies(string json, string speciesId)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return SpeciesCatalog.Load(stream).GetRequired(speciesId);
    }

    private sealed record Scenario(
        World World,
        EntityRecord Entity,
        SpeciesDefinition Species,
        FoodSource Source,
        FakeNavigationAdapter Navigator,
        FoodConsumptionService Service);

    private sealed class ThrowingNavigationAdapter : INavigationAdapter
    {
        public string Request(NavigationRequest request)
        {
            throw new InvalidOperationException("Navigation request failed.");
        }

        public NavigationOutcome Poll(string requestId, double elapsedSeconds) => NavigationOutcome.Removed;
        public bool Cancel(string requestId) => false;
        public bool RemoveTarget(string requestId) => false;
    }
}
