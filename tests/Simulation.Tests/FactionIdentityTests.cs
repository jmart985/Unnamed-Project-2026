using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class FactionIdentityTests
{
    [Fact]
    public void DefinesDistinctFactionIdsAndChangesEntityAllegianceThroughWorld()
    {
        var world = new World();
        var aligned = world.SpawnEntity("human", Vector3.Zero);
        var unaligned = world.SpawnEntity("creature", Vector3.One);

        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("north-settlement"));
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("river-clan"));
        Assert.Equal(FactionDefinitionResult.DuplicateId, world.DefineFaction("north-settlement"));
        Assert.Equal(FactionDefinitionResult.InvalidId, world.DefineFaction(""));
        Assert.Equal(FactionDefinitionResult.InvalidId, world.DefineFaction("Bad-ID"));
        Assert.Equal(new[] { "north-settlement", "river-clan" }, world.FactionIds.Order(StringComparer.Ordinal));

        Assert.Equal(
            FactionAllegianceResult.Assigned,
            world.SetEntityAllegiance(aligned.Id, "north-settlement"));
        Assert.Equal(
            FactionAllegianceResult.UnknownFaction,
            world.SetEntityAllegiance(aligned.Id, "unknown"));
        Assert.Equal("north-settlement", aligned.AllegianceFactionId);
        Assert.Equal(
            FactionAllegianceResult.EntityNotFound,
            world.SetEntityAllegiance("missing", "river-clan"));
        Assert.Equal(
            FactionAllegianceResult.Removed,
            world.SetEntityAllegiance(aligned.Id, null));
        Assert.Null(aligned.AllegianceFactionId);
        Assert.Null(unaligned.AllegianceFactionId);

        Assert.Equal(
            FactionAllegianceResult.Assigned,
            world.SetEntityAllegiance(aligned.Id, "river-clan"));
        Assert.Equal("river-clan", aligned.AllegianceFactionId);
    }

    [Fact]
    public void RemovingEntitiesAndResettingWorldRemoveFactionReferencesAndDefinitions()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("town"));
        Assert.Equal(FactionAllegianceResult.Assigned, world.SetEntityAllegiance(entity.Id, "town"));

        Assert.True(world.TryRemoveEntity(entity.Id));
        Assert.Empty(world.Entities);
        Assert.Null(entity.AllegianceFactionId);
        Assert.Contains("town", world.FactionIds);

        world.Reset();

        Assert.Empty(world.Entities);
        Assert.Empty(world.FactionIds);
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("town"));
        Assert.Equal("entity-000002", world.SpawnEntity("human", Vector3.One).Id);
    }

    [Fact]
    public void SnapshotRoundTripPreservesFactionIdentitiesAndOptionalAllegiance()
    {
        var source = new World();
        Assert.Equal(FactionDefinitionResult.Defined, source.DefineFaction("town"));
        Assert.Equal(FactionDefinitionResult.Defined, source.DefineFaction("wild"));
        var aligned = source.SpawnEntity("human", Vector3.Zero);
        var unaligned = source.SpawnEntity("creature", Vector3.One);
        Assert.Equal(FactionAllegianceResult.Assigned, source.SetEntityAllegiance(aligned.Id, "town"));
        var json = WorldSnapshotSerializer.Serialize(source, 3);
        var target = new World();
        target.SpawnEntity("human", Vector3.UnitX * 3);

        var restoredTime = WorldSnapshotSerializer.Load(target, json, GetSpeciesCatalog());

        Assert.Equal(3, restoredTime);
        Assert.Equal(new[] { "town", "wild" }, target.FactionIds);
        Assert.Equal("town", target.Entities.Single(entity => entity.Id == aligned.Id).AllegianceFactionId);
        Assert.Null(target.Entities.Single(entity => entity.Id == unaligned.Id).AllegianceFactionId);
        Assert.Equal(json, WorldSnapshotSerializer.Serialize(target, restoredTime));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("blank")]
    [InlineData("dangling")]
    public void InvalidFactionSnapshotsDoNotReplaceWorldOrResetTransientState(string invalidation)
    {
        var world = new World();
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("existing"));
        var existingEntity = world.SpawnEntity("human", Vector3.Zero);
        Assert.Equal(
            FactionAllegianceResult.Assigned,
            world.SetEntityAllegiance(existingEntity.Id, "existing"));

        var incoming = new World();
        Assert.Equal(FactionDefinitionResult.Defined, incoming.DefineFaction("north"));
        var incomingEntity = incoming.SpawnEntity("creature", Vector3.One);
        Assert.Equal(FactionAllegianceResult.Assigned, incoming.SetEntityAllegiance(incomingEntity.Id, "north"));
        var invalidJson = WorldSnapshotSerializer.Serialize(incoming, 1);
        var document = JsonNode.Parse(invalidJson)!.AsObject();

        switch (invalidation)
        {
            case "duplicate":
                document["FactionIds"]!.AsArray().Add("north");
                break;
            case "blank":
                document["FactionIds"]!.AsArray().Add("");
                break;
            case "dangling":
                document["Entities"]!.AsArray()[0]!["AllegianceFactionId"] = "missing";
                break;
        }

        var resetProbe = new ResetProbe();

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            world,
            document.ToJsonString(),
            GetSpeciesCatalog(),
            resetProbe));

        Assert.Equal(0, resetProbe.ResetCount);
        Assert.Same(existingEntity, Assert.Single(world.Entities));
        Assert.Equal(new[] { "existing" }, world.FactionIds);
        Assert.Equal("existing", existingEntity.AllegianceFactionId);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void OlderSnapshotsMigrateWithoutFactionState(int schemaVersion)
    {
        var world = new World();
        var entity = world.SpawnEntity("creature", Vector3.Zero);
        var document = JsonNode.Parse(WorldSnapshotSerializer.Serialize(world, 2))!.AsObject();
        document["SchemaVersion"] = schemaVersion;
        document.Remove("FactionIds");
        var savedEntity = document["Entities"]!.AsArray()[0]!.AsObject();
        savedEntity.Remove("AllegianceFactionId");
        if (schemaVersion == 4)
        {
            savedEntity.Remove("CaptivityState");
        }

        var migrated = WorldSnapshotSerializer.Deserialize(document.ToJsonString());
        var target = new World();
        WorldSnapshotSerializer.Load(target, document.ToJsonString(), GetSpeciesCatalog());

        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Empty(migrated.FactionIds!);
        Assert.Equal(entity.Id, Assert.Single(target.Entities).Id);
        Assert.Null(Assert.Single(target.Entities).AllegianceFactionId);
        Assert.Empty(target.FactionIds);
    }

    private static SpeciesCatalog GetSpeciesCatalog() =>
        SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    private sealed class ResetProbe : ITransientSimulationState
    {
        public int ResetCount { get; private set; }

        public void Reset() => ResetCount++;
    }
}
