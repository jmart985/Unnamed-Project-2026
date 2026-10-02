using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class WorldSnapshotCreatureCaptureTests
{
    [Fact]
    public void SnapshotRoundTripPreservesCreatureCaptivity()
    {
        var source = new World();
        var creature = source.SpawnEntity("creature", Vector3.One);
        Assert.Equal(CreatureCaptureResult.Captured, source.TryCaptureCreature(creature.Id));
        var json = WorldSnapshotSerializer.Serialize(source, 3);

        var target = new World();
        var restoredTime = WorldSnapshotSerializer.Load(target, json, GetSpeciesCatalog());
        var restored = Assert.Single(target.Entities);

        Assert.Equal(3, restoredTime);
        Assert.Equal(creature.Id, restored.Id);
        Assert.Equal("creature", restored.SpeciesId);
        Assert.Equal(CreatureCaptivityState.Captive, restored.CaptivityState);
        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, WorldSnapshotSerializer.Deserialize(json).SchemaVersion);
    }

    [Fact]
    public void InvalidCaptivitySnapshotDoesNotResetServicesOrReplaceWorld()
    {
        var source = new World();
        var creature = source.SpawnEntity("creature", Vector3.One);
        Assert.Equal(CreatureCaptureResult.Captured, source.TryCaptureCreature(creature.Id));
        var invalidJson = WorldSnapshotSerializer.Serialize(source, 3)
            .Replace("\"CaptivityState\": \"Captive\"", "\"CaptivityState\": 42", StringComparison.Ordinal);

        var target = new World();
        var existingEntity = target.SpawnEntity("human", Vector3.Zero);
        var transientState = new CountingTransientState();

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            target,
            invalidJson,
            GetSpeciesCatalog(),
            transientState));

        Assert.Same(existingEntity, Assert.Single(target.Entities));
        Assert.Equal(0, transientState.ResetCount);
    }

    [Fact]
    public void VersionFourSnapshotsRestoreEntitiesAsWild()
    {
        var source = new World();
        source.SpawnEntity("creature", Vector3.One);
        var json = WorldSnapshotSerializer.Serialize(source, 3);
        var legacySnapshot = JsonNode.Parse(json)!;
        legacySnapshot["SchemaVersion"] = 4;
        foreach (var entity in legacySnapshot["Entities"]!.AsArray())
        {
            entity!.AsObject().Remove("CaptivityState");
        }

        var target = new World();
        WorldSnapshotSerializer.Load(target, legacySnapshot.ToJsonString(), GetSpeciesCatalog());

        Assert.Equal(CreatureCaptivityState.Wild, Assert.Single(target.Entities).CaptivityState);
        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, WorldSnapshotSerializer.Capture(target, 3).SchemaVersion);
    }

    [Fact]
    public void CaptiveHumanSnapshotIsRejectedBeforeReplacingWorld()
    {
        var source = new World();
        var human = source.SpawnEntity("human", Vector3.One);
        var json = WorldSnapshotSerializer.Serialize(source, 3)
            .Replace("\"CaptivityState\": \"Wild\"", "\"CaptivityState\": \"Captive\"", StringComparison.Ordinal);

        var target = new World();
        var existingEntity = target.SpawnEntity("creature", Vector3.Zero);

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            target,
            json,
            GetSpeciesCatalog()));

        Assert.Same(existingEntity, Assert.Single(target.Entities));
        Assert.Equal(CreatureCaptivityState.Wild, human.CaptivityState);
    }

    private static SpeciesCatalog GetSpeciesCatalog() =>
        SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    private sealed class CountingTransientState : ITransientSimulationState
    {
        public int ResetCount { get; private set; }

        public void Reset() => ResetCount++;
    }
}
