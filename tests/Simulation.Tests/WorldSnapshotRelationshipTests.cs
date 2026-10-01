using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class WorldSnapshotRelationshipTests
{
    [Fact]
    public void CaptureIncludesRelationshipsInDeterministicPairOrder()
    {
        var world = new World();
        var first = world.SpawnEntity("human", Vector3.Zero);
        var second = world.SpawnEntity("human", Vector3.One);
        var third = world.SpawnEntity("creature", Vector3.UnitZ);
        Assert.True(world.TryGetOrCreateRelationship(second.Id, first.Id, out _));
        Assert.True(world.TryGetOrCreateRelationship(first.Id, third.Id, out _));

        var snapshot = WorldSnapshotSerializer.Capture(world, 0);

        Assert.Collection(
            snapshot.Relationships,
            relationship =>
            {
                Assert.Equal(first.Id, relationship.ObserverEntityId);
                Assert.Equal(third.Id, relationship.TargetEntityId);
            },
            relationship =>
            {
                Assert.Equal(second.Id, relationship.ObserverEntityId);
                Assert.Equal(first.Id, relationship.TargetEntityId);
            });
    }

    [Fact]
    public void LoadRestoresAllRelationshipValuesAndDirectionality()
    {
        var sourceWorld = new World();
        var first = sourceWorld.SpawnEntity("human", Vector3.Zero);
        var second = sourceWorld.SpawnEntity("creature", Vector3.One);
        Assert.True(sourceWorld.TryGetOrCreateRelationship(first.Id, second.Id, out var forward));
        Assert.True(sourceWorld.TryGetOrCreateRelationship(second.Id, first.Id, out var reverse));
        forward!.ApplyDelta(RelationshipDimension.Familiarity, 14);
        forward.ApplyDelta(RelationshipDimension.Trust, 25);
        forward.ApplyDelta(RelationshipDimension.Fear, 36);
        forward.ApplyDelta(RelationshipDimension.Loyalty, 47);
        reverse!.ApplyDelta(RelationshipDimension.Trust, 63);
        var json = WorldSnapshotSerializer.Serialize(sourceWorld, 0);

        var restoredWorld = new World();
        var time = WorldSnapshotSerializer.Load(restoredWorld, json, GetSpeciesCatalog());

        Assert.Equal(0, time);
        Assert.True(restoredWorld.TryGetRelationship(first.Id, second.Id, out var restoredForward));
        Assert.True(restoredWorld.TryGetRelationship(second.Id, first.Id, out var restoredReverse));
        Assert.Equal(14, restoredForward!.Familiarity);
        Assert.Equal(25, restoredForward.Trust);
        Assert.Equal(36, restoredForward.Fear);
        Assert.Equal(47, restoredForward.Loyalty);
        Assert.Equal(63, restoredReverse!.Trust);
        Assert.Equal(0, restoredReverse.Familiarity);
        Assert.Collection(
            WorldSnapshotSerializer.Capture(restoredWorld, 0).Relationships,
            relationship => Assert.Equal(first.Id, relationship.ObserverEntityId),
            relationship => Assert.Equal(second.Id, relationship.ObserverEntityId));
    }

    [Fact]
    public void InvalidRelationshipSnapshotsFailBeforeWorldOrTransientMutation()
    {
        var sourceWorld = new World();
        var first = sourceWorld.SpawnEntity("human", Vector3.Zero);
        var second = sourceWorld.SpawnEntity("creature", Vector3.One);
        Assert.True(sourceWorld.TryGetOrCreateRelationship(first.Id, second.Id, out var sourceRelationship));
        sourceRelationship!.ApplyDelta(RelationshipDimension.Familiarity, 20);
        sourceRelationship!.ApplyDelta(RelationshipDimension.Trust, 25);
        var validJson = WorldSnapshotSerializer.Serialize(sourceWorld, 0);

        var invalidTargetJson = validJson.Replace(
            $"\"TargetEntityId\": \"{second.Id}\"",
            "\"TargetEntityId\": \"missing-entity\"",
            StringComparison.Ordinal);
        var invalidValueJson = validJson.Replace(
            "\"Trust\": 25",
            "\"Trust\": 101",
            StringComparison.Ordinal);
        var negativeValueJson = validJson.Replace(
            "\"Familiarity\": 20",
            "\"Familiarity\": -0.1",
            StringComparison.Ordinal);
        var selfRelationshipJson = validJson.Replace(
            $"\"TargetEntityId\": \"{second.Id}\"",
            $"\"TargetEntityId\": \"{first.Id}\"",
            StringComparison.Ordinal);
        var nonFiniteValueJson = validJson.Replace(
            "\"Trust\": 25",
            "\"Trust\": 1e999",
            StringComparison.Ordinal);
        var duplicateRoot = JsonNode.Parse(validJson)!.AsObject();
        var relationships = duplicateRoot["Relationships"]!.AsArray();
        relationships.Add(relationships[0]!.DeepClone());
        var duplicatePairJson = duplicateRoot.ToJsonString();

        foreach (var invalidJson in new[]
            {
                invalidTargetJson,
                invalidValueJson,
                negativeValueJson,
                selfRelationshipJson,
                nonFiniteValueJson,
                duplicatePairJson
            })
        {
            var targetWorld = new World();
            var existing = targetWorld.SpawnEntity("human", Vector3.Zero);
            var other = targetWorld.SpawnEntity("human", Vector3.One);
            Assert.True(targetWorld.TryGetOrCreateRelationship(existing.Id, other.Id, out var existingRelationship));
            existingRelationship!.ApplyDelta(RelationshipDimension.Trust, 72);
            var transientState = new RecordingTransientState();

            Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
                targetWorld,
                invalidJson,
                GetSpeciesCatalog(),
                transientState));

            Assert.Equal(0, transientState.ResetCount);
            Assert.Same(existing, Assert.Single(targetWorld.Entities, entity => entity.Id == existing.Id));
            Assert.Same(existingRelationship, Assert.Single(targetWorld.Relationships));
            Assert.Equal(72, existingRelationship.Trust);
        }
    }

    [Fact]
    public void UnsupportedPriorSnapshotSchemaIsRejected()
    {
        var world = new World();
        var json = WorldSnapshotSerializer.Serialize(world, 0)
            .Replace(
                $"\"SchemaVersion\": {WorldSnapshotSerializer.CurrentSchemaVersion}",
                "\"SchemaVersion\": 2",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Deserialize(json));
    }

    private static SpeciesCatalog GetSpeciesCatalog() =>
        SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    private sealed class RecordingTransientState : ITransientSimulationState
    {
        public int ResetCount { get; private set; }

        public void Reset()
        {
            ResetCount++;
        }
    }
}
