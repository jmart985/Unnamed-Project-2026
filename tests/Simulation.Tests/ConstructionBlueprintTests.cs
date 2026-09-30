using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class ConstructionBlueprintTests
{
    [Fact]
    public void ProgressIsIncrementalAndClampedAtRequirements()
    {
        var blueprint = new ConstructionBlueprint("hut", 2, 3, new Dictionary<string,int> { [ItemIds.Wood] = 3 });
        Assert.Equal(ResourceMutationResult.Success, blueprint.ApplyProgress(ItemIds.Wood, 2));
        Assert.Equal(1, blueprint.Remaining(ItemIds.Wood));
        Assert.Equal(ResourceMutationResult.Success, blueprint.ApplyProgress(ItemIds.Wood, 20));
        Assert.True(blueprint.IsComplete);
        Assert.Equal(3, blueprint.GetProgress(ItemIds.Wood));
    }

    [Fact]
    public void InvalidRequirementsAndMaterialsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new ConstructionBlueprint("bad", 1, 1, new Dictionary<string,int> { ["missing"] = 1 }));
        var blueprint = new ConstructionBlueprint("hut", 1, 1, new Dictionary<string,int> { [ItemIds.Wood] = 1 });
        Assert.Equal(ResourceMutationResult.UnknownItem, blueprint.ApplyProgress(ItemIds.Food, 1));
        Assert.Equal(ResourceMutationResult.InvalidAmount, blueprint.ApplyProgress(ItemIds.Wood, 0));
    }
}
