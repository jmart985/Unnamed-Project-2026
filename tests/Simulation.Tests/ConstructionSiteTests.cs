using System.Numerics;
using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class ConstructionSiteTests
{
    [Fact]
    public void RegistryRejectsDuplicatesAndReflectsBlueprintCompletion()
    {
        var blueprint = new ConstructionBlueprint("hut", 1, 1, new Dictionary<string,int> { [ItemIds.Wood] = 1 }, workRequired: 1);
        var site = new SettlementConstructionSite("site-1", Vector3.Zero, blueprint);
        var registry = new ConstructionSiteRegistry();
        Assert.True(registry.TryRegister(site)); Assert.False(registry.TryRegister(site));
        Assert.Equal(ConstructionSiteState.Planned, site.State);
        blueprint.ApplyProgress(ItemIds.Wood, 1);
        Assert.Equal(ConstructionSiteState.InProgress, site.State);
        Assert.True(blueprint.ApplyWork(1));
        Assert.Equal(ConstructionSiteState.Complete, site.State);
    }
}
