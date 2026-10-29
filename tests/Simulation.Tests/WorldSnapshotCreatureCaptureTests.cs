using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class WorldSnapshotCreatureCaptureTests
{
    [Fact]
    public void SnapshotRoundTripPreservesCreatureCaptivity()
    {
        var source = CreateWorld();
        var creature = source.SpawnEntity("creature", Vector3.One);
        Assert.Equal(CreatureCaptureResult.Captured, source.TryCaptureCreature(creature.Id));
        var json = WorldSnapshotSerializer.Serialize(source, 3);

        var target = new World();
        var restoredTime = WorldSnapshotSerializer.Load(target, json, GetSpeciesCatalog());
        var restored = Assert.Single(target.Entities);

        Assert.Equal(3, restoredTime);
        Assert.Equal(creature.Id, restored.Id);
        Assert.Equal("creature", restored.SpeciesId);
        Assert.Equal(CreatureCaptivityState.Captured, restored.CaptivityState);
        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, WorldSnapshotSerializer.Deserialize(json).SchemaVersion);
    }

    [Fact]
    public void SnapshotRoundTripPreservesEveryCreatureLifecycleState()
    {
        var source = CreateWorld();
        var captured = source.SpawnEntity("creature", Vector3.Zero);
        var captive = source.SpawnEntity("wolf", Vector3.One);
        var domesticated = source.SpawnEntity("cow", Vector3.UnitX * 2);
        Assert.Equal(CreatureCaptureResult.Captured, source.TryCaptureCreature(captured.Id));
        Assert.Equal(CreatureCaptureResult.Captured, source.TryCaptureCreature(captive.Id));
        Assert.Equal(CreatureCaptureResult.Captured, source.TryCaptureCreature(domesticated.Id));
        Assert.Equal(
            CreatureLifecycleTransitionResult.Transitioned,
            source.TryTransitionCreatureState(captive.Id, CreatureCaptivityState.Captive));
        Assert.Equal(
            CreatureLifecycleTransitionResult.Transitioned,
            source.TryTransitionCreatureState(domesticated.Id, CreatureCaptivityState.Captive));
        Assert.Equal(
            CreatureLifecycleTransitionResult.Transitioned,
            source.TryTransitionCreatureState(domesticated.Id, CreatureCaptivityState.Domesticated));
        var json = WorldSnapshotSerializer.Serialize(source, 3);

        var target = new World();
        WorldSnapshotSerializer.Load(target, json, GetSpeciesCatalog());
        var restored = target.Entities.OrderBy(entity => entity.Id, StringComparer.Ordinal).ToArray();

        Assert.Equal(
            [
                CreatureCaptivityState.Captured,
                CreatureCaptivityState.Captive,
                CreatureCaptivityState.Domesticated
            ],
            restored.Select(entity => entity.CaptivityState));
        Assert.Equal(json, WorldSnapshotSerializer.Serialize(target, 3));
    }

    [Fact]
    public void InvalidCaptivitySnapshotDoesNotResetServicesOrReplaceWorld()
    {
        var source = CreateWorld();
        var creature = source.SpawnEntity("creature", Vector3.One);
        Assert.Equal(CreatureCaptureResult.Captured, source.TryCaptureCreature(creature.Id));
        var invalidJson = WorldSnapshotSerializer.Serialize(source, 3)
            .Replace("\"CaptivityState\": \"Captured\"", "\"CaptivityState\": 42", StringComparison.Ordinal);

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
        var source = CreateWorld();
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

    [Theory]
    [InlineData("Captured")]
    [InlineData("Captive")]
    [InlineData("Domesticated")]
    public void NonWildHumanSnapshotIsRejectedBeforeReplacingWorld(string lifecycleState)
    {
        var source = new World();
        var human = source.SpawnEntity("human", Vector3.One);
        var json = WorldSnapshotSerializer.Serialize(source, 3)
            .Replace("\"CaptivityState\": \"Wild\"", $"\"CaptivityState\": \"{lifecycleState}\"", StringComparison.Ordinal);

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

    private static World CreateWorld() => new() { SpeciesCatalog = GetSpeciesCatalog() };

    private sealed class CountingTransientState : ITransientSimulationState
    {
        public int ResetCount { get; private set; }

        public void Reset() => ResetCount++;
    }
}
