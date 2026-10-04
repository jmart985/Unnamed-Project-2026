using System.Numerics;
using Xunit;
using Simulation;

namespace Simulation.Tests;

public class InteractionResolverTests
{
    private (World World, InteractionResolver Resolver, SpeciesCatalog Catalog) CreateHarness()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var tradeCatalog = TradeGoodsCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "trade_goods.json"));
        var eventCatalog = RelationshipEventCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "relationship_events.json"));
        
        var world = new World();
        world.SpeciesCatalog = catalog;
        var socialService = new RelationshipEventService(world, eventCatalog);
        var resolver = new InteractionResolver(world, tradeCatalog, socialService);
        
        return (world, resolver, catalog);
    }

    [Fact]
    public void ResolveFeed_CorrectFood_Succeeds()
    {
        var (world, resolver, catalog) = CreateHarness();
        var species = catalog.GetRequired("creature");
        var creature = world.SpawnEntity(species, Vector3.Zero);
        var player = world.SpawnEntity(catalog.GetRequired("human"), Vector3.Zero);
        
        var foodItem = species.Diet.First();
        player.TryAddCarried(foodItem, 1);

        var container = new ResourceContainer("hand", 1);
        container.TryAdd(foodItem, 1);

        var request = new InteractionRequest(player.Id, creature.Id, InteractionType.Physical, "feed", container);
        var result = resolver.Resolve(request);

        Assert.True(result.Succeeded);
        Assert.Contains("Fed", result.Reason);
    }

    [Fact]
    public void ResolvePet_Creature_Succeeds()
    {
        var (world, resolver, catalog) = CreateHarness();
        var creature = world.SpawnEntity(catalog.GetRequired("creature"), Vector3.Zero);
        var player = world.SpawnEntity(catalog.GetRequired("human"), Vector3.Zero);

        var request = new InteractionRequest(player.Id, creature.Id, InteractionType.Physical, "pet");
        var result = resolver.Resolve(request);

        Assert.True(result.Succeeded);
        Assert.Contains("petted", result.Reason);
    }

    [Fact]
    public void ResolveToggleDoor_Structure_Succeeds()
    {
        var (world, resolver, catalog) = CreateHarness();
        var player = world.SpawnEntity(catalog.GetRequired("human"), Vector3.Zero);
        var structure = world.AddStructure("house", StructureType.Hut, Vector3.Zero);
        
        var request = new InteractionRequest(player.Id, structure.Id, InteractionType.Physical, "toggle_door");
        var result = resolver.Resolve(request);

        Assert.True(result.Succeeded);
        Assert.Contains("Door is now", result.Reason);
    }

    [Fact]
    public void ResolveGreet_Human_Succeeds()
    {
        var (world, resolver, catalog) = CreateHarness();
        var player = world.SpawnEntity(catalog.GetRequired("human"), Vector3.Zero);
        var npc = world.SpawnEntity(catalog.GetRequired("human"), Vector3.Zero);

        var request = new InteractionRequest(player.Id, npc.Id, InteractionType.Social, "greet");
        var result = resolver.Resolve(request);

        Assert.True(result.Succeeded);
        Assert.Contains("Successfully performed greet", result.Reason);
    }
}
