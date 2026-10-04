using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class SocialFeedbackTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void DomesticBaseTrustMustBeWithinRelationshipBounds(double invalid)
    {
        var json = File.ReadAllText(Data("species.json")).Replace("\"baseTrust\": 50", $"\"baseTrust\": {invalid}");
        Assert.Throws<InvalidDataException>(() => SpeciesCatalog.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))));
    }

    private static string Data(string name) => Path.Combine(AppContext.BaseDirectory, "data", name);
    private static (World World, EntityRecord Actor, EntityRecord Cow, InteractionResolver Resolver) Harness()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Data("species.json"));
        var world = new World { SpeciesCatalog = catalog };
        var actor = world.SpawnEntity(catalog.GetRequired("human"), Vector3.Zero);
        var cow = world.SpawnEntity(catalog.GetRequired("cow"), Vector3.UnitX);
        var resolver = new InteractionResolver(world, TradeGoodsCatalog.LoadFromFile(Data("trade_goods.json")),
            new(world, RelationshipEventCatalog.LoadFromFile(Data("relationship_events.json"))));
        return (world, actor, cow, resolver);
    }

    [Fact]
    public void DomesticDefaultsAreDirectedReadOnlyAndExcludePredators()
    {
        var (world, actor, cow, _) = Harness();
        var ape = world.SpawnEntity("ape", Vector3.Zero);
        var wolf = world.SpawnEntity("wolf", Vector3.Zero);
        Assert.Equal(RelationshipBondLevel.Trusted, world.GetBondLevel(cow.Id, actor.Id));
        Assert.Equal(RelationshipBondLevel.Trusted, world.GetBondLevel(cow.Id, ape.Id));
        Assert.Equal(RelationshipBondLevel.Stranger, world.GetBondLevel(cow.Id, wolf.Id));
        Assert.Equal(RelationshipBondLevel.Stranger, world.GetBondLevel(actor.Id, cow.Id));
        Assert.Equal(RelationshipBondLevel.Stranger, world.GetBondLevel(cow.Id, cow.Id));
        Assert.Empty(world.Relationships);
    }

    [Fact]
    public void PetPermissionCrossesTrustedBoundaryInRecipientDirectionAndFearOverridesIt()
    {
        var (world, actor, cow, resolver) = Harness();
        world.ApplyRelationshipAdjustment(cow.Id, actor.Id, new(0, -1, 0, 0));
        var before = WorldSnapshotSerializer.Serialize(world, 0);
        Assert.False(resolver.Resolve(new(actor.Id, cow.Id, InteractionType.Physical, "pet")).Succeeded);
        Assert.Equal(before, WorldSnapshotSerializer.Serialize(world, 0));
        actor.TryAddCarried(ItemIds.Plant, 2);
        var food = new ResourceContainer("hand", 2); food.TryAdd(ItemIds.Plant, 2);
        Assert.True(resolver.Resolve(new(actor.Id, cow.Id, InteractionType.Physical, "feed", food)).Succeeded);
        Assert.Equal(1, actor.Inventory[ItemIds.Plant]);
        Assert.Equal(RelationshipBondLevel.Trusted, world.GetBondLevel(cow.Id, actor.Id));
        Assert.True(resolver.Resolve(new(actor.Id, cow.Id, InteractionType.Physical, "pet")).Succeeded);
        world.ApplyRelationshipAdjustment(cow.Id, actor.Id, new(0, -2, 0, 0));
        Assert.False(resolver.Resolve(new(actor.Id, cow.Id, InteractionType.Physical, "pet")).Succeeded);
        world.ApplyRelationshipAdjustment(cow.Id, actor.Id, new(0, 1, 50, 0));
        Assert.Equal(RelationshipBondLevel.Fearful, world.GetBondLevel(cow.Id, actor.Id));
        Assert.False(resolver.Resolve(new(actor.Id, cow.Id, InteractionType.Physical, "pet")).Succeeded);
        Assert.False(world.TryGetRelationship(actor.Id, cow.Id, out _));
        Assert.False(resolver.Resolve(new(cow.Id, actor.Id, InteractionType.Physical, "capture")).Succeeded);
    }

    [Fact]
    public void WildCreatureCannotBePettedBeforeTrustButTrustedPredatorCan()
    {
        var (world, actor, _, resolver) = Harness();
        var wolf = world.SpawnEntity("wolf", Vector3.UnitZ);
        Assert.False(resolver.Resolve(new(actor.Id, wolf.Id, InteractionType.Physical, "pet")).Succeeded);
        Assert.Empty(world.Relationships);
        world.ApplyRelationshipAdjustment(wolf.Id, actor.Id, new(20, 50, 0, 0));
        Assert.True(resolver.Resolve(new(actor.Id, wolf.Id, InteractionType.Physical, "pet")).Succeeded);
    }

    [Fact]
    public void NotificationsContainAppliedClampedDeltasAndRejectsProduceNone()
    {
        var (world, actor, cow, resolver) = Harness();
        var notices = new List<(string Observer, string Target, RelationshipAdjustment Change)>();
        world.RelationshipChanged += (observer, target, change) => notices.Add((observer, target, change));
        Assert.True(resolver.Resolve(new(actor.Id, cow.Id, InteractionType.Physical, "pet")).Succeeded);
        Assert.Equal((cow.Id, actor.Id, new RelationshipAdjustment(1, 1, 0, 1)), Assert.Single(notices));
        world.ApplyRelationshipAdjustment(cow.Id, actor.Id, new(100, 100, 0, 100));
        notices.Clear();
        world.ApplyRelationshipAdjustment(cow.Id, actor.Id, new(1, 1, -1, 1));
        world.ApplyRelationshipAdjustment(cow.Id, "missing", new(1, 1, 1, 1));
        world.ApplyRelationshipAdjustment(cow.Id, actor.Id, new(double.NaN, 1, 1, 1));
        Assert.Empty(notices);
        world.ApplyRelationshipAdjustment(cow.Id, actor.Id, new(-1, -1, 1, -1));
        Assert.Equal(new RelationshipAdjustment(-1, -1, 1, -1), Assert.Single(notices).Change);
    }

    [Fact]
    public void NpcSocialEventsUseUnitStepsAndRecipientDirection()
    {
        var (world, actor, _, resolver) = Harness();
        var npc = world.SpawnEntity("human", Vector3.UnitZ);
        var changes = new List<RelationshipAdjustment>();
        world.RelationshipChanged += (observer, target, delta) =>
        {
            Assert.Equal(npc.Id, observer); Assert.Equal(actor.Id, target); changes.Add(delta);
        };
        Assert.True(resolver.Resolve(new(actor.Id, npc.Id, InteractionType.Social, "greet")).Succeeded);
        Assert.Equal(new(1, 0, 0, 0), changes[0]);
        Assert.True(resolver.Resolve(new(actor.Id, npc.Id, InteractionType.Social, "help")).Succeeded);
        Assert.Equal(new(1, 1, 0, 1), changes[1]);
        Assert.True(resolver.Resolve(new(actor.Id, npc.Id, InteractionType.Social, "threaten")).Succeeded);
        Assert.Equal(new(1, -1, 1, -1), changes[2]);
        Assert.False(world.TryGetRelationship(actor.Id, npc.Id, out _));
        var formatted = EntityInspectionReadModel.FormatOutgoingRelationships(EntityInspectionReadModel.Create(world, npc), false);
        Assert.Contains("Stranger", formatted);
        Assert.DoesNotContain("Trust", formatted);
        Assert.DoesNotContain("Familiarity", formatted);
    }

    [Fact]
    public void FactionUnitEventsNotifyOnlyAppliedChangesIncludingBounds()
    {
        var (world, actor, _, _) = Harness();
        world.DefineFaction("settlers");
        var changes = new List<double>();
        world.FactionStandingChanged += (id, faction, delta) =>
        { Assert.Equal(actor.Id, id); Assert.Equal("settlers", faction); changes.Add(delta); };
        Assert.Equal(FactionReputationResult.OutOfRange, world.ApplyFactionStandingEvent(actor.Id, "settlers", false));
        Assert.Equal(FactionReputationResult.Applied, world.ApplyFactionStandingEvent(actor.Id, "settlers", true));
        Assert.Equal(FactionReputationResult.Applied, world.ApplyFactionStandingEvent(actor.Id, "settlers", false));
        Assert.Equal(new double[] { 1, -1 }, changes);
        world.AdjustFactionReputation(actor.Id, "settlers", 100); changes.Clear();
        Assert.Equal(FactionReputationResult.OutOfRange, world.ApplyFactionStandingEvent(actor.Id, "settlers", true));
        Assert.Equal(FactionReputationResult.UnknownFaction, world.ApplyFactionStandingEvent(actor.Id, "unknown", true));
        Assert.Empty(changes);
    }

    [Theory]
    [InlineData(-1, 49)]
    [InlineData(-50, 0)]
    public void SavingAndLoadingDoesNotReseedBetrayedDomesticBondsOrEmitFeedback(double loss, double expectedTrust)
    {
        var (world, actor, cow, _) = Harness();
        world.ApplyRelationshipAdjustment(cow.Id, actor.Id, new(0, loss, 0, 0));
        var saved = WorldSnapshotSerializer.Serialize(world, 0);
        var notifications = 0;
        world.RelationshipChanged += (_, _, _) => notifications++;
        world.FactionStandingChanged += (_, _, _) => notifications++;
        WorldSnapshotSerializer.Load(world, saved, world.SpeciesCatalog!);
        Assert.Equal(0, notifications);
        Assert.True(world.TryGetRelationship(cow.Id, actor.Id, out var bond));
        Assert.Equal(expectedTrust, bond!.Trust);
        Assert.Equal(20, bond.Familiarity);
        Assert.Equal(RelationshipBondLevel.Familiar, world.GetBondLevel(cow.Id, actor.Id));
        Assert.Equal(saved, WorldSnapshotSerializer.Serialize(world, 0));
    }
}
