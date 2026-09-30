using System.Numerics;
using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class MeleeReachValidatorTests
{
    [Fact]
    public void ReachAcceptsNearbyTargetsAndRejectsDistantTargets()
    {
        Assert.Equal(MeleeReachResult.InRange, MeleeReachValidator.Validate(Vector3.Zero, new Vector3(1,0,0), 1));
        Assert.Equal(MeleeReachResult.OutOfRange, MeleeReachValidator.Validate(Vector3.Zero, new Vector3(2,0,0), 1));
    }

    [Fact]
    public void InvalidReachAndPositionsAreRejected()
    {
        Assert.Equal(MeleeReachResult.InvalidReach, MeleeReachValidator.Validate(Vector3.Zero, Vector3.One, -1));
        Assert.Equal(MeleeReachResult.InvalidReach, MeleeReachValidator.Validate(new Vector3(float.NaN,0,0), Vector3.Zero, 1));
    }
}
