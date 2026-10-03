using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class FactionOwnershipTests
{
    [Fact]
    public void StockpileOwnershipIsAssignedAndRejectedAtomicallyThroughWorld()
    {
        var world = CreateWorld();
        var unowned = AddStockpile(world, "unowned");
        var owned = AddStockpile(world, "owned", "north");

        Assert.Null(unowned.FactionOwnerId);
        Assert.Equal("north", owned.FactionOwnerId);
        Assert.Equal(
            FactionOwnershipResult.Assigned,
            world.SetStockpileFactionOwner("unowned", "south"));
        Assert.Equal("south", unowned.FactionOwnerId);
        Assert.Equal(
            FactionOwnershipResult.UnknownFaction,
            world.SetStockpileFactionOwner("unowned", "missing"));
        Assert.Equal("south", unowned.FactionOwnerId);
        Assert.Equal(
            FactionOwnershipResult.ContainerNotFound,
            world.SetStockpileFactionOwner("missing", "north"));
        Assert.Equal(
            FactionOwnershipResult.Removed,
            world.SetStockpileFactionOwner("unowned", null));
        Assert.Null(unowned.FactionOwnerId);

        Assert.Throws<ArgumentException>(() =>
            world.AddStockpile("rejected", Vector3.Zero, new ResourceContainer("rejected", 5), "missing"));
        var acceptedAfterRejectedCreation =
            world.AddStockpile("rejected", Vector3.Zero, new ResourceContainer("rejected", 5));
        Assert.Null(acceptedAfterRejectedCreation.FactionOwnerId);
    }

    [Fact]
    public void RemovingOrResettingWorldClearsStockpileOwnerReferences()
    {
        var world = CreateWorld();
        var removed = AddStockpile(world, "removed", "north");
        var reset = AddStockpile(world, "reset", "south");

        Assert.True(world.TryRemoveStockpile(removed.Id));
        Assert.Null(removed.FactionOwnerId);
        Assert.Equal(
            FactionOwnershipResult.ContainerNotFound,
            world.SetStockpileFactionOwner(removed.Id, "north"));

        world.Reset();

        Assert.Empty(world.Stockpiles);
        Assert.Null(reset.FactionOwnerId);
        Assert.Empty(world.FactionIds);
    }

    [Fact]
    public void StockpileOwnershipRoundTripsWithoutChangingResourceTransfers()
    {
        var source = CreateWorld();
        var stockpile = AddStockpile(source, "owned-stockpile", "north");
        var pile = new ResourceContainer("wood-pile", 10);
        Assert.Equal(ResourceMutationResult.Success, pile.TryAdd(ItemIds.Wood, 4));
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryTransfer(pile, stockpile.Contents, ItemIds.Wood, 3));
        Assert.Equal("north", stockpile.FactionOwnerId);
        Assert.Equal(1, pile.Counts[ItemIds.Wood]);
        Assert.Equal(3, stockpile.Contents.Counts[ItemIds.Wood]);

        var json = WorldSnapshotSerializer.Serialize(source, 5);
        var target = new World();
        var restoredTime = WorldSnapshotSerializer.Load(target, json, GetSpeciesCatalog());

        Assert.Equal(5, restoredTime);
        var restoredStockpile = Assert.Single(target.Stockpiles);
        Assert.Equal("north", restoredStockpile.FactionOwnerId);
        Assert.Equal(stockpile.Contents.Counts, restoredStockpile.Contents.Counts);
        Assert.Equal(json, WorldSnapshotSerializer.Serialize(target, restoredTime));
    }

    [Theory]
    [InlineData("unknown-owner")]
    [InlineData("resource-pile-owner")]
    [InlineData("duplicate-stockpile")]
    public void InvalidStockpileOwnershipSnapshotsPreserveWorldAndTransientState(string invalidation)
    {
        var world = CreateWorld();
        AddStockpile(world, "existing", "south");
        var existingSnapshot = WorldSnapshotSerializer.Serialize(world, 3);

        var incoming = CreateWorld();
        AddStockpile(incoming, "incoming", "north");
        var document = JsonNode.Parse(WorldSnapshotSerializer.Serialize(incoming, 1))!.AsObject();
        var stockpiles = document["Stockpiles"]!.AsArray();

        switch (invalidation)
        {
            case "unknown-owner":
                stockpiles[0]!["FactionOwnerId"] = "missing";
                break;
            case "resource-pile-owner":
                var pile = new ResourceContainer("pile", 5);
                incoming.AddResourcePile("pile", Vector3.One, pile);
                document = JsonNode.Parse(WorldSnapshotSerializer.Serialize(incoming, 1))!.AsObject();
                document["ResourcePiles"]!.AsArray()[0]!["FactionOwnerId"] = "north";
                break;
            case "duplicate-stockpile":
                stockpiles.Add(stockpiles[0]!.DeepClone());
                break;
        }

        var resetProbe = new ResetProbe();
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            world,
            document.ToJsonString(),
            GetSpeciesCatalog(),
            resetProbe));

        Assert.Equal(0, resetProbe.ResetCount);
        Assert.Equal(existingSnapshot, WorldSnapshotSerializer.Serialize(world, 3));
    }

    [Fact]
    public void SchemaSevenSnapshotsMigrateStockpilesAsUnowned()
    {
        var source = CreateWorld();
        var entity = source.SpawnEntity("human", Vector3.Zero);
        Assert.Equal(FactionReputationResult.Applied, source.AdjustFactionReputation(entity.Id, "north", 25));
        Assert.Equal(FactionHostilityResult.Updated, source.SetFactionHostility("north", "south", true));
        AddStockpile(source, "formerly-owned", "north");
        AddStockpile(source, "formerly-unowned");
        var document = JsonNode.Parse(WorldSnapshotSerializer.Serialize(source, 2))!.AsObject();
        document["SchemaVersion"] = 7;
        foreach (var stockpile in document["Stockpiles"]!.AsArray())
        {
            stockpile!.AsObject().Remove("FactionOwnerId");
        }

        var target = new World();
        WorldSnapshotSerializer.Load(target, document.ToJsonString(), GetSpeciesCatalog());

        Assert.All(target.Stockpiles, stockpile => Assert.Null(stockpile.FactionOwnerId));
        Assert.True(target.TryGetFactionReputation(entity.Id, "north", out var reputation));
        Assert.Equal(25, reputation);
        Assert.True(target.TryGetFactionHostility("north", "south", out var hostile));
        Assert.True(hostile);
    }

    private static World CreateWorld()
    {
        var world = new World();
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("north"));
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("south"));
        return world;
    }

    private static ResourceLocation AddStockpile(World world, string id, string? factionOwnerId = null) =>
        world.AddStockpile(
            id,
            Vector3.Zero,
            new ResourceContainer(id, 10),
            factionOwnerId);

    private static SpeciesCatalog GetSpeciesCatalog() =>
        SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    private sealed class ResetProbe : ITransientSimulationState
    {
        public int ResetCount { get; private set; }

        public void Reset() => ResetCount++;
    }
}
