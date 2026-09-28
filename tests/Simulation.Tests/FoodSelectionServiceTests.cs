using System.Numerics;
using System.Text;
using Simulation;

namespace Simulation.Tests;

public sealed class FoodSelectionServiceTests
{
    [Fact]
    public void SelectsNearestAvailableCompatibleFoodAndReservesOneUnit()
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var near = CreateFoodSource("near", new Vector3(1, 0, 0), amount: 2);
        var far = CreateFoodSource("far", new Vector3(5, 0, 0), amount: 1);
        var service = new FoodSelectionService(world);

        var result = service.TrySelect(entity, GetHumanSpecies(), [far, near], simulationTick: 0);

        Assert.Equal(FoodSelectionStatus.Selected, result.Status);
        Assert.Same(near, result.Source);
        Assert.Equal("SeekingFood", entity.Activity);
        Assert.Null(entity.WaitingReason);

        var competitor = SpawnHungryEntity(world);
        var thirdEntity = SpawnHungryEntity(world);
        Assert.Equal(ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("competitor-eat", ActionKind.Eat)));
        Assert.Equal(ActionStartResult.Started,
            thirdEntity.Actions.TryStart(new ActionCandidate("third-eat", ActionKind.Eat)));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(
                competitor.Id,
                "competitor-eat",
                near.Id,
                near.Contents,
                ItemIds.Food,
                1));
        Assert.Equal(
            ReservationResult.InsufficientQuantity,
            world.Reservations.TryReserveResource(
                thirdEntity.Id,
                "third-eat",
                near.Id,
                near.Contents,
                ItemIds.Food,
                1));
    }

    [Fact]
    public void SkipsEmptyAndAlreadyReservedSources()
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var otherEntity = SpawnHungryEntity(world);
        var empty = CreateFoodSource("empty", new Vector3(1, 0, 0), amount: 0);
        var reserved = CreateFoodSource("reserved", new Vector3(2, 0, 0), amount: 1);
        var available = CreateFoodSource("available", new Vector3(3, 0, 0), amount: 1);
        Assert.Equal(
            ActionStartResult.Started,
            otherEntity.Actions.TryStart(new ActionCandidate("other-eat", ActionKind.Eat)));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(
                otherEntity.Id,
                "other-eat",
                reserved.Id,
                reserved.Contents,
                ItemIds.Food,
                1));
        var service = new FoodSelectionService(world);

        var result = service.TrySelect(
            entity,
            GetHumanSpecies(),
            [empty, reserved, available],
            simulationTick: 0);

        Assert.Equal(FoodSelectionStatus.Selected, result.Status);
        Assert.Same(available, result.Source);
    }

    [Fact]
    public void UnreachableCandidateReleasesItsReservationAndSelectsAnother()
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var unreachable = CreateFoodSource("blocked", Vector3.UnitX, amount: 1);
        var alternate = CreateFoodSource("alternate", new Vector3(2, 0, 0), amount: 1);
        var service = new FoodSelectionService(world);
        var selection = service.TrySelect(
            entity,
            GetHumanSpecies(),
            [unreachable, alternate],
            simulationTick: 0);

        var replacement = service.ReportCandidateFailure(
            entity,
            unreachable.Id,
            FoodTargetFailure.Unreachable,
            [unreachable, alternate],
            simulationTick: 1);

        Assert.Equal(FoodSelectionStatus.Selected, selection.Status);
        Assert.Equal(FoodSelectionStatus.Selected, replacement.Status);
        Assert.Same(alternate, replacement.Source);
        Assert.Equal(selection.ActionId, replacement.ActionId);

        var competitor = SpawnHungryEntity(world);
        Assert.Equal(
            ActionStartResult.Started,
            competitor.Actions.TryStart(new ActionCandidate("competitor-eat", ActionKind.Eat)));
        Assert.Equal(
            ReservationResult.Reserved,
            world.Reservations.TryReserveResource(
                competitor.Id,
                "competitor-eat",
                unreachable.Id,
                unreachable.Contents,
                ItemIds.Food,
                1));
    }

    [Fact]
    public void DepletedCandidateIsSkippedAndItsReservationIsReleased()
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var depleted = CreateFoodSource("depleted", Vector3.UnitX, amount: 1);
        var alternate = CreateFoodSource("alternate", new Vector3(2, 0, 0), amount: 1);
        var service = new FoodSelectionService(world);
        service.TrySelect(entity, GetHumanSpecies(), [depleted, alternate], simulationTick: 0);
        Assert.Equal(
            ResourceMutationResult.Success,
            depleted.Contents.TryRemove(ItemIds.Food, 1));

        var replacement = service.ReportCandidateFailure(
            entity,
            depleted.Id,
            FoodTargetFailure.Depleted,
            [depleted, alternate],
            simulationTick: 1);

        Assert.Equal(FoodSelectionStatus.Selected, replacement.Status);
        Assert.Same(alternate, replacement.Source);
        Assert.Empty(depleted.Contents.Counts);
    }

    [Fact]
    public void RemovedCandidateIsInvalidatedAndAnAlternateIsSelected()
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var removed = CreateFoodSource("removed", Vector3.UnitX, amount: 1);
        var alternate = CreateFoodSource("alternate", new Vector3(2, 0, 0), amount: 1);
        var service = new FoodSelectionService(world);
        service.TrySelect(entity, GetHumanSpecies(), [removed, alternate], simulationTick: 0);

        var replacement = service.ReportCandidateFailure(
            entity,
            removed.Id,
            FoodTargetFailure.Removed,
            [removed, alternate],
            simulationTick: 1);

        Assert.Equal(FoodSelectionStatus.Selected, replacement.Status);
        Assert.Same(alternate, replacement.Source);

        var competitor = SpawnHungryEntity(world);
        Assert.Equal(
            ActionStartResult.Started,
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
    public void NoCompatibleFoodShowsReasonAndRetriesAfterBoundedDelay()
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var service = new FoodSelectionService(world);
        var woodOnlySpecies = LoadSpecies(
            """[{"id":"human","movementSpeed":1,"hungerRate":0.2,"diet":["wood"],"canHaul":true}]""",
            "human");
        var food = CreateFoodSource("food", Vector3.UnitX, amount: 1);

        var waiting = service.TrySelect(entity, woodOnlySpecies, [food], simulationTick: 0);

        Assert.Equal(FoodSelectionStatus.Waiting, waiting.Status);
        Assert.Equal("Waiting", entity.Activity);
        Assert.Equal("No compatible food available.", entity.WaitingReason);

        var deferred = service.TrySelect(entity, GetHumanSpecies(), [food], simulationTick: 9);
        Assert.Equal(FoodSelectionStatus.RetryDeferred, deferred.Status);

        var recovered = service.TrySelect(entity, GetHumanSpecies(), [food], simulationTick: 10);
        Assert.Equal(FoodSelectionStatus.Selected, recovered.Status);
        Assert.Same(food, recovered.Source);
        Assert.Null(entity.WaitingReason);
    }

    [Fact]
    public void IncompatibleDietWithNoSourcesReportsIncompatibleReasonNotEmptyReason()
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var service = new FoodSelectionService(world);
        var woodOnlySpecies = LoadSpecies(
            """[{"id":"human","movementSpeed":1,"hungerRate":0.2,"diet":["wood"],"canHaul":true}]""",
            "human");

        var waiting = service.TrySelect(entity, woodOnlySpecies, [], simulationTick: 0);

        Assert.Equal(FoodSelectionStatus.Waiting, waiting.Status);
        Assert.Equal("Waiting", entity.Activity);
        Assert.Equal("No compatible food available.", entity.WaitingReason);
    }

    [Fact]
    public void EmptyFoodSourceRecoversAfterReplenishmentAndRetryDelay()
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var food = CreateFoodSource("replenishable", Vector3.UnitX, amount: 0);
        var service = new FoodSelectionService(world);

        var waiting = service.TrySelect(entity, GetHumanSpecies(), [food], simulationTick: 0);

        Assert.Equal(FoodSelectionStatus.Waiting, waiting.Status);
        Assert.Equal("No available food.", entity.WaitingReason);
        Assert.Equal(
            ResourceMutationResult.Success,
            food.Contents.TryAdd(ItemIds.Food, 1));
        Assert.Equal(
            FoodSelectionStatus.RetryDeferred,
            service.TrySelect(entity, GetHumanSpecies(), [food], simulationTick: 9).Status);

        var recovered = service.TrySelect(entity, GetHumanSpecies(), [food], simulationTick: 10);

        Assert.Equal(FoodSelectionStatus.Selected, recovered.Status);
        Assert.Same(food, recovered.Source);
    }

    [Fact]
    public void CompetingEntitiesCannotReserveTheFinalFoodUnit()
    {
        var world = new World();
        var first = SpawnHungryEntity(world);
        var second = SpawnHungryEntity(world);
        var food = CreateFoodSource("last-food", Vector3.UnitX, amount: 1);
        var service = new FoodSelectionService(world);

        var firstResult = service.TrySelect(first, GetHumanSpecies(), [food], simulationTick: 0);
        var secondResult = service.TrySelect(second, GetHumanSpecies(), [food], simulationTick: 0);

        Assert.Equal(FoodSelectionStatus.Selected, firstResult.Status);
        Assert.Equal(FoodSelectionStatus.Waiting, secondResult.Status);
        Assert.Equal("No available food.", second.WaitingReason);
        Assert.Equal(1, food.Contents.Counts[ItemIds.Food]);
    }

    [Fact]
    public void RejectsFailureReportsForStaleOrUnselectedTargets()
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var food = CreateFoodSource("food", Vector3.UnitX, amount: 1);
        var service = new FoodSelectionService(world);

        var result = service.ReportCandidateFailure(
            entity,
            food.Id,
            FoodTargetFailure.Unreachable,
            [food],
            simulationTick: 0);

        Assert.Equal(FoodSelectionStatus.InvalidSelection, result.Status);
    }

    [Fact]
    public void RejectsSpeciesThatDoesNotMatchTheEntity()
    {
        var world = new World();
        var entity = SpawnHungryEntity(world);
        var species = LoadSpecies(
            """[{"id":"wood-eater","movementSpeed":1,"hungerRate":0.2,"diet":["wood"],"canHaul":false}]""",
            "wood-eater");
        var service = new FoodSelectionService(world);

        var result = service.TrySelect(entity, species, [], simulationTick: 0);

        Assert.Equal(FoodSelectionStatus.InvalidSpecies, result.Status);
        Assert.Null(entity.Actions.Current);
    }

    private static EntityRecord SpawnHungryEntity(World world)
    {
        var entity = world.SpawnEntity("human", Vector3.Zero);
        entity.Needs.Advance(150);
        Assert.True(entity.Needs.IsHungry);
        return entity;
    }

    private static FoodSource CreateFoodSource(string id, Vector3 position, int amount)
    {
        var contents = new ResourceContainer(id, 10);
        if (amount > 0)
        {
            Assert.Equal(ResourceMutationResult.Success, contents.TryAdd(ItemIds.Food, amount));
        }

        return new FoodSource(id, position, contents);
    }

    private static SpeciesDefinition GetHumanSpecies()
    {
        return SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"))
            .GetRequired("human");
    }

    private static SpeciesDefinition LoadSpecies(string json, string speciesId)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return SpeciesCatalog.Load(stream).GetRequired(speciesId);
    }
}
