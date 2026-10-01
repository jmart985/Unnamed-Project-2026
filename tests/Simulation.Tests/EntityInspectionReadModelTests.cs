using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class EntityInspectionReadModelTests
{
    [Fact]
    public void SnapshotContainsIdleAndInventoryStateWithoutMutationAccess()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.One, 72);
        var snapshot = EntityInspectionReadModel.Create(world, entity);

        Assert.Equal(entity.Id, snapshot.Id);
        Assert.Equal("human", snapshot.SpeciesId);
        Assert.Equal(Vector3.One, snapshot.Position);
        Assert.Equal(72, snapshot.Satiety);
        Assert.Equal("Idle", snapshot.Activity);
        Assert.Null(snapshot.Target);
        Assert.Empty(snapshot.Inventory);
        Assert.Empty(snapshot.OutgoingRelationships);
    }

    [Fact]
    public void SnapshotReflectsHungryHaulingAndWaitingStates()
    {
        var world = new World();
        var hungry = world.SpawnEntity("human", Vector3.Zero, 10);
        hungry.Needs.Advance(0);
        var haul = world.SpawnEntity("human", Vector3.One);
        Assert.Equal(ActionStartResult.Started, haul.Actions.TryStart(new ActionCandidate("haul-1", ActionKind.Haul)));
        var waiting = world.SpawnEntity("human", Vector3.UnitX, 10);
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var selection = new FoodSelectionService(world, catalog);
        var result = selection.TrySelect(waiting, [], 0);

        Assert.Equal(FoodSelectionStatus.Waiting, result.Status);
        Assert.True(EntityInspectionReadModel.Create(world, hungry).Satiety < 30);
        Assert.Equal("haul-1", EntityInspectionReadModel.Create(world, haul).Target);
        var waitingSnapshot = EntityInspectionReadModel.Create(world, waiting);
        Assert.Equal("Waiting", waitingSnapshot.Activity);
        Assert.NotNull(waitingSnapshot.WaitingReason);
    }

    [Fact]
    public void SnapshotIncludesStableReadOnlyOutgoingRelationshipsAndBondLevels()
    {
        var world = new World();
        var inspected = world.SpawnEntity("human", Vector3.Zero);
        var stranger = world.SpawnEntity("human", Vector3.One);
        var trusted = world.SpawnEntity("human", Vector3.UnitX);
        var loyal = world.SpawnEntity("human", Vector3.UnitY);
        var fearful = world.SpawnEntity("creature", Vector3.UnitZ);

        Assert.True(world.TryGetOrCreateRelationship(inspected.Id, fearful.Id, out var fearRelationship));
        fearRelationship!.ApplyDelta(RelationshipDimension.Fear, 50);
        Assert.True(world.TryGetOrCreateRelationship(inspected.Id, loyal.Id, out var loyalRelationship));
        loyalRelationship!.ApplyDelta(RelationshipDimension.Familiarity, 20);
        loyalRelationship.ApplyDelta(RelationshipDimension.Trust, 75);
        loyalRelationship.ApplyDelta(RelationshipDimension.Loyalty, 60);
        Assert.True(world.TryGetOrCreateRelationship(inspected.Id, trusted.Id, out var trustedRelationship));
        trustedRelationship!.ApplyDelta(RelationshipDimension.Familiarity, 20);
        trustedRelationship.ApplyDelta(RelationshipDimension.Trust, 50);
        Assert.True(world.TryGetOrCreateRelationship(inspected.Id, stranger.Id, out _));

        var snapshot = EntityInspectionReadModel.Create(world, inspected);

        Assert.Equal(
            new[] { stranger.Id, trusted.Id, loyal.Id, fearful.Id },
            snapshot.OutgoingRelationships.Select(relationship => relationship.TargetEntityId));
        Assert.Equal(
            new[]
            {
                RelationshipBondLevel.Stranger,
                RelationshipBondLevel.Trusted,
                RelationshipBondLevel.Loyal,
                RelationshipBondLevel.Fearful
            },
            snapshot.OutgoingRelationships.Select(relationship => relationship.BondLevel));
        var formatted = EntityInspectionReadModel.FormatOutgoingRelationships(snapshot);
        Assert.Contains($"{stranger.Id}: Familiarity 0.0, Trust 0.0, Fear 0.0, Loyalty 0.0 [Stranger]", formatted);
        Assert.Contains($"{trusted.Id}: Familiarity 20.0, Trust 50.0, Fear 0.0, Loyalty 0.0 [Trusted]", formatted);
        Assert.Contains($"{loyal.Id}: Familiarity 20.0, Trust 75.0, Fear 0.0, Loyalty 60.0 [Loyal]", formatted);
        Assert.Contains($"{fearful.Id}: Familiarity 0.0, Trust 0.0, Fear 50.0, Loyalty 0.0 [Fearful]", formatted);
        Assert.Equal(75, loyalRelationship.Trust);
        Assert.Equal(60, loyalRelationship.Loyalty);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RelationshipInspectionSnapshot>)snapshot.OutgoingRelationships).Clear());
    }

    [Fact]
    public void SnapshotOmitsRelationshipsAfterTargetRemoval()
    {
        var world = new World();
        var inspected = world.SpawnEntity("human", Vector3.Zero);
        var target = world.SpawnEntity("human", Vector3.One);
        Assert.True(world.TryGetOrCreateRelationship(inspected.Id, target.Id, out _));
        Assert.True(world.TryRemoveEntity(target.Id));

        var snapshot = EntityInspectionReadModel.Create(world, inspected);
        Assert.Empty(snapshot.OutgoingRelationships);
        Assert.Equal("none", EntityInspectionReadModel.FormatOutgoingRelationships(snapshot));
    }
}
