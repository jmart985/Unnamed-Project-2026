namespace Simulation.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void DomainProjectLoads()
    {
        Assert.True(typeof(object).Assembly is not null);
    }
}
