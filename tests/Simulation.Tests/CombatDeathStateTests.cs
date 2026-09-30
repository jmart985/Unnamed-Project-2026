using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class CombatDeathStateTests
{
    [Fact]
    public void DestroyedHeadHasShortPendingDeathWindow()
    {
        var health = CharacterHealth.CreateHumanoid(100);
        Assert.True(health.TryDamage("Head", 100));
        Assert.True(health.IsIncapacitated); Assert.False(health.IsDead);
        health.AdvanceTime(0.5); Assert.False(health.IsDead);
        health.AdvanceTime(0.5); Assert.True(health.IsDead);
    }

    [Fact]
    public void HeadCanBeSavedBeforeDelayExpires()
    {
        var health = CharacterHealth.CreateHumanoid(100);
        health.TryDamage("Head", 100); health.AdvanceTime(0.5);
        Assert.True(health.TryHeal("Head", 100));
        health.AdvanceTime(2); Assert.False(health.IsDead);
    }
}
