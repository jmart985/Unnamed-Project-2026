using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class MeleeAttackCooldownTests
{
    [Fact]
    public void CooldownAcceptsFirstAttackAndBlocksUntilElapsed()
    {
        var cooldown = new MeleeAttackCooldown(2);
        Assert.True(cooldown.TryStart()); Assert.False(cooldown.TryStart());
        cooldown.Advance(1); Assert.False(cooldown.IsReady);
        cooldown.Advance(1); Assert.True(cooldown.IsReady); Assert.True(cooldown.TryStart());
    }

    [Fact]
    public void InvalidDurationOrAdvanceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeAttackCooldown(0));
        var cooldown = new MeleeAttackCooldown(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => cooldown.Advance(-1));
    }
}
