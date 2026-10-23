using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class NearFarTransitionServiceTests
{
    [Fact]
    public void PromotesAndDemotesARegionWithoutDuplicatingCounts()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var transitions = new NearFarTransitionService(world, catalog);
        transitions.SetFarRecord(new("grove", "creature", 2, 40, []));

        var near = transitions.Promote("grove", Vector3.Zero);
        Assert.Equal(2, near.Count);
        Assert.All(near, entity =>
        {
            Assert.Equal(80, entity.BaseStats.MaxHealth);
            Assert.Equal(2, entity.BaseStats.MovementSpeed);
            Assert.Equal(1.2, entity.BaseStats.Endurance);
        });
        Assert.Empty(transitions.FarRecords);
        Assert.True(transitions.Demote("grove"));
        Assert.Empty(world.Entities);
        Assert.Equal(2, transitions.FarRecords.Single(record => record.RegionId == "grove").Count);
    }

    [Fact]
    public void EmptyOrInvalidRegionsAreNoOps()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var transitions = new NearFarTransitionService(world, catalog);
        Assert.Empty(transitions.Promote("missing", Vector3.Zero));
        Assert.False(transitions.Demote("missing"));
    }

    [Fact]
    public void MixedSpeciesInSameRegionPromoteAndDemoteIndependently()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var transitions = new NearFarTransitionService(world, catalog);
        transitions.SetFarRecord(new("grove", "cow", 2, 40, [new(ItemIds.Plant, 7)]));
        transitions.SetFarRecord(new("grove", "wolf", 1, 35, [new(ItemIds.Meat, 3)]));

        var nearbyCows = transitions.Promote("grove", "cow", Vector3.Zero);

        Assert.Equal(2, nearbyCows.Count);
        Assert.Collection(transitions.FarRecords, record => Assert.Equal("wolf", record.SpeciesId));
        Assert.True(transitions.Demote("grove", "cow"));
        Assert.Empty(world.Entities);
        Assert.Equal(2, world.FarPopulations.Single(record => record.SpeciesId == "cow").Count);
        Assert.Equal(7, world.FarPopulations.Single(record => record.SpeciesId == "cow").Resources[ItemIds.Plant]);
        Assert.Equal(1, world.FarPopulations.Single(record => record.SpeciesId == "wolf").Count);

        var nearbyWolves = transitions.Promote("grove", "wolf", Vector3.One);
        Assert.Single(nearbyWolves);
        Assert.True(transitions.Demote("grove", "wolf"));
        Assert.Equal(2, transitions.FarRecords.Count);
    }

    [Fact]
    public void RegionPromotionWithoutSpeciesIsOnlyAllowedWhenUnambiguous()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var transitions = new NearFarTransitionService(world, catalog);
        transitions.SetFarRecord(new("grove", "cow", 1, 40, []));
        transitions.SetFarRecord(new("grove", "wolf", 1, 35, []));

        Assert.Empty(transitions.Promote("grove", Vector3.Zero));
        Assert.Equal(2, transitions.FarRecords.Count);
    }

    [Fact]
    public void UnknownSpeciesCannotReplaceAnExistingFarPopulation()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var transitions = new NearFarTransitionService(world, catalog);
        transitions.SetFarRecord(new("grove", "cow", 2, 40, []));

        Assert.Throws<ArgumentException>(() => transitions.SetFarRecord(new("grove", "unknown", 9, 10, [])));

        var preserved = Assert.Single(world.FarPopulations);
        Assert.Equal("cow", preserved.SpeciesId);
        Assert.Equal(2, preserved.Count);
    }

    [Fact]
    public void MixedFarPopulationsSurviveSnapshotRoundTripAfterSpeciesSpecificTransition()
    {
        var source = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var transitions = new NearFarTransitionService(source, catalog);
        transitions.SetFarRecord(new("grove", "cow", 2, 40, [new(ItemIds.Plant, 7)]));
        transitions.SetFarRecord(new("grove", "wolf", 1, 35, [new(ItemIds.Meat, 3)]));
        transitions.Promote("grove", "cow", Vector3.Zero);
        Assert.True(transitions.Demote("grove", "cow"));
        var json = WorldSnapshotSerializer.Serialize(source, 5);

        var restored = new World();
        WorldSnapshotSerializer.Load(restored, json, catalog);

        Assert.Empty(restored.Entities);
        Assert.Equal(2, restored.FarPopulations.Count);
        Assert.Equal(2, restored.FarPopulations.Single(record => record.SpeciesId == "cow").Count);
        Assert.Equal(7, restored.FarPopulations.Single(record => record.SpeciesId == "cow").Resources[ItemIds.Plant]);
        Assert.Equal(1, restored.FarPopulations.Single(record => record.SpeciesId == "wolf").Count);
        Assert.Equal(3, restored.FarPopulations.Single(record => record.SpeciesId == "wolf").Resources[ItemIds.Meat]);
    }

    [Fact]
    public void SnapshotRejectsDuplicateOrUnknownFarPopulationAndLeavesWorldUnchanged()
    {
        var source = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var transitions = new NearFarTransitionService(source, catalog);
        transitions.SetFarRecord(new("grove", "cow", 2, 40, []));
        var json = WorldSnapshotSerializer.Serialize(source, 0);
        var target = new World { SpeciesCatalog = catalog };
        target.SetFarPopulation(new FarEntityRecord("keep", "wolf", 1, 55, []));

        var duplicate = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        duplicate["FarPopulations"]!.AsArray().Add(duplicate["FarPopulations"]![0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(target, duplicate.ToJsonString(), catalog));

        var unknown = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        unknown["FarPopulations"]![0]!["SpeciesId"] = "unknown";
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(target, unknown.ToJsonString(), catalog));
        Assert.Single(target.FarPopulations);
        Assert.Equal("keep", target.FarPopulations.Single().RegionId);
    }

    [Fact]
    public void Schema18SnapshotMigratesWithoutInventingFarPopulations()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var snapshot = System.Text.Json.Nodes.JsonNode.Parse(WorldSnapshotSerializer.Serialize(world, 0))!.AsObject();
        snapshot["SchemaVersion"] = 18;
        snapshot.Remove("FarPopulations");

        var restored = new World();
        WorldSnapshotSerializer.Load(restored, snapshot.ToJsonString(), catalog);

        Assert.Empty(restored.FarPopulations);
    }
}
