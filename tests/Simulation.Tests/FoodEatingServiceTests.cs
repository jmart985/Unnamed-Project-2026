using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class FoodEatingServiceTests
{
    [Fact]
    public void FoodIsConsumedExactlyOnceOnlyAfterArrivalWithinTolerance()
    {
        var (world, entity, food, service) = CreateHungryWorld(foodAmount: 2);
        var navigation = new FakeNavigationAdapter();

        var moving = service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 1);
        var requestId = Assert.Single(navigation.Requests).Key;

        Assert.Equal(FoodEatingStatus.Traveling, moving.Status);
        Assert.Equal(2, food.Contents.Counts[ItemIds.Food]);
        Assert.Equal(0, service.TotalFoodConsumed);
        Assert.Equal(0, entity.Satiety);

        Assert.True(navigation.CompleteAsArrived(requestId));
        Assert.True(world.TrySetEntityPosition(entity.Id, food.Position));
        var eaten = service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 2);

        Assert.Equal(FoodEatingStatus.Ate, eaten.Status);
        Assert.Equal(1, food.Contents.Counts[ItemIds.Food]);
        Assert.Equal(50, entity.Satiety);
        Assert.Equal(1, service.TotalFoodConsumed);
        Assert.Equal(
            2,
            food.Contents.Counts.GetValueOrDefault(ItemIds.Food) + service.TotalFoodConsumed);
        Assert.Null(entity.Actions.Current);
        Assert.Equal("Idle", entity.Activity);

        Assert.Equal(
            FoodEatingStatus.NotHungry,
            service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 3).Status);
        Assert.Equal(1, service.TotalFoodConsumed);
        Assert.Equal(50, entity.Satiety);
    }

    [Fact]
    public void ArrivalOutsideToleranceRejectsConsumptionAndReleasesReservation()
    {
        var (world, entity, food, service) = CreateHungryWorld();
        var navigation = new FakeNavigationAdapter();
        service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 1);
        var requestId = Assert.Single(navigation.Requests).Key;
        Assert.True(navigation.CompleteAsArrived(requestId));

        var result = service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 2);

        Assert.Equal(FoodEatingStatus.ArrivalRejected, result.Status);
        Assert.Equal(1, food.Contents.Counts[ItemIds.Food]);
        Assert.Equal(0, service.TotalFoodConsumed);
        Assert.Equal(0, entity.Satiety);
        Assert.Null(entity.Actions.Current);

        var competitor = SpawnHungryEntity(world);
        Assert.Equal(
            ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("competitor-eat", ActionKind.Eat)));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(
                competitor.Id,
                "competitor-eat",
                food.Id,
                food.Contents,
                ItemIds.Food,
                1));
    }

    [Fact]
    public void UnreachableFoodReleasesReservationAndImmediatelyRoutesToAlternate()
    {
        var (world, entity, blocked, service) = CreateHungryWorld();
        var alternate = AddFood(world, "alternate", new Vector3(2, 0, 0), amount: 1);
        var navigation = new FakeNavigationAdapter();
        service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 1);
        var firstRequest = navigation.Requests.Single().Key;
        Assert.True(navigation.MarkUnreachable(firstRequest));

        var result = service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 2);

        Assert.Equal(FoodEatingStatus.Traveling, result.Status);
        Assert.Same(alternate, result.Source);
        Assert.Equal(2, navigation.Requests.Count);
        Assert.Equal(NavigationOutcome.Unreachable, navigation.Requests[firstRequest]);

        var competitor = SpawnHungryEntity(world);
        Assert.Equal(
            ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("competitor-eat", ActionKind.Eat)));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(
                competitor.Id,
                "competitor-eat",
                blocked.Id,
                blocked.Contents,
                ItemIds.Food,
                1));
    }

    [Fact]
    public void RemovedFoodTargetReleasesReservationAndUsesAnotherSource()
    {
        var (world, entity, removed, service) = CreateHungryWorld();
        var alternate = AddFood(world, "alternate", new Vector3(2, 0, 0), amount: 1);
        var navigation = new FakeNavigationAdapter();
        service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 1);
        var requestId = navigation.Requests.Single().Key;
        Assert.True(world.TryRemoveFoodSource(removed.Id));
        Assert.True(navigation.RemoveTarget(requestId));

        var result = service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 2);

        Assert.Equal(FoodEatingStatus.Traveling, result.Status);
        Assert.Same(alternate, result.Source);
        Assert.Equal(2, navigation.Requests.Count);
        Assert.Equal(NavigationOutcome.Removed, navigation.Requests[requestId]);
        Assert.DoesNotContain(world.FoodSources, source => source.Id == removed.Id);

        var competitor = SpawnHungryEntity(world);
        Assert.Equal(ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("competitor-eat", ActionKind.Eat)));
        Assert.Equal(
            ReservationResult.StaleTarget,
            world.Reservations.TryReserveResource(
                competitor.Id,
                "competitor-eat",
                removed.Id,
                removed.Contents,
                ItemIds.Food,
                1));
    }

    [Fact]
    public void DepletedFoodAtArrivalDoesNotAddNutritionOrIncrementCounter()
    {
        var (world, entity, food, service) = CreateHungryWorld();
        var navigation = new FakeNavigationAdapter();
        service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 1);
        var requestId = navigation.Requests.Single().Key;
        Assert.Equal(ResourceMutationResult.Success, food.Contents.TryRemove(ItemIds.Food, 1));
        Assert.True(navigation.CompleteAsArrived(requestId));
        Assert.True(world.TrySetEntityPosition(entity.Id, food.Position));

        var result = service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 2);

        Assert.Equal(FoodEatingStatus.Waiting, result.Status);
        Assert.Equal(0, entity.Satiety);
        Assert.Equal(0, service.TotalFoodConsumed);
        Assert.Null(entity.Actions.Current);
    }

    [Fact]
    public void ExplicitCancellationStopsNavigationAndReleasesFoodReservation()
    {
        var (world, entity, food, service) = CreateHungryWorld();
        var navigation = new FakeNavigationAdapter();
        service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 1);
        var requestId = navigation.Requests.Single().Key;

        Assert.True(service.Cancel(entity));

        Assert.Equal(NavigationOutcome.Cancelled, navigation.Requests[requestId]);
        Assert.Null(entity.Actions.Current);
        Assert.Equal("Idle", entity.Activity);

        var competitor = SpawnHungryEntity(world);
        Assert.Equal(ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("competitor-eat", ActionKind.Eat)));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(
                competitor.Id,
                "competitor-eat",
                food.Id,
                food.Contents,
                ItemIds.Food,
                1));
    }

    [Fact]
    public void EntityActionCancellationCancelsItsNavigationOnNextAdvance()
    {
        var (_, entity, _, service) = CreateHungryWorld();
        var navigation = new FakeNavigationAdapter();
        service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 1);
        var requestId = navigation.Requests.Single().Key;

        Assert.True(entity.Actions.Cancel());

        var restarted = service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 2);

        Assert.Equal(FoodEatingStatus.Traveling, restarted.Status);
        Assert.Equal(NavigationOutcome.Cancelled, navigation.Requests[requestId]);
        Assert.Equal(2, navigation.Requests.Count);
    }

    [Fact]
    public void HungryEntityPreemptsLowerPriorityWorkBeforeSeekingFood()
    {
        var (world, entity, _, service) = CreateHungryWorld();
        Assert.Equal(ActionStartResult.Started,
            entity.Actions.TryStart(new ActionCandidate("haul-action", ActionKind.Haul)));
        var navigation = new FakeNavigationAdapter();

        var result = service.Advance(entity, GetHumanSpecies(), navigation, 0.1, simulationTick: 1);

        Assert.Equal(FoodEatingStatus.Traveling, result.Status);
        Assert.Equal(ActionKind.Eat, entity.Actions.Current?.Kind);
        Assert.Equal(NavigationOutcome.InProgress, navigation.Requests.Values.Single());
    }

    private static (World World, EntityRecord Entity, FoodSource Food, FoodEatingService Service)
        CreateHungryWorld(int foodAmount = 1)
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var food = AddFood(world, "food-source", Vector3.UnitX, foodAmount);
        var service = new FoodEatingService(world, new FoodSelectionService(world));
        return (world, entity, food, service);
    }

    private static EntityRecord SpawnHungryEntity(World world)
    {
        var entity = world.SpawnEntity("human", Vector3.Zero);
        entity.Needs.Advance(300);
        return entity;
    }

    private static FoodSource AddFood(World world, string id, Vector3 position, int amount)
    {
        var contents = new ResourceContainer(id, amount);
        Assert.Equal(ResourceMutationResult.Success, contents.TryAdd(ItemIds.Food, amount));
        var source = new FoodSource(id, position, contents);
        world.AddFoodSource(source);
        return source;
    }

    private static SpeciesDefinition GetHumanSpecies()
    {
        return SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"))
            .GetRequired("human");
    }
}
