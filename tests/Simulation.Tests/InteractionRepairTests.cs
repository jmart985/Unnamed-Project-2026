using System.Numerics;
using System.Text;

namespace Simulation.Tests;

public sealed class InteractionRepairTests
{
    private static (World World, InteractionResolver Resolver, EntityRecord Actor, EntityRecord Target) Harness(bool missingHelp = false, int capacity = 10)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "data");
        var species = SpeciesCatalog.LoadFromFile(Path.Combine(root, "species.json"));
        var world = new World(inventoryCapacity: capacity) { SpeciesCatalog = species };
        var events = missingHelp ? RelationshipEventCatalog.Load(new MemoryStream(Encoding.UTF8.GetBytes(
            "[{\"id\":\"greeted\",\"familiarityDelta\":5,\"trustDelta\":0,\"fearDelta\":0,\"loyaltyDelta\":0}]")))
            : RelationshipEventCatalog.LoadFromFile(Path.Combine(root, "relationship_events.json"));
        var resolver = new InteractionResolver(world, TradeGoodsCatalog.LoadFromFile(Path.Combine(root, "trade_goods.json")), new(world, events));
        return (world, resolver, world.SpawnEntity(species.GetRequired("human"), Vector3.Zero),
            world.SpawnEntity(species.GetRequired("cow"), new(1, 0, 0)));
    }

    private static ResourceContainer Item(string id, int count)
    {
        var descriptor = new ResourceContainer("provided item", count);
        Assert.Equal(ResourceMutationResult.Success, descriptor.TryAdd(id, count));
        return descriptor;
    }

    [Fact]
    public void FeedConsumesOneUnitFromStackAndUsesConfiguredNutrition()
    {
        var (world, resolver, actor, target) = Harness();
        actor.TryAddCarried(ItemIds.Plant, 5);
        var descriptor = Item(ItemIds.Plant, 5);
        var before = target.Satiety;
        var result = resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, "feed", descriptor));
        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(4, actor.Inventory[ItemIds.Plant]);
        Assert.Equal(5, descriptor.Counts[ItemIds.Plant]);
        Assert.Equal(Math.Min(100, before + target.Needs.NutritionPerUnit), target.Satiety);
        Assert.True(world.TryGetRelationship(target.Id, actor.Id, out var bond));
        Assert.Equal(8, bond!.Trust);
        Assert.False(world.TryGetRelationship(actor.Id, target.Id, out _));
        Assert.False(resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, "feed", descriptor)).Succeeded);
        Assert.Equal(4, actor.Inventory[ItemIds.Plant]);
    }

    [Fact]
    public void FeedRejectsWrongMissingOrAmbiguousFoodWithoutMutating()
    {
        var (world, resolver, actor, target) = Harness();
        actor.TryAddCarried(ItemIds.Meat, 2);
        var mixed = new ResourceContainer("mixed", 2); mixed.TryAdd(ItemIds.Plant, 1); mixed.TryAdd(ItemIds.Wood, 1);
        var before = WorldSnapshotSerializer.Serialize(world, 0);
        foreach (var supplied in new[] { Item(ItemIds.Meat, 1), Item(ItemIds.Plant, 1), mixed, null })
            Assert.False(resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, "feed", supplied)).Succeeded);
        Assert.Equal(before, WorldSnapshotSerializer.Serialize(world, 0));
    }

    [Theory]
    [InlineData("capture")]
    [InlineData("feed")]
    [InlineData("pet")]
    [InlineData("give")]
    public void PhysicalActionsRejectInvalidParticipantsBeforeMutation(string action)
    {
        var (world, resolver, actor, target) = Harness();
        actor.TryAddCarried(ItemIds.Plant, 3);
        var item = Item(ItemIds.Plant, 1);
        void Unchanged(string? targetId)
        {
            var before = WorldSnapshotSerializer.Serialize(world, 0);
            var healthBefore = target.Health.Parts.Select(part => part.CurrentHealth).ToArray();
            var deathBefore = target.Health.PendingDeathSeconds;
            Assert.False(resolver.Resolve(new(actor.Id, targetId, InteractionType.Physical, action, item)).Succeeded);
            Assert.Equal(before, WorldSnapshotSerializer.Serialize(world, 0));
            Assert.Equal(healthBefore, target.Health.Parts.Select(part => part.CurrentHealth));
            Assert.Equal(deathBefore, target.Health.PendingDeathSeconds);
        }
        Unchanged(null); Unchanged("missing"); Unchanged(actor.Id);
        world.TrySetEntityPosition(target.Id, new(3, 0, 0)); Unchanged(target.Id);
        world.TrySetEntityPosition(target.Id, new(1, 0, 0));
        target.Health.TryDamage("Head", target.BaseStats.MaxHealth); Unchanged(target.Id);
        target.Health.TryHeal("Head", target.BaseStats.MaxHealth);
        actor.Health.TryDamage("Torso", actor.BaseStats.MaxHealth); Unchanged(target.Id);
        Assert.Equal(3, actor.Inventory[ItemIds.Plant]);
        Assert.Equal(CreatureCaptivityState.Wild, target.CaptivityState);
        Assert.Empty(world.Relationships);
    }

    [Fact]
    public void CaptureAndPetReportActualOutcomes()
    {
        var (world, resolver, actor, target) = Harness();
        Assert.True(resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, "capture")).Succeeded);
        Assert.Equal(CreatureCaptivityState.Captured, target.CaptivityState);
        Assert.False(resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, "capture")).Succeeded);
        Assert.True(resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, "pet")).Succeeded);
        Assert.True(world.TryGetRelationship(target.Id, actor.Id, out var relation)); Assert.Equal(8, relation!.Trust);
        var human = world.SpawnEntity(world.SpeciesCatalog!.GetRequired("human"), Vector3.Zero);
        Assert.False(resolver.Resolve(new(actor.Id, human.Id, InteractionType.Physical, "capture")).Succeeded);
        Assert.False(resolver.Resolve(new(actor.Id, human.Id, InteractionType.Physical, "pet")).Succeeded);
        var wolf = world.SpawnEntity(world.SpeciesCatalog.GetRequired("wolf"), Vector3.Zero);
        Assert.False(resolver.Resolve(new(actor.Id, wolf.Id, InteractionType.Physical, "pet")).Succeeded);
    }

    [Theory]
    [InlineData("feed")]
    [InlineData("pet")]
    [InlineData("give")]
    public void MissingSocialProfileFailsHonestlyAndPreservesItems(string action)
    {
        var (world, resolver, actor, target) = Harness(missingHelp: true);
        actor.TryAddCarried(ItemIds.Plant, 3);
        var before = WorldSnapshotSerializer.Serialize(world, 0);
        Assert.False(resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, action, Item(ItemIds.Plant, 1))).Succeeded);
        Assert.Equal(before, WorldSnapshotSerializer.Serialize(world, 0));
    }

    [Fact]
    public void GiveHonorsCapacityAndConservesAllItems()
    {
        var (world, resolver, actor, target) = Harness(capacity: 3);
        actor.TryAddCarried(ItemIds.Wood, 3); target.TryAddCarried(ItemIds.Food, 2);
        var before = WorldSnapshotSerializer.Serialize(world, 0);
        Assert.False(resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, "give", Item(ItemIds.Wood, 2))).Succeeded);
        Assert.Equal(before, WorldSnapshotSerializer.Serialize(world, 0));
        Assert.True(resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, "give", Item(ItemIds.Wood, 1))).Succeeded);
        Assert.Equal(2, actor.Inventory[ItemIds.Wood]); Assert.Equal(1, target.Inventory[ItemIds.Wood]);
        Assert.Equal(2, target.Inventory[ItemIds.Food]);
        Assert.True(world.TryGetRelationship(target.Id, actor.Id, out var bond)); Assert.Equal(8, bond!.Trust);
        Assert.Equal(ResourceMutationResult.InvalidAmount, ResourceTransferService.TryTransfer(actor, target, ItemIds.Wood, 0));
        Assert.Equal(ResourceMutationResult.InvalidDestination, ResourceTransferService.TryTransfer(actor, actor, ItemIds.Wood, 1));
        Assert.Equal(ResourceMutationResult.InsufficientQuantity, ResourceTransferService.TryTransfer(actor, target, ItemIds.Wood, 4));
        Assert.Equal(ResourceMutationResult.UnknownItem, ResourceTransferService.TryTransfer(actor, target, "unknown", 1));
        Assert.Equal(2, actor.Inventory[ItemIds.Wood]);
    }

    [Fact]
    public void SessionAndLoadedWorldConfigureSpeciesForFeed()
    {
        var (world, _, _, _) = Harness();
        var catalog = world.SpeciesCatalog!;
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Interaction test"));
        Assert.Same(catalog, session.World.SpeciesCatalog);
        var loaded = new World();
        WorldSnapshotSerializer.Load(loaded, WorldSnapshotSerializer.Serialize(session.World, 0), catalog);
        Assert.Same(catalog, loaded.SpeciesCatalog);
        var actor = loaded.Entities.Single(entity => entity.Id == loaded.PlayerEntityId);
        var target = loaded.Entities.Single(entity => entity.SpeciesId == "cow");
        loaded.TrySetEntityPosition(target.Id, actor.Position + Vector3.UnitX);
        actor.TryAddCarried(ItemIds.Plant, 2);
        var root = Path.Combine(AppContext.BaseDirectory, "data");
        var resolver = new InteractionResolver(loaded, TradeGoodsCatalog.LoadFromFile(Path.Combine(root, "trade_goods.json")),
            new(loaded, RelationshipEventCatalog.LoadFromFile(Path.Combine(root, "relationship_events.json"))));
        Assert.True(resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, "feed", Item(ItemIds.Plant, 2))).Succeeded);
        Assert.Equal(1, actor.Inventory[ItemIds.Plant]);
    }

    [Fact]
    public void MissingSpeciesAndIncompatibleSocialWorldFailWithoutMutation()
    {
        var (world, resolver, actor, target) = Harness();
        actor.TryAddCarried(ItemIds.Plant, 1);
        world.SpeciesCatalog = null;
        Assert.False(resolver.Resolve(new(actor.Id, target.Id, InteractionType.Physical, "feed", Item(ItemIds.Plant, 1))).Succeeded);
        Assert.Equal(1, actor.Inventory[ItemIds.Plant]);
        Assert.Equal(60, target.Satiety);
        var root = Path.Combine(AppContext.BaseDirectory, "data");
        Assert.Throws<ArgumentException>(() => new InteractionResolver(world,
            TradeGoodsCatalog.LoadFromFile(Path.Combine(root, "trade_goods.json")),
            new(new World(), RelationshipEventCatalog.LoadFromFile(Path.Combine(root, "relationship_events.json")))));
    }
}
