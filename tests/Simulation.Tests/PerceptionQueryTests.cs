using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class PerceptionQueryTests
{
    [Fact]
    public void NearbyFoodUsesDietAndSmellRangeWithStableDistanceAndIdOrdering()
    {
        var world = new World();
        var wolf = world.SpawnEntity("wolf", Vector3.Zero);
        var farther = AddFood(world, "food-z", new Vector3(0, 0, 3), ItemIds.Meat);
        var nearer = AddFood(world, "food-b", new Vector3(1, 0, 0), ItemIds.Meat);
        AddFood(world, "food-a", new Vector3(-1, 0, 0), ItemIds.Meat);
        AddFood(world, "plant", new Vector3(0, 0, 1), ItemIds.Plant);
        AddFood(world, "out-of-range", new Vector3(0, 0, 30.01f), ItemIds.Meat);
        AddFood(world, "depleted", new Vector3(0, 0, 0.5f), ItemIds.Meat, 0);
        var service = CreateService(world);
        var availableMeatBefore = farther.Contents.Counts[ItemIds.Meat];

        var result = service.FindNearbyFood(wolf.Id);
        var repeatedResult = service.FindNearbyFood(wolf.Id);

        Assert.Equal(PerceptionQueryStatus.Success, result.Status);
        Assert.Equal(new[] { "food-a", "food-b", "food-z" }, result.Results.Select(source => source.Id));
        Assert.Equal(result.Results.Select(source => source.Id), repeatedResult.Results.Select(source => source.Id));
        Assert.Same(nearer, result.Results[1]);
        Assert.Same(farther, result.Results[2]);
        Assert.Equal(availableMeatBefore, farther.Contents.Counts[ItemIds.Meat]);
    }

    [Fact]
    public void NearbyEntitiesExcludeObserverAndSortByDistanceThenStableId()
    {
        var world = new World();
        var observer = world.SpawnEntity("wolf", Vector3.Zero);
        var farther = world.SpawnEntity("cow", new Vector3(0, 0, 3));
        var firstAtSameDistance = world.SpawnEntity("ape", new Vector3(1, 0, 0));
        var secondAtSameDistance = world.SpawnEntity("human", new Vector3(-1, 0, 0));
        world.SpawnEntity("cow", new Vector3(0, 0, 30.1f));

        var result = CreateService(world).FindNearbyEntities(observer.Id);

        Assert.Equal(PerceptionQueryStatus.Success, result.Status);
        Assert.Equal(
            new[] { firstAtSameDistance.Id, secondAtSameDistance.Id, farther.Id },
            result.Results.Select(entity => entity.Id));
    }

    [Fact]
    public void NearbyTracksRequireCapabilityAndReturnOnlyOtherTracksWithinRange()
    {
        var world = new World();
        var wolf = world.SpawnEntity("wolf", Vector3.Zero);
        var unsupported = world.SpawnEntity("cow", Vector3.Zero);
        var tracks = new[]
        {
            new CreatureTrack("target-z", new Vector3(0, 0, 2), 2),
            new CreatureTrack("target-b", new Vector3(1, 0, 0), 1),
            new CreatureTrack(wolf.Id, Vector3.Zero, 0),
            new CreatureTrack("target-far", new Vector3(0, 0, 30.01f), 3),
            new CreatureTrack("target-a", new Vector3(-1, 0, 0), 1)
        };
        var service = CreateService(world);

        var supportedResult = service.FindNearbyTracks(wolf.Id, tracks);
        var reversedInputResult = service.FindNearbyTracks(wolf.Id, tracks.Reverse());
        var unsupportedResult = service.FindNearbyTracks(unsupported.Id, tracks);

        Assert.Equal(PerceptionQueryStatus.Success, supportedResult.Status);
        Assert.Equal(
            new[] { "target-a", "target-b", "target-z" },
            supportedResult.Results.Select(track => track.TargetEntityId));
        Assert.Equal(
            supportedResult.Results.Select(track => (track.TargetEntityId, track.RecordedAtTick, track.Position)),
            reversedInputResult.Results.Select(track => (track.TargetEntityId, track.RecordedAtTick, track.Position)));
        Assert.Equal(PerceptionQueryStatus.TrackingUnsupported, unsupportedResult.Status);
        Assert.Empty(unsupportedResult.Results);
    }

    [Fact]
    public void InvalidObserverAndSpeciesAreReportedWithoutResults()
    {
        var world = new World();
        world.SpawnEntity("missing-species", Vector3.Zero);
        var service = CreateService(world);

        var missing = service.FindNearbyFood("missing");
        var invalidSpecies = service.FindNearbyEntities("entity-000001");

        Assert.Equal(PerceptionQueryStatus.InvalidObserver, missing.Status);
        Assert.Empty(missing.Results);
        Assert.Equal(PerceptionQueryStatus.InvalidSpecies, invalidSpecies.Status);
        Assert.Empty(invalidSpecies.Results);
    }

    [Fact]
    public void TrackInputsRejectInvalidValues()
    {
        Assert.Throws<ArgumentException>(() => new CreatureTrack(" ", Vector3.Zero, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CreatureTrack("target", new Vector3(float.NaN, 0, 0), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CreatureTrack("target", Vector3.Zero, -1));
    }

    private static FoodSource AddFood(
        World world,
        string id,
        Vector3 position,
        string itemId,
        int quantity = 1)
    {
        var contents = new ResourceContainer(id, 10);
        if (quantity > 0)
        {
            Assert.Equal(ResourceMutationResult.Success, contents.TryAdd(itemId, quantity));
        }

        return world.AddFoodSource(id, position, contents);
    }

    private static PerceptionQueryService CreateService(World world)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "species.json");
        return new PerceptionQueryService(world, SpeciesCatalog.LoadFromFile(path));
    }
}
