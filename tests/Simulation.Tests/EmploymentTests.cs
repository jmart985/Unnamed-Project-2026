using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class EmploymentTests
{
    private static SpeciesCatalog Catalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    private static World CreateWorld()
    {
        var world = new World { SpeciesCatalog = Catalog() };
        world.AddStockpile("workplace", new(4, 0, 0), new ResourceContainer("storage", 100));
        return world;
    }
    private static EntityRecord Resident(World world, string species = "human")
    {
        var entity = world.SpawnEntity(world.SpeciesCatalog!.GetRequired(species), Vector3.Zero);
        Assert.Equal(EmploymentResult.Accepted, world.RegisterSettlementWorker(entity.Id));
        return entity;
    }

    [Fact]
    public void FiniteVacanciesAreClaimedDeterministicallyWithoutReplacingAnExistingJob()
    {
        var world = CreateWorld();
        var first = Resident(world);
        var second = Resident(world);
        var third = Resident(world);
        world.AddWorkplacePosition("second-position", "workplace");
        world.AddWorkplacePosition("first-position", "workplace");
        Assert.Equal(EmploymentResult.Accepted, world.AssignWorkerToPosition(second.Id, "first-position"));
        Assert.Equal(1, world.AutoAssignWorkplacePositions());
        Assert.Equal("second-position", world.GetEmployment(first.Id)!.Id);
        Assert.Equal("first-position", world.GetEmployment(second.Id)!.Id);
        Assert.Null(world.GetEmployment(third.Id));
        Assert.Equal(0, world.AutoAssignWorkplacePositions());
        world.AddWorkplacePosition("new-opening", "workplace");
        Assert.Equal(1, world.AutoAssignWorkplacePositions());
        Assert.Equal("new-opening", world.GetEmployment(third.Id)!.Id);
        world.AddWorkplacePosition("another-opening", "workplace");
        Assert.Equal(EmploymentResult.AlreadyEmployed, world.AssignWorkerToPosition(first.Id, "another-opening"));
    }

    [Fact]
    public void CapabilityAndExplicitResidencyPreventRecruitingVisitorsOrIncapableSpecies()
    {
        var world = CreateWorld();
        var incapable = Resident(world, "creature");
        var visitor = world.SpawnEntity(world.SpeciesCatalog!.GetRequired("human"), Vector3.Zero);
        var ape = Resident(world, "ape");
        world.AddWorkplacePosition("job", "workplace");
        Assert.Equal(EmploymentResult.Incapable, world.AssignWorkerToPosition(incapable.Id, "job"));
        Assert.Equal(EmploymentResult.NotResident, world.AssignWorkerToPosition(visitor.Id, "job"));
        Assert.Equal(1, world.AutoAssignWorkplacePositions());
        Assert.Equal(ape.Id, world.WorkplacePositions.Single().WorkerId);
    }

    [Fact]
    public void RemovingWorkerOrWorkplaceReopensOrRemovesPositionsAndNeverDeletesCargo()
    {
        var world = CreateWorld();
        var worker = Resident(world);
        var replacement = Resident(world);
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        world.TryRemoveEntity(worker.Id);
        Assert.Null(world.WorkplacePositions.Single().WorkerId);
        world.AutoAssignWorkplacePositions();
        Assert.Equal(replacement.Id, world.WorkplacePositions.Single().WorkerId);
        replacement.TryAddCarried(ItemIds.Wood, 3);
        world.TryRemoveStockpile("workplace");
        Assert.Empty(world.WorkplacePositions);
        Assert.Null(world.GetEmployment(replacement.Id));
        Assert.Equal(3, replacement.Inventory[ItemIds.Wood]);
    }

    [Fact]
    public void ManualReleasePausesAutoAssignmentButWorkCompletionDoesNotEndEmployment()
    {
        var world = CreateWorld();
        var worker = Resident(world);
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        var hauling = new WoodHaulingService(world, _ => new FakeNavigationAdapter());
        Assert.Equal(WoodHaulingStatus.Waiting, hauling.Advance(worker, world.SpeciesCatalog!.GetRequired("human"), 0).Status);
        Assert.NotNull(world.GetEmployment(worker.Id));
        Assert.Equal(EmploymentResult.Accepted, world.UnassignWorker(worker.Id));
        Assert.Equal(0, world.AutoAssignWorkplacePositions());
        Assert.False(world.SettlementWorkers.Single().AutoAssign);
        world.SetWorkerAutoAssignment(worker.Id, true);
        Assert.Equal(1, world.AutoAssignWorkplacePositions());
    }

    [Theory]
    [InlineData(ActionKind.Eat)]
    [InlineData(ActionKind.Flee)]
    [InlineData(ActionKind.CommandMove)]
    public void TemporaryActionsAndRestPreserveTheOngoingJob(ActionKind interruption)
    {
        var world = CreateWorld();
        var worker = Resident(world);
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        var job = world.GetEmployment(worker.Id);
        worker.Actions.TryStart(new("interruption", interruption));
        world.AutoAssignWorkplacePositions();
        Assert.Equal(job, world.GetEmployment(worker.Id));
        worker.Actions.Complete();
        var hauling = new WoodHaulingService(world, _ => new FakeNavigationAdapter());
        hauling.ReconcileSchedule(worker, 100);
        Assert.Equal(job, world.GetEmployment(worker.Id));
    }

    [Fact]
    public void AssignedStockpileWinsOverANearerOneAndDeliveryKeepsTheJob()
    {
        var world = CreateWorld();
        var worker = Resident(world);
        var wood = new ResourceContainer("wood", 10);
        wood.TryAdd(ItemIds.Wood, 2);
        var source = world.AddResourcePile("source", Vector3.UnitX, wood);
        world.AddStockpile("nearer", Vector3.UnitX, new ResourceContainer("nearer-storage", 100));
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        var navigator = new FakeNavigationAdapter();
        var hauling = new WoodHaulingService(world, _ => navigator);
        var species = world.SpeciesCatalog!.GetRequired("human");
        Assert.Equal("workplace", hauling.Advance(worker, species, 0).Stockpile!.Id);
        navigator.CompleteAsArrived(navigator.Requests.Keys.Single());
        world.TrySetEntityPosition(worker.Id, source.Position);
        Assert.Equal("workplace", hauling.Advance(worker, species, 1).Stockpile!.Id);
        navigator.CompleteAsArrived(navigator.Requests.Keys.Last());
        world.TrySetEntityPosition(worker.Id, world.Stockpiles[0].Position);
        Assert.Equal(WoodHaulingStatus.Delivered, hauling.Advance(worker, species, 2).Status);
        Assert.NotNull(world.GetEmployment(worker.Id));
        Assert.Equal(2, world.Stockpiles[0].Contents.Counts[ItemIds.Wood]);
        Assert.Empty(world.Stockpiles[1].Contents.Counts);
    }

    [Fact]
    public void AssigningAnActiveLegacyHaulerRebuildsItsTaskWithoutLosingCargo()
    {
        var world = CreateWorld();
        var worker = world.SpawnEntity(world.SpeciesCatalog!.GetRequired("human"), Vector3.Zero);
        worker.TryAddCarried(ItemIds.Wood, 2);
        var navigator = new FakeNavigationAdapter();
        var hauling = new WoodHaulingService(world, _ => navigator);
        hauling.Advance(worker, world.SpeciesCatalog.GetRequired("human"), 0);
        var request = navigator.Requests.Keys.Single();
        world.RegisterSettlementWorker(worker.Id);
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        Assert.Equal(NavigationOutcome.Cancelled, navigator.Requests[request]);
        Assert.Equal(2, worker.Inventory[ItemIds.Wood]);
        Assert.NotNull(world.GetEmployment(worker.Id));
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile,
            hauling.Advance(worker, world.SpeciesCatalog.GetRequired("human"), 1).Status);
    }

    [Fact]
    public void HungerPreemptsWorkWithoutMakingAnEmployeeUnemployed()
    {
        var world = CreateWorld();
        var worker = world.SpawnEntity(world.SpeciesCatalog!.GetRequired("human"), Vector3.Zero, satiety: 20);
        world.RegisterSettlementWorker(worker.Id);
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        var hauling = new WoodHaulingService(world, _ => new FakeNavigationAdapter());
        Assert.Equal(WoodHaulingStatus.NeedsFood,
            hauling.Advance(worker, world.SpeciesCatalog.GetRequired("human"), 0).Status);
        Assert.NotNull(world.GetEmployment(worker.Id));
        worker.Needs.AddNutrition(100);
        Assert.Equal(WoodHaulingStatus.Waiting,
            hauling.Advance(worker, world.SpeciesCatalog.GetRequired("human"), 1).Status);
        Assert.NotNull(world.GetEmployment(worker.Id));
    }

    [Fact]
    public void WorkplaceOwnershipChangesReleaseIneligibleWorkersImmediately()
    {
        var world = CreateWorld();
        var worker = Resident(world);
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        world.DefineFaction("other");
        world.SetStockpileFactionOwner("workplace", "other");
        Assert.Null(world.GetEmployment(worker.Id));
        Assert.Equal(0, world.AutoAssignWorkplacePositions());
        world.SetEntityAllegiance(worker.Id, "other");
        Assert.Equal(1, world.AutoAssignWorkplacePositions());
        world.SetEntityAllegiance(worker.Id, null);
        Assert.Null(world.GetEmployment(worker.Id));
    }

    [Fact]
    public void IncapacitationKeepsAJobButDeathReleasesItForAnotherResident()
    {
        var world = CreateWorld();
        var worker = Resident(world);
        var replacement = Resident(world);
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        worker.Health.TryDamage("Head", 1000);
        Assert.True(worker.Health.IsIncapacitated);
        Assert.Equal(0, world.AutoAssignWorkplacePositions());
        Assert.Equal(worker.Id, world.WorkplacePositions.Single().WorkerId);
        var hauling = new WoodHaulingService(world, _ => new FakeNavigationAdapter());
        Assert.Equal(WoodHaulingStatus.Incapacitated,
            hauling.Advance(worker, world.SpeciesCatalog!.GetRequired("human"), 0).Status);
        Assert.Null(worker.Actions.Current);
        worker.Health.TryDamage("Torso", 1000);
        Assert.Equal(1, world.AutoAssignWorkplacePositions());
        Assert.Equal(replacement.Id, world.WorkplacePositions.Single().WorkerId);
    }

    [Fact]
    public void UnreachableWorkRetainsEmploymentAndReleasesReservationsBeforeRetry()
    {
        var world = CreateWorld();
        var worker = Resident(world);
        var wood = new ResourceContainer("wood", 10);
        wood.TryAdd(ItemIds.Wood, 2);
        var source = world.AddResourcePile("source", Vector3.UnitX, wood);
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        var navigator = new FakeNavigationAdapter();
        var hauling = new WoodHaulingService(world, _ => navigator);
        var species = world.SpeciesCatalog!.GetRequired("human");
        hauling.Advance(worker, species, 0);
        navigator.MarkUnreachable(navigator.Requests.Keys.Single());
        hauling.Advance(worker, species, 1);
        Assert.NotNull(world.GetEmployment(worker.Id));
        Assert.Equal(2, world.Reservations.GetAvailableResourceQuantity(source.Contents, ItemIds.Wood));
        Assert.Equal(WoodHaulingStatus.RetryDeferred, hauling.Advance(worker, species, 2).Status);
        Assert.Equal(0, world.AutoAssignWorkplacePositions());
    }

    [Fact]
    public void PlayerIsNeverRecruitedAndUnemployedResidentsDoNotBeginPickupTrips()
    {
        var session = NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Player"));
        Assert.Equal(EmploymentResult.InvalidWorker, session.World.RegisterSettlementWorker(session.PlayerEntityId));
        var npc = session.World.Entities.First(entity => entity.Id != session.PlayerEntityId && entity.SpeciesId == "human");
        session.World.UnassignWorker(npc.Id);
        var hauling = new WoodHaulingService(session.World, _ => new FakeNavigationAdapter());
        Assert.Equal(WoodHaulingStatus.NotEmployed, hauling.Advance(npc, Catalog().GetRequired("human"), 0).Status);
        Assert.Null(npc.Actions.Current);
    }

    [Fact]
    public void SaveLoadPreservesEmploymentAndManualAutoAssignmentPreference()
    {
        var world = CreateWorld();
        var worker = Resident(world);
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        world.SetWorkerAutoAssignment(worker.Id, false);
        var loaded = CreateWorld();
        WorldSnapshotSerializer.Load(loaded, WorldSnapshotSerializer.Serialize(world, 12), Catalog());
        Assert.Equal(world.GetEmployment(worker.Id), loaded.GetEmployment(worker.Id));
        Assert.False(loaded.SettlementWorkers.Single().AutoAssign);
        Assert.Null(loaded.Entities.Single().Actions.Current);
    }

    [Fact]
    public void Schema26UsesKnownResidentsWithoutRecruitingAllOtherEntities()
    {
        var session = NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Legacy"));
        var node = JsonNode.Parse(WorldSnapshotSerializer.Serialize(session.World, 0))!.AsObject();
        node["SchemaVersion"] = 26;
        node.Remove("UsesWorkplaceEmployment");
        node.Remove("SettlementWorkers");
        node.Remove("WorkplacePositions");
        var loaded = CreateWorld();
        WorldSnapshotSerializer.Load(loaded, node.ToJsonString(), Catalog());
        Assert.Equal(NewGameSession.HumanNpcCount, loaded.SettlementWorkers.Count);
        Assert.Equal(NewGameSession.HumanNpcCount, loaded.AutoAssignWorkplacePositions());
        Assert.DoesNotContain(loaded.WorkplacePositions, position => position.WorkerId == loaded.PlayerEntityId);
    }

    [Theory]
    [InlineData("duplicate-worker")]
    [InlineData("duplicate-position")]
    [InlineData("unknown-worker")]
    [InlineData("unknown-workplace")]
    [InlineData("unknown-role")]
    [InlineData("null-workers")]
    [InlineData("disabled-with-data")]
    public void InvalidEmploymentLoadPreservesWorldAndCurrentAction(string fault)
    {
        var world = CreateWorld();
        var worker = Resident(world);
        world.AddWorkplacePosition("job", "workplace");
        world.AutoAssignWorkplacePositions();
        worker.Actions.TryStart(new("attention", ActionKind.Eat));
        var original = WorldSnapshotSerializer.Serialize(world, 0);
        var node = JsonNode.Parse(original)!.AsObject();
        var position = node["WorkplacePositions"]![0]!.AsObject();
        switch (fault)
        {
            case "duplicate-worker":
                var second = position.DeepClone().AsObject(); second["Id"] = "second";
                node["WorkplacePositions"]!.AsArray().Add(second); break;
            case "duplicate-position": node["WorkplacePositions"]!.AsArray().Add(position.DeepClone()); break;
            case "unknown-worker": position["WorkerId"] = "missing"; break;
            case "unknown-workplace": position["WorkplaceId"] = "missing"; break;
            case "unknown-role": position["RoleId"] = "farmer-without-executor"; break;
            case "null-workers": node["SettlementWorkers"] = null; break;
            case "disabled-with-data": node["UsesWorkplaceEmployment"] = false; break;
        }
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(world, node.ToJsonString(), Catalog()));
        Assert.Equal(original, WorldSnapshotSerializer.Serialize(world, 0));
        Assert.Equal("attention", worker.Actions.Current!.Id);
    }
}
