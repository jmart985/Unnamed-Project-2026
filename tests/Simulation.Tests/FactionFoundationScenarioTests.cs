using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class FactionFoundationScenarioTests
{
    [Fact]
    public void FactionFoundationIsDeterministicAndRecoversFromInvalidSnapshot()
    {
        Assert.Equal(RunScenario(), RunScenario());
    }

    private static string RunScenario()
    {
        var catalog = SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var world = new World();

        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("north"));
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("south"));
        var aligned = world.SpawnEntity("human", Vector3.Zero);
        var unaligned = world.SpawnEntity("creature", Vector3.One);
        Assert.Equal(FactionAllegianceResult.Assigned, world.SetEntityAllegiance(aligned.Id, "north"));
        Assert.Equal(FactionAllegianceResult.UnknownFaction,
            world.SetEntityAllegiance(aligned.Id, "missing"));
        Assert.Equal(FactionAllegianceResult.EntityNotFound,
            world.SetEntityAllegiance("missing", "south"));
        Assert.Equal("north", aligned.AllegianceFactionId);
        Assert.Null(unaligned.AllegianceFactionId);

        Assert.Equal(FactionReputationResult.Applied,
            world.AdjustFactionReputation(aligned.Id, "north", 35));
        Assert.Equal(FactionReputationResult.Applied,
            world.AdjustFactionReputation(aligned.Id, "south", 80));
        Assert.Equal(FactionReputationResult.OutOfRange,
            world.AdjustFactionReputation(aligned.Id, "north", -36));
        Assert.Equal(FactionReputationResult.EntityNotFound,
            world.AdjustFactionReputation("missing", "north", 1));
        Assert.Equal(FactionReputationResult.UnknownFaction,
            world.AdjustFactionReputation(aligned.Id, "missing", 1));
        Assert.Equal(FactionHostilityResult.Updated,
            world.SetFactionHostility("north", "south", true));
        Assert.Equal(FactionHostilityResult.Updated,
            world.SetFactionHostility("south", "north", false));
        Assert.Equal(FactionHostilityResult.SameFaction,
            world.SetFactionHostility("north", "north", true));

        var relationshipTarget = world.SpawnEntity("human", Vector3.UnitX);
        var relationshipAdjustment = new RelationshipAdjustment(0, 20, 0, 0);
        Assert.Equal(RelationshipAdjustmentResult.Applied,
            world.ApplyRelationshipAdjustment(aligned.Id, relationshipTarget.Id, relationshipAdjustment));

        var woodPile = new ResourceContainer("wood-pile", 8);
        Assert.Equal(ResourceMutationResult.Success, woodPile.TryAdd(ItemIds.Wood, 5));
        var source = world.AddResourcePile("wood-pile", Vector3.One, woodPile);
        var owned = world.AddStockpile(
            "owned-storage",
            Vector3.UnitX,
            new ResourceContainer("owned-storage", 8),
            "north");
        var unowned = world.AddStockpile(
            "unowned-storage",
            Vector3.UnitY,
            new ResourceContainer("unowned-storage", 8));
        Assert.Equal(FactionOwnershipResult.UnknownFaction,
            world.SetStockpileFactionOwner(unowned.Id, "missing"));
        Assert.Equal("north", owned.FactionOwnerId);
        Assert.Null(unowned.FactionOwnerId);
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryTransfer(source.Contents, owned.Contents, ItemIds.Wood, 3));
        Assert.Equal(2, source.Contents.Counts[ItemIds.Wood]);
        Assert.Equal(3, owned.Contents.Counts[ItemIds.Wood]);

        Assert.True(world.TryGetFactionReputation(aligned.Id, "north", out var northReputation));
        Assert.True(world.TryGetFactionReputation(aligned.Id, "south", out var southReputation));
        Assert.Equal(35, northReputation);
        Assert.Equal(80, southReputation);
        Assert.True(world.TryGetFactionHostility("north", "south", out var northHostile));
        Assert.True(world.TryGetFactionHostility("south", "north", out var southHostile));
        Assert.True(northHostile);
        Assert.False(southHostile);

        const double simulationTime = 12.5;
        var validSnapshot = WorldSnapshotSerializer.Serialize(world, simulationTime);
        var invalidSnapshot = JsonNode.Parse(validSnapshot)!.AsObject();
        invalidSnapshot["Stockpiles"]!.AsArray()[0]!["FactionOwnerId"] = "missing";
        var resetProbe = new ResetProbe();

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            world,
            invalidSnapshot.ToJsonString(),
            catalog,
            resetProbe));

        Assert.Equal(0, resetProbe.ResetCount);
        Assert.Same(aligned, Assert.Single(world.Entities, entity => entity.Id == aligned.Id));
        Assert.Equal(validSnapshot, WorldSnapshotSerializer.Serialize(world, simulationTime));

        var restoredTime = WorldSnapshotSerializer.Load(world, validSnapshot, catalog);
        Assert.Equal(simulationTime, restoredTime);
        Assert.Equal(validSnapshot, WorldSnapshotSerializer.Serialize(world, restoredTime));
        Assert.Equal("north", Assert.Single(world.Entities, entity => entity.Id == aligned.Id).AllegianceFactionId);
        Assert.Null(Assert.Single(world.Entities, entity => entity.Id == unaligned.Id).AllegianceFactionId);
        Assert.Equal(
            new[]
            {
                new FactionReputationState(aligned.Id, "north", 35),
                new FactionReputationState(aligned.Id, "south", 80)
            },
            world.FactionReputations);
        Assert.Equal(
            new[]
            {
                new FactionHostilityState("north", "south", true),
                new FactionHostilityState("south", "north", false)
            },
            world.FactionHostilities);
        Assert.Equal("north", world.Stockpiles.Single(stockpile => stockpile.Id == owned.Id).FactionOwnerId);
        Assert.Null(world.Stockpiles.Single(stockpile => stockpile.Id == unowned.Id).FactionOwnerId);
        Assert.Equal(2, world.ResourcePiles.Single().Contents.Counts[ItemIds.Wood]);
        Assert.Equal(3, world.Stockpiles.Single(stockpile => stockpile.Id == owned.Id).Contents.Counts[ItemIds.Wood]);
        Assert.Equal(20, Assert.Single(world.Relationships).Trust);

        return validSnapshot;
    }

    private sealed class ResetProbe : ITransientSimulationState
    {
        public int ResetCount { get; private set; }

        public void Reset() => ResetCount++;
    }
}
