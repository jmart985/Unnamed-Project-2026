using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class WildTerritoryTests
{
    [Fact]
    public void WorldValidatesTerritoryAssignmentsAndCleansThemUp()
    {
        var world = new World();
        var wolf = world.SpawnEntity("wolf", new Vector3(2, 0, 3));
        var center = new Vector3(1, 0, 2);

        Assert.Equal(WildTerritoryResult.Accepted, world.SetWildTerritory(wolf.Id, center, 5));
        Assert.True(world.TryGetWildTerritory(wolf.Id, out var territory));
        Assert.Equal(new WildTerritory(wolf.Id, center, 5), territory);
        Assert.True(territory!.Contains(new Vector3(6, 0, 2)));
        Assert.False(territory.Contains(new Vector3(6.01f, 0, 2)));
        Assert.Equal(WildTerritoryResult.Unchanged, world.SetWildTerritory(wolf.Id, center, 5));

        Assert.Equal(WildTerritoryResult.InvalidGeometry, world.SetWildTerritory(wolf.Id, new Vector3(float.NaN, 0, 0), 4));
        Assert.Equal(WildTerritoryResult.InvalidGeometry, world.SetWildTerritory(wolf.Id, center, 0));
        Assert.Equal(WildTerritoryResult.InvalidGeometry, world.SetWildTerritory(wolf.Id, center, float.PositiveInfinity));
        Assert.Equal(WildTerritoryResult.InvalidEntity, world.SetWildTerritory("missing", center, 4));
        Assert.Equal(new WildTerritory(wolf.Id, center, 5), world.WildTerritories.Single());

        Assert.True(world.TryRemoveEntity(wolf.Id));
        Assert.Empty(world.WildTerritories);

        var next = world.SpawnEntity("wolf", Vector3.Zero);
        world.SetWildTerritory(next.Id, center, 3);
        world.Reset();
        Assert.Empty(world.WildTerritories);
    }

    [Fact]
    public void TerritorySnapshotRoundTripsAndSchemaSixteenMigratesWithoutInventingOne()
    {
        var catalog = GetSpeciesCatalog();
        var source = new World();
        var wolf = source.SpawnEntity("wolf", Vector3.Zero);
        Assert.Equal(WildTerritoryResult.Accepted,
            source.SetWildTerritory(wolf.Id, new Vector3(4, 0, 7), 12));

        var currentJson = WorldSnapshotSerializer.Serialize(source, 2);
        var target = new World();
        WorldSnapshotSerializer.Load(target, currentJson, catalog);

        Assert.Equal(source.WildTerritories.Single(), target.WildTerritories.Single());
        Assert.Equal(currentJson, WorldSnapshotSerializer.Serialize(target, 2));

        var oldSnapshot = JsonNode.Parse(currentJson)!.AsObject();
        oldSnapshot["SchemaVersion"] = 16;
        oldSnapshot.Remove("WildTerritories");
        WorldSnapshotSerializer.Load(target, oldSnapshot.ToJsonString(), catalog);

        Assert.Empty(target.WildTerritories);
        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion,
            WorldSnapshotSerializer.Capture(target, 2).SchemaVersion);
    }

    [Fact]
    public void InvalidTerritorySnapshotIsRejectedAtomically()
    {
        var catalog = GetSpeciesCatalog();
        var source = new World();
        var wolf = source.SpawnEntity("wolf", Vector3.Zero);
        source.SetWildTerritory(wolf.Id, Vector3.One, 4);
        var invalidSnapshot = JsonNode.Parse(WorldSnapshotSerializer.Serialize(source, 0))!.AsObject();
        invalidSnapshot["WildTerritories"]![0]!["Radius"] = -1;

        var target = new World();
        var original = target.SpawnEntity("human", new Vector3(9, 0, 2));
        var before = WorldSnapshotSerializer.Serialize(target, 5);

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            target, invalidSnapshot.ToJsonString(), catalog));

        Assert.Same(original, Assert.Single(target.Entities));
        Assert.Equal(before, WorldSnapshotSerializer.Serialize(target, 5));
    }

    [Fact]
    public void SnapshotRejectsDuplicateTerritoryOwners()
    {
        var world = new World();
        var wolf = world.SpawnEntity("wolf", Vector3.Zero);
        world.SetWildTerritory(wolf.Id, Vector3.Zero, 4);
        var invalidSnapshot = JsonNode.Parse(WorldSnapshotSerializer.Serialize(world, 0))!.AsObject();
        var territories = invalidSnapshot["WildTerritories"]!.AsArray();
        territories.Add(territories[0]!.DeepClone());

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            new World(), invalidSnapshot.ToJsonString(), GetSpeciesCatalog()));
    }

    private static SpeciesCatalog GetSpeciesCatalog() =>
        SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
}
