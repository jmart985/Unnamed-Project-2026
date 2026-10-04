using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class CreatureGreetingServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IncapacitatedParticipantsCannotAcknowledgeOrChangeRelationships(bool incapacitatePlayer)
    {
        var (world, player, creature, greeting) = CreateGreetingTarget("cow", new Vector3(1, 0, 0));
        (incapacitatePlayer ? player : creature).Health.TryDamage("Torso", 1000);
        Assert.Equal(CreatureGreetingResultCode.ParticipantUnavailable, greeting.Greet(creature.Id).Code);
        Assert.False(world.TryGetRelationship(player.Id, creature.Id, out _));
    }
    [Fact]
    public void GreetingAppliesTheProfileOnlyToTheCreatureTowardPlayerRelationship()
    {
        var (world, player, creature, greeting) = CreateGreetingTarget("ape", new Vector3(2, 0, 0));

        var result = greeting.Greet(creature.Id);

        Assert.Equal(CreatureGreetingResultCode.Greeted, result.Code);
        Assert.True(world.TryGetRelationship(creature.Id, player.Id, out var relationship));
        Assert.Equal(1, relationship!.Familiarity);
        Assert.Equal(0, relationship.Trust);
        Assert.Equal(0, relationship.Fear);
        Assert.Equal(0, relationship.Loyalty);
        Assert.False(world.TryGetRelationship(player.Id, creature.Id, out _));
    }

    [Fact]
    public void PlayerInteractCommandRunsTheGreetingAndReturnsVisibleResult()
    {
        var (world, player, creature, greeting) = CreateGreetingTarget("creature", new Vector3(1, 0, 0));
        var commands = new PlayerCommandBoundary(world, new SimulationClock(), greeting);

        var result = commands.Execute(new PlayerCommand(PlayerCommandKind.Interact, creature.Id));

        Assert.Equal(PlayerCommandResultCode.Accepted, result.Code);
        Assert.Equal("Greeted Creature.", result.Reason);

        var unsupported = world.SpawnEntity("wolf", player.Position + new Vector3(1, 0, 0));
        result = commands.Execute(new PlayerCommand(PlayerCommandKind.Interact, unsupported.Id));

        Assert.Equal(PlayerCommandResultCode.UnsupportedInteraction, result.Code);
        Assert.Equal("This species does not accept friendly interaction.", result.Reason);
        Assert.False(world.TryGetRelationship(player.Id, unsupported.Id, out _));
    }

    [Fact]
    public void UnsupportedOutOfRangeAndInvalidTargetsDoNotMutateRelationships()
    {
        var (world, player, _, greeting) = CreateGreetingTarget("ape", new Vector3(1, 0, 0));
        var unsupported = world.SpawnEntity("wolf", player.Position + new Vector3(1, 0, 0));
        var distant = world.SpawnEntity("ape", player.Position + new Vector3(3, 0, 0));
        var removed = world.SpawnEntity("ape", player.Position + new Vector3(1, 0, 0));
        Assert.True(world.TryRemoveEntity(removed.Id));

        Assert.Equal(CreatureGreetingResultCode.UnsupportedSpecies, greeting.Greet(unsupported.Id).Code);
        Assert.Equal(CreatureGreetingResultCode.OutOfRange, greeting.Greet(distant.Id).Code);
        Assert.Equal(CreatureGreetingResultCode.TargetNotFound, greeting.Greet(removed.Id).Code);
        Assert.Equal(CreatureGreetingResultCode.TargetNotFound, greeting.Greet("missing").Code);
        Assert.Equal(CreatureGreetingResultCode.TargetNotFound, greeting.Greet(null).Code);
        Assert.Equal(CreatureGreetingResultCode.SelfTarget, greeting.Greet(player.Id).Code);
        Assert.Empty(world.Relationships);
    }

    [Fact]
    public void MissingGreetedProfileRejectsWithoutMutatingRelationships()
    {
        var (world, _, creature, _) = CreateGreetingTarget("creature", new Vector3(1, 0, 0));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            """[{"id":"helped","familiarityDelta":5,"trustDelta":0,"fearDelta":0,"loyaltyDelta":0}]"""));
        var catalog = RelationshipEventCatalog.Load(stream);
        var greeting = new CreatureGreetingService(world, GetSpeciesCatalog(), catalog);

        Assert.Equal(
            CreatureGreetingResultCode.RelationshipEventUnavailable,
            greeting.Greet(creature.Id).Code);
        Assert.Empty(world.Relationships);
    }

    private static SpeciesCatalog GetSpeciesCatalog() =>
        SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));

    private static (World World, EntityRecord Player, EntityRecord Target, CreatureGreetingService Service)
        CreateGreetingTarget(string targetSpeciesId, Vector3 targetOffset)
    {
        var species = GetSpeciesCatalog();
        var session = NewGameSession.Create(species, PlayerCharacterProfile.Create("Test Player"), seed: 2026);
        var target = session.World.SpawnEntity(targetSpeciesId, session.Player.Position + targetOffset);
        var events = RelationshipEventCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "relationship_events.json"));
        var service = new CreatureGreetingService(session.World, species, events);
        return (session.World, session.Player, target, service);
    }
}
