using System.Text;
using Simulation;

namespace Simulation.Tests;

public sealed class RelationshipEventCatalogTests
{
    [Fact]
    public void LoadsProvisionalSocialEventProfiles()
    {
        var catalog = RelationshipEventCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "relationship_events.json"));

        Assert.Equal(
            new[] { "greeted", "helped", "rescued", "harmed", "threatened" },
            catalog.Definitions.Select(definition => definition.Id));
        Assert.True(catalog.TryGet("greeted", out var greeted));
        Assert.Equal(new RelationshipAdjustment(5, 0, 0, 0), greeted!.Adjustment);
        Assert.True(catalog.TryGet("rescued", out var rescued));
        Assert.Equal(20, rescued!.Adjustment.TrustDelta);
        Assert.Equal(-15, rescued.Adjustment.FearDelta);
    }

    [Fact]
    public void AppliesKnownEventToOnlyTheDirectedPair()
    {
        var world = new World();
        var observer = world.SpawnEntity("human", System.Numerics.Vector3.Zero);
        var target = world.SpawnEntity("human", System.Numerics.Vector3.One);
        var service = new RelationshipEventService(world, GetCatalog());

        Assert.Equal(
            RelationshipEventApplyResult.Applied,
            service.Apply(observer.Id, target.Id, "helped"));
        Assert.True(world.TryGetRelationship(observer.Id, target.Id, out var relationship));
        Assert.Equal(5, relationship!.Familiarity);
        Assert.Equal(8, relationship.Trust);
        Assert.Equal(0, relationship.Fear);
        Assert.Equal(3, relationship.Loyalty);
        Assert.False(world.TryGetRelationship(target.Id, observer.Id, out _));
    }

    [Fact]
    public void UnknownEventsAndInvalidParticipantsDoNotMutateRelationships()
    {
        var world = new World();
        var observer = world.SpawnEntity("human", System.Numerics.Vector3.Zero);
        var target = world.SpawnEntity("human", System.Numerics.Vector3.One);
        var service = new RelationshipEventService(world, GetCatalog());

        Assert.Equal(
            RelationshipEventApplyResult.UnknownEvent,
            service.Apply(observer.Id, target.Id, "unknown"));
        Assert.Equal(
            RelationshipEventApplyResult.InvalidParticipant,
            service.Apply(observer.Id, "missing", "helped"));
        Assert.Empty(world.Relationships);
    }

    [Fact]
    public void EventEffectsClampAtRelationshipBounds()
    {
        var world = new World();
        var observer = world.SpawnEntity("human", System.Numerics.Vector3.Zero);
        var target = world.SpawnEntity("human", System.Numerics.Vector3.One);
        var service = new RelationshipEventService(world, GetCatalog());
        Assert.Equal(
            RelationshipEventApplyResult.Applied,
            service.Apply(observer.Id, target.Id, "rescued"));
        Assert.Equal(
            RelationshipEventApplyResult.Applied,
            service.Apply(observer.Id, target.Id, "harmed"));

        Assert.True(world.TryGetRelationship(observer.Id, target.Id, out var relationship));
        Assert.Equal(15, relationship!.Familiarity);
        Assert.Equal(5, relationship.Trust);
        Assert.Equal(18, relationship.Fear);
        Assert.Equal(0, relationship.Loyalty);
    }

    [Theory]
    [InlineData("""[{"id":"helped","familiarityDelta":0,"trustDelta":0,"fearDelta":0,"loyaltyDelta":0},{"id":"helped","familiarityDelta":0,"trustDelta":0,"fearDelta":0,"loyaltyDelta":0}]""", "defined more than once")]
    [InlineData("""[{"id":"-invalid","familiarityDelta":0,"trustDelta":0,"fearDelta":0,"loyaltyDelta":0}]""", "valid lowercase event ID")]
    [InlineData("""[{"id":"missing-delta","familiarityDelta":0,"trustDelta":0,"fearDelta":0}]""", "must define all four")]
    public void RejectsInvalidEventData(string json, string expectedMessage)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Load(json));
        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void RejectsNonFiniteEventDeltas()
    {
        Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"non-finite","familiarityDelta":1e999,"trustDelta":0,"fearDelta":0,"loyaltyDelta":0}]"""));
    }

    private static RelationshipEventCatalog GetCatalog() =>
        RelationshipEventCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "relationship_events.json"));

    private static RelationshipEventCatalog Load(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return RelationshipEventCatalog.Load(stream);
    }
}
