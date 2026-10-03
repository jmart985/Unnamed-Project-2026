using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class FactionRelationsTests
{
    [Fact]
    public void ReputationIsIndependentPerEntityAndFaction()
    {
        var world = CreateWorld(out var entity);
        var adjustment = new RelationshipAdjustment(0, 25, 0, 0);
        Assert.Equal(RelationshipAdjustmentResult.Applied,
            world.ApplyRelationshipAdjustment(entity.Id, world.SpawnEntity("human", Vector3.UnitX).Id, adjustment));

        Assert.Equal(FactionReputationResult.Applied, world.AdjustFactionReputation(entity.Id, "north", 35));
        Assert.Equal(FactionReputationResult.Applied, world.AdjustFactionReputation(entity.Id, "south", 70));

        Assert.True(world.TryGetFactionReputation(entity.Id, "north", out var northReputation));
        Assert.True(world.TryGetFactionReputation(entity.Id, "south", out var southReputation));
        Assert.Equal(35, northReputation);
        Assert.Equal(70, southReputation);
        Assert.Equal(25, world.Relationships.Single().Trust);
    }

    [Fact]
    public void ReputationRejectsInvalidReferencesAndOutOfRangeChangesAtomically()
    {
        var world = CreateWorld(out var entity);

        Assert.Equal(FactionReputationResult.Applied,
            world.AdjustFactionReputation(entity.Id, "north", 100));
        Assert.Equal(FactionReputationResult.OutOfRange,
            world.AdjustFactionReputation(entity.Id, "north", double.Epsilon));
        Assert.Equal(FactionReputationResult.OutOfRange,
            world.AdjustFactionReputation(entity.Id, "north", -double.MaxValue));
        Assert.Equal(FactionReputationResult.InvalidDelta,
            world.AdjustFactionReputation(entity.Id, "north", double.NaN));
        Assert.Equal(FactionReputationResult.UnknownFaction,
            world.AdjustFactionReputation(entity.Id, "missing", 1));
        Assert.Equal(FactionReputationResult.EntityNotFound,
            world.AdjustFactionReputation("missing", "north", 1));
        Assert.True(world.TryGetFactionReputation(entity.Id, "north", out var value));
        Assert.Equal(100, value);

        Assert.Equal(FactionReputationResult.Applied,
            world.AdjustFactionReputation(entity.Id, "north", -100));
        Assert.True(world.TryGetFactionReputation(entity.Id, "north", out value));
        Assert.Equal(0, value);

        Assert.True(world.TryRemoveEntity(entity.Id));
        Assert.DoesNotContain(world.FactionReputations, reputation => reputation.EntityId == entity.Id);
    }

    [Fact]
    public void HostilityIsDirectedAndRejectsUnknownOrSelfFactionReferences()
    {
        var world = CreateWorld(out _);

        Assert.Equal(FactionHostilityResult.Updated, world.SetFactionHostility("north", "south", true));
        Assert.Equal(FactionHostilityResult.Updated, world.SetFactionHostility("south", "north", false));
        Assert.True(world.TryGetFactionHostility("north", "south", out var northHostile));
        Assert.True(northHostile);
        Assert.True(world.TryGetFactionHostility("south", "north", out var southHostile));
        Assert.False(southHostile);
        Assert.Equal(FactionHostilityResult.SameFaction, world.SetFactionHostility("north", "north", true));
        Assert.Equal(FactionHostilityResult.UnknownFaction, world.SetFactionHostility("missing", "north", true));
        Assert.Equal(2, world.FactionHostilities.Count);

        Assert.Equal(FactionHostilityResult.Updated, world.SetFactionHostility("north", "south", false));
        Assert.True(world.TryGetFactionHostility("north", "south", out northHostile));
        Assert.False(northHostile);
    }

    [Fact]
    public void SnapshotRoundTripPreservesFactionReputationAndDirectedHostility()
    {
        var source = CreateWorld(out var entity);
        Assert.Equal(FactionReputationResult.Applied,
            source.AdjustFactionReputation(entity.Id, "north", 46));
        Assert.Equal(FactionReputationResult.Applied,
            source.AdjustFactionReputation(entity.Id, "south", 82));
        Assert.Equal(FactionHostilityResult.Updated,
            source.SetFactionHostility("north", "south", true));

        var json = WorldSnapshotSerializer.Serialize(source, 5);
        var target = new World();
        var restoredTime = WorldSnapshotSerializer.Load(target, json, GetSpeciesCatalog());

        Assert.Equal(5, restoredTime);
        Assert.Equal(
            new[]
            {
                new FactionReputationState(entity.Id, "north", 46),
                new FactionReputationState(entity.Id, "south", 82)
            },
            target.FactionReputations);
        Assert.Equal(
            new[] { new FactionHostilityState("north", "south", true) },
            target.FactionHostilities);
        Assert.Equal(json, WorldSnapshotSerializer.Serialize(target, restoredTime));
    }

    [Theory]
    [InlineData("duplicate-reputation")]
    [InlineData("unknown-entity")]
    [InlineData("unknown-faction")]
    [InlineData("out-of-range-reputation")]
    [InlineData("duplicate-hostility")]
    [InlineData("unknown-hostility-faction")]
    [InlineData("self-hostility")]
    public void InvalidFactionRelationSnapshotsPreserveWorldAndTransientState(string invalidation)
    {
        var world = CreateWorld(out var existingEntity);
        Assert.Equal(FactionReputationResult.Applied,
            world.AdjustFactionReputation(existingEntity.Id, "north", 22));
        Assert.Equal(FactionHostilityResult.Updated,
            world.SetFactionHostility("north", "south", true));
        var existingSnapshot = WorldSnapshotSerializer.Serialize(world, 3);

        var incoming = CreateWorld(out var incomingEntity);
        Assert.Equal(FactionReputationResult.Applied,
            incoming.AdjustFactionReputation(incomingEntity.Id, "north", 50));
        Assert.Equal(FactionHostilityResult.Updated,
            incoming.SetFactionHostility("north", "south", true));
        var document = JsonNode.Parse(WorldSnapshotSerializer.Serialize(incoming, 1))!.AsObject();
        var reputations = document["FactionReputations"]!.AsArray();
        var hostilities = document["FactionHostilities"]!.AsArray();

        switch (invalidation)
        {
            case "duplicate-reputation":
                reputations.Add(reputations[0]!.DeepClone());
                break;
            case "unknown-entity":
                reputations[0]!["EntityId"] = "missing";
                break;
            case "unknown-faction":
                reputations[0]!["FactionId"] = "missing";
                break;
            case "out-of-range-reputation":
                reputations[0]!["Value"] = 101;
                break;
            case "duplicate-hostility":
                hostilities.Add(hostilities[0]!.DeepClone());
                break;
            case "unknown-hostility-faction":
                hostilities[0]!["TargetFactionId"] = "missing";
                break;
            case "self-hostility":
                hostilities[0]!["TargetFactionId"] = "north";
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
        Assert.Equal(existingSnapshot, WorldSnapshotSerializer.Serialize(world, 3));
    }

    [Fact]
    public void SchemaSixSnapshotsMigrateWithFactionIdentitiesButNoFactionRelations()
    {
        var source = CreateWorld(out var entity);
        Assert.Equal(FactionAllegianceResult.Assigned,
            source.SetEntityAllegiance(entity.Id, "north"));
        var document = JsonNode.Parse(WorldSnapshotSerializer.Serialize(source, 2))!.AsObject();
        document["SchemaVersion"] = 6;
        document.Remove("FactionReputations");
        document.Remove("FactionHostilities");

        var migrated = WorldSnapshotSerializer.Deserialize(document.ToJsonString());
        var target = new World();
        WorldSnapshotSerializer.Load(target, document.ToJsonString(), GetSpeciesCatalog());

        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(new[] { "north", "south" }, target.FactionIds);
        Assert.Equal("north", Assert.Single(target.Entities).AllegianceFactionId);
        Assert.Empty(target.FactionReputations);
        Assert.Empty(target.FactionHostilities);
    }

    private static World CreateWorld(out EntityRecord entity)
    {
        var world = new World();
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("north"));
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("south"));
        entity = world.SpawnEntity("human", Vector3.Zero);
        return world;
    }

    private static SpeciesCatalog GetSpeciesCatalog() =>
        SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    private sealed class ResetProbe : ITransientSimulationState
    {
        public int ResetCount { get; private set; }

        public void Reset() => ResetCount++;
    }
}
