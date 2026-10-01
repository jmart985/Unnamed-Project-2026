using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class SocialMeleeCombatServiceTests
{
    [Fact]
    public void SuccessfulHitAppliesHarmedEventFromDefenderTowardAttacker()
    {
        var world = new World();
        var attacker = world.SpawnEntity("human", Vector3.Zero);
        var defender = world.SpawnEntity("human", Vector3.One);
        Assert.True(world.TryGetOrCreateRelationship(defender.Id, attacker.Id, out var initialRelationship));
        initialRelationship!.ApplyDelta(RelationshipDimension.Familiarity, 5);
        initialRelationship.ApplyDelta(RelationshipDimension.Trust, 30);
        initialRelationship.ApplyDelta(RelationshipDimension.Loyalty, 25);
        var service = new SocialMeleeCombatService(world, GetCatalog());

        var result = service.Resolve(
            new MeleeAttackRequest(attacker.Id, defender.Id, "Torso", 10),
            maximumReach: 2);

        Assert.Equal(MeleeAttackResult.Success, result.Combat.Result);
        Assert.Equal(RelationshipEventApplyResult.Applied, result.RelationshipEvent);
        Assert.Equal(40, defender.Health.Parts.Single(part => part.Name == "Torso").CurrentHealth);
        Assert.True(world.TryGetRelationship(defender.Id, attacker.Id, out var defenderView));
        Assert.Equal(8, defenderView!.Familiarity);
        Assert.Equal(15, defenderView.Trust);
        Assert.Equal(18, defenderView.Fear);
        Assert.Equal(13, defenderView.Loyalty);
        Assert.False(world.TryGetRelationship(attacker.Id, defender.Id, out _));
    }

    [Theory]
    [InlineData("invalid-damage")]
    [InlineData("missing-entity")]
    [InlineData("unknown-body-part")]
    [InlineData("out-of-range")]
    public void RejectedAttackDoesNotCreateRelationship(string rejection)
    {
        var world = new World();
        var attacker = world.SpawnEntity("human", Vector3.Zero);
        var defender = world.SpawnEntity(
            "human",
            rejection == "out-of-range" ? new Vector3(10, 0, 0) : Vector3.One);
        var service = new SocialMeleeCombatService(world, GetCatalog());
        var request = rejection switch
        {
            "invalid-damage" => new MeleeAttackRequest(attacker.Id, defender.Id, "Head", 0),
            "missing-entity" => new MeleeAttackRequest(attacker.Id, "missing", "Head", 10),
            "unknown-body-part" => new MeleeAttackRequest(attacker.Id, defender.Id, "Tail", 10),
            "out-of-range" => new MeleeAttackRequest(attacker.Id, defender.Id, "Head", 10),
            _ => throw new ArgumentOutOfRangeException(nameof(rejection))
        };

        var result = service.Resolve(request, maximumReach: 2);

        Assert.NotEqual(MeleeAttackResult.Success, result.Combat.Result);
        Assert.Null(result.RelationshipEvent);
        Assert.Empty(world.Relationships);
        Assert.Equal(25, defender.Health.Parts.Single(part => part.Name == "Head").CurrentHealth);
    }

    [Fact]
    public void ConstructorRequiresHarmedEventProfile()
    {
        var world = new World();
        var catalog = LoadCatalog(
            """[{"id":"helped","familiarityDelta":1,"trustDelta":1,"fearDelta":0,"loyaltyDelta":0}]""");

        Assert.Throws<ArgumentException>(() => new SocialMeleeCombatService(world, catalog));
    }

    private static RelationshipEventCatalog GetCatalog() =>
        RelationshipEventCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "relationship_events.json"));

    private static RelationshipEventCatalog LoadCatalog(string json)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return RelationshipEventCatalog.Load(stream);
    }
}
