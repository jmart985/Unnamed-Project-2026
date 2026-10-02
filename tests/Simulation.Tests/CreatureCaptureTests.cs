using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class CreatureCaptureTests
{
    [Fact]
    public void CapturesLiveCreatureOnlyOnce()
    {
        var world = new World();
        var creature = world.SpawnEntity("creature", Vector3.One);

        Assert.Equal(CreatureCaptureResult.Captured, world.TryCaptureCreature(creature.Id));
        Assert.Equal(CreatureCaptivityState.Captive, creature.CaptivityState);
        Assert.Equal(CreatureCaptureResult.AlreadyCaptive, world.TryCaptureCreature(creature.Id));
        Assert.Equal(CreatureCaptivityState.Captive, creature.CaptivityState);
    }

    [Fact]
    public void RejectsMissingHumanDeadAndRemovedTargetsWithoutChangingState()
    {
        var world = new World();
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
}
