using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class CreatureCaptureTests
{
    [Fact]
    public void CreatureLifecycleSupportsCaptureCaptivityDomesticationAndRelease()
    {
        var world = CreateWorld();
        var creature = world.SpawnEntity("creature", Vector3.One);

        Assert.Equal(CreatureCaptureResult.Captured, world.TryCaptureCreature(creature.Id));
        Assert.Equal(CreatureCaptivityState.Captured, creature.CaptivityState);
        Assert.Equal(CreatureCaptureResult.AlreadyCaptured, world.TryCaptureCreature(creature.Id));
        Assert.Equal(
            CreatureLifecycleTransitionResult.Transitioned,
            world.TryTransitionCreatureState(creature.Id, CreatureCaptivityState.Captive));
        Assert.Equal(CreatureCaptivityState.Captive, creature.CaptivityState);
        Assert.Equal(CreatureCaptureResult.AlreadyCaptive, world.TryCaptureCreature(creature.Id));
        Assert.Equal(
            CreatureLifecycleTransitionResult.Transitioned,
            world.TryTransitionCreatureState(creature.Id, CreatureCaptivityState.Domesticated));
        Assert.Equal(CreatureCaptivityState.Domesticated, creature.CaptivityState);
        Assert.Equal(
            CreatureLifecycleTransitionResult.Transitioned,
            world.TryTransitionCreatureState(creature.Id, CreatureCaptivityState.Wild));
        Assert.Equal(CreatureCaptivityState.Wild, creature.CaptivityState);
        Assert.Equal(CreatureCaptureResult.Captured, world.TryCaptureCreature(creature.Id));
    }

    [Theory]
    [InlineData(CreatureCaptivityState.Wild, CreatureCaptivityState.Captive)]
    [InlineData(CreatureCaptivityState.Wild, CreatureCaptivityState.Domesticated)]
    [InlineData(CreatureCaptivityState.Captured, CreatureCaptivityState.Domesticated)]
    [InlineData(CreatureCaptivityState.Captive, CreatureCaptivityState.Captured)]
    [InlineData(CreatureCaptivityState.Domesticated, CreatureCaptivityState.Captive)]
    [InlineData(CreatureCaptivityState.Domesticated, CreatureCaptivityState.Captured)]
    public void RejectsInvalidLifecycleTransitionsWithoutMutation(
        CreatureCaptivityState initialState,
        CreatureCaptivityState targetState)
    {
        var world = CreateWorld();
        var creature = world.SpawnEntity("creature", Vector3.One);
        SetLifecycleState(world, creature.Id, initialState);

        Assert.Equal(
            CreatureLifecycleTransitionResult.InvalidTransition,
            world.TryTransitionCreatureState(creature.Id, targetState));
        Assert.Equal(initialState, creature.CaptivityState);
    }

    [Theory]
    [InlineData(CreatureCaptivityState.Wild)]
    [InlineData(CreatureCaptivityState.Captured)]
    [InlineData(CreatureCaptivityState.Captive)]
    [InlineData(CreatureCaptivityState.Domesticated)]
    public void DeathIsTerminalForLifecycleTransitions(CreatureCaptivityState initialState)
    {
        var world = CreateWorld();
        var creature = world.SpawnEntity("creature", Vector3.One);
        SetLifecycleState(world, creature.Id, initialState);
        Assert.True(creature.Health.TryDamage("Torso", creature.BaseStats.MaxHealth));

        Assert.Equal(
            CreatureLifecycleTransitionResult.TargetDead,
            world.TryTransitionCreatureState(creature.Id, CreatureCaptivityState.Wild));
        Assert.Equal(initialState, creature.CaptivityState);
    }

    [Theory]
    [InlineData(CreatureCaptivityState.Captured)]
    [InlineData(CreatureCaptivityState.Captive)]
    [InlineData(CreatureCaptivityState.Domesticated)]
    public void ReleaseReturnsCreatureToWild(CreatureCaptivityState initialState)
    {
        var world = CreateWorld();
        var creature = world.SpawnEntity("creature", Vector3.One);
        SetLifecycleState(world, creature.Id, initialState);

        Assert.Equal(
            CreatureLifecycleTransitionResult.Transitioned,
            world.TryTransitionCreatureState(creature.Id, CreatureCaptivityState.Wild));
        Assert.Equal(CreatureCaptivityState.Wild, creature.CaptivityState);
    }

    [Fact]
    public void RejectsRepeatedStateAndNonCreatureTransitions()
    {
        var world = CreateWorld();
        var human = world.SpawnEntity("human", Vector3.Zero);
        var creature = world.SpawnEntity("creature", Vector3.One);

        Assert.Equal(
            CreatureLifecycleTransitionResult.AlreadyInState,
            world.TryTransitionCreatureState(creature.Id, CreatureCaptivityState.Wild));
        Assert.Equal(
            CreatureLifecycleTransitionResult.NotCreature,
            world.TryTransitionCreatureState(human.Id, CreatureCaptivityState.Domesticated));
        Assert.Equal(CreatureCaptivityState.Wild, human.CaptivityState);
    }

    [Fact]
    public void RejectsMissingHumanDeadAndRemovedTargetsWithoutChangingState()
    {
        var world = CreateWorld();
        var human = world.SpawnEntity("human", Vector3.Zero);
        var deadCreature = world.SpawnEntity("creature", Vector3.One);
        deadCreature.Health.TryDamage("Torso", deadCreature.BaseStats.MaxHealth);
        var removedCreature = world.SpawnEntity("creature", Vector3.UnitX * 2);
        Assert.True(world.TryRemoveEntity(removedCreature.Id));

        Assert.Equal(CreatureCaptureResult.TargetNotFound, world.TryCaptureCreature("missing"));
        Assert.Equal(CreatureCaptureResult.NotCreature, world.TryCaptureCreature(human.Id));
        Assert.Equal(CreatureCaptureResult.TargetDead, world.TryCaptureCreature(deadCreature.Id));
        Assert.Equal(CreatureCaptureResult.TargetNotFound, world.TryCaptureCreature(removedCreature.Id));
        Assert.Equal(CreatureCaptivityState.Wild, human.CaptivityState);
        Assert.Equal(CreatureCaptivityState.Wild, deadCreature.CaptivityState);
    }

    private static void SetLifecycleState(
        World world,
        string entityId,
        CreatureCaptivityState state)
    {
        if (state is CreatureCaptivityState.Captured
            or CreatureCaptivityState.Captive
            or CreatureCaptivityState.Domesticated)
        {
            Assert.Equal(CreatureCaptureResult.Captured, world.TryCaptureCreature(entityId));
        }

        if (state is CreatureCaptivityState.Captive or CreatureCaptivityState.Domesticated)
        {
            Assert.Equal(
                CreatureLifecycleTransitionResult.Transitioned,
                world.TryTransitionCreatureState(entityId, CreatureCaptivityState.Captive));
        }

        if (state == CreatureCaptivityState.Domesticated)
        {
            Assert.Equal(
                CreatureLifecycleTransitionResult.Transitioned,
                world.TryTransitionCreatureState(entityId, CreatureCaptivityState.Domesticated));
        }
    }

    private static World CreateWorld() => new()
    {
        SpeciesCatalog = SpeciesCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "species.json"))
    };
}
