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
        Assert.Equal(6, defenderView!.Familiarity);
        Assert.Equal(29, defenderView.Trust);
        Assert.Equal(1, defenderView.Fear);
        Assert.Equal(24, defenderView.Loyalty);
        Assert.False(world.TryGetRelationship(attacker.Id, defender.Id, out _));
    }

    [Theory]
    [InlineData("invalid-damage")]
    [InlineData("missing-entity")]
    [InlineData("unknown-body-part")]
    [InlineData("out-of-range")]
    [InlineData("dead-attacker")]
    [InlineData("dead-defender")]
    [InlineData("destroyed-body-part")]
    [InlineData("no-applied-damage")]
    public void RejectedAttackDoesNotCreateRelationship(string rejection)
    {
        var world = new World();
        var attacker = world.SpawnEntity("human", Vector3.Zero);
        var defender = world.SpawnEntity(
            "human",
            rejection == "out-of-range" ? new Vector3(10, 0, 0) : Vector3.One);
        Assert.True(world.TryGetOrCreateRelationship(defender.Id, attacker.Id, out var defenderView));
        Assert.True(world.TryGetOrCreateRelationship(attacker.Id, defender.Id, out var attackerView));
        defenderView!.ApplyDelta(RelationshipDimension.Trust, 40);
        defenderView.ApplyDelta(RelationshipDimension.Fear, 10);
        attackerView!.ApplyDelta(RelationshipDimension.Loyalty, 20);
        if (rejection == "dead-attacker")
        {
            Assert.True(attacker.Health.TryDamage("Torso", 50));
        }
        else if (rejection == "dead-defender")
        {
            Assert.True(defender.Health.TryDamage("Torso", 50));
        }
        else if (rejection == "destroyed-body-part")
        {
            Assert.True(defender.Health.TryDamage("LeftArm", 100));
        }

        var service = new SocialMeleeCombatService(world, GetCatalog());
        var request = rejection switch
        {
            "invalid-damage" => new MeleeAttackRequest(attacker.Id, defender.Id, "Head", 0),
            "missing-entity" => new MeleeAttackRequest(attacker.Id, "missing", "Head", 10),
            "unknown-body-part" => new MeleeAttackRequest(attacker.Id, defender.Id, "Tail", 10),
            "out-of-range" => new MeleeAttackRequest(attacker.Id, defender.Id, "Head", 10),
            "dead-attacker" => new MeleeAttackRequest(attacker.Id, defender.Id, "Head", 10),
            "dead-defender" => new MeleeAttackRequest(attacker.Id, defender.Id, "Head", 10),
            "destroyed-body-part" => new MeleeAttackRequest(attacker.Id, defender.Id, "LeftArm", 10),
            "no-applied-damage" => new MeleeAttackRequest(attacker.Id, defender.Id, "Head", 1e-300),
            _ => throw new ArgumentOutOfRangeException(nameof(rejection))
        };

        var result = service.Resolve(request, maximumReach: 2);

        var expectedResult = rejection switch
        {
            "invalid-damage" => MeleeAttackResult.InvalidRequest,
            "missing-entity" => MeleeAttackResult.MissingEntity,
            "unknown-body-part" => MeleeAttackResult.UnknownBodyPart,
            "out-of-range" => MeleeAttackResult.OutOfRange,
            "dead-attacker" => MeleeAttackResult.AttackerIncapacitated,
            "dead-defender" or "destroyed-body-part" => MeleeAttackResult.DefenderUnavailable,
            "no-applied-damage" => MeleeAttackResult.NoDamage,
            _ => throw new ArgumentOutOfRangeException(nameof(rejection))
        };
        Assert.Equal(expectedResult, result.Combat.Result);
        Assert.Null(result.RelationshipEvent);
        Assert.Same(defenderView, Assert.Single(world.Relationships, relationship =>
            relationship.ObserverEntityId == defender.Id));
        Assert.Same(attackerView, Assert.Single(world.Relationships, relationship =>
            relationship.ObserverEntityId == attacker.Id));
        Assert.Equal(40, defenderView.Trust);
        Assert.Equal(10, defenderView.Fear);
        Assert.Equal(20, attackerView.Loyalty);
        Assert.Equal(25, defender.Health.Parts.Single(part => part.Name == "Head").CurrentHealth);
        if (rejection == "destroyed-body-part")
        {
            Assert.Equal(0, defender.Health.Parts.Single(part => part.Name == "LeftArm").CurrentHealth);
        }
    }

    [Fact]
    public void ConstructorRequiresHarmedEventProfile()
    {
        var world = new World();
        var catalog = LoadCatalog(
            """[{"id":"helped","familiarityDelta":1,"trustDelta":1,"fearDelta":0,"loyaltyDelta":0}]""");

        Assert.Throws<ArgumentException>(() => new SocialMeleeCombatService(world, catalog));
    }

    [Fact]
    public void SuccessfulHitTriggersTheConfiguredAggressiveDefenderResponse()
    {
        var species = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var world = new World { SpeciesCatalog = species };
        var attacker = world.SpawnEntity("human", Vector3.Zero);
        var defender = world.SpawnEntity("wolf", Vector3.UnitX);
        var service = new SocialMeleeCombatService(world, GetCatalog());

        var result = service.Resolve(new(attacker.Id, defender.Id, "Torso", 10), maximumReach: 2);

        Assert.Equal(MeleeAttackResult.Success, result.Combat.Result);
        Assert.Equal("AggressivePursuit", defender.Activity);
        Assert.Equal(attacker.Id, defender.TargetId);
    }

    [Fact]
    public void RejectedHitDoesNotTriggerDefenderBehavior()
    {
        var species = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var world = new World { SpeciesCatalog = species };
        var attacker = world.SpawnEntity("human", Vector3.Zero);
        var defender = world.SpawnEntity("wolf", Vector3.UnitX);
        var service = new SocialMeleeCombatService(world, GetCatalog());

        var result = service.Resolve(new(attacker.Id, defender.Id, "Tail", 10), maximumReach: 2);

        Assert.Equal(MeleeAttackResult.UnknownBodyPart, result.Combat.Result);
        Assert.NotEqual("AggressivePursuit", defender.Activity);
        Assert.Null(defender.TargetId);
        Assert.Null(result.RelationshipEvent);
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
