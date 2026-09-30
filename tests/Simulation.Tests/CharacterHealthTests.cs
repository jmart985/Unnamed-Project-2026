using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class CharacterHealthTests
{
    [Fact]
    public void DamageIsClampedAndRequiredPartCanIncapacitate()
    {
        var health = CharacterHealth.CreateHumanoid(100);
        Assert.True(health.TryDamage("Head", 1000));
        Assert.Equal(0, health.Parts.Single(part => part.Name == "Head").CurrentHealth);
        Assert.True(health.IsIncapacitated);
    }

    [Fact]
    public void HealingIsClampedToMaximum()
    {
        var health = CharacterHealth.CreateHumanoid(100);
        Assert.True(health.TryDamage("Torso", 10));
        Assert.True(health.TryHeal("Torso", 1000));
        var torso = health.Parts.Single(part => part.Name == "Torso");
        Assert.Equal(torso.MaximumHealth, torso.CurrentHealth);
        Assert.False(health.IsIncapacitated);
    }

    [Fact]
    public void UnknownPartsAreRejectedWithoutMutation()
    {
        var health = CharacterHealth.CreateHumanoid(100);
        Assert.False(health.TryDamage("Tail", 10));
        Assert.False(health.TryHeal("Tail", 10));
        Assert.All(health.Parts, part => Assert.Equal(part.MaximumHealth, part.CurrentHealth));
    }
}
