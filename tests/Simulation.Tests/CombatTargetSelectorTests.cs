using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class CombatTargetSelectorTests
{
    [Fact]
    public void ExplicitTargetIsPreservedAndMissingTargetDefaultsToTorso()
    {
        var health = CharacterHealth.CreateHumanoid(100);
        Assert.Equal("Head", CombatTargetSelector.Select(health, "Head").BodyPart);
        Assert.Equal("Torso", CombatTargetSelector.Select(health, null).BodyPart);
    }

    [Fact]
    public void InvalidTargetIsRejected()
    {
        var health = CharacterHealth.CreateHumanoid(100);
        Assert.Equal(CombatTargetResult.InvalidTarget, CombatTargetSelector.Select(health, "Tail").Result);
    }
}
