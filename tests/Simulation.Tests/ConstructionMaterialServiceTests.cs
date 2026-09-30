using System.Numerics;
using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class ConstructionMaterialServiceTests
{
    [Fact]
    public void ConsumesRequiredMaterialsAndCompletesBlueprint()
    {
        var storage = new SettlementStorage("yard", Vector3.Zero, 5, [ItemIds.Wood]);
        storage.TryAdd(ItemIds.Wood, 2);
        var blueprint = new ConstructionBlueprint("hut", 1, 1, new Dictionary<string,int> { [ItemIds.Wood] = 2 });
        Assert.Equal(ConstructionMaterialResult.Success, ConstructionMaterialService.TryConsume(storage, blueprint));
        Assert.True(blueprint.IsComplete); Assert.Equal(0, storage.Contents.UsedCapacity);
    }

    [Fact]
    public void MissingMaterialsLeaveStateUnchanged()
    {
        var storage = new SettlementStorage("yard", Vector3.Zero, 5, [ItemIds.Wood]);
        storage.TryAdd(ItemIds.Wood, 1);
        var blueprint = new ConstructionBlueprint("hut", 1, 1, new Dictionary<string,int> { [ItemIds.Wood] = 2 });
        Assert.Equal(ConstructionMaterialResult.MissingMaterial, ConstructionMaterialService.TryConsume(storage, blueprint));
        Assert.Equal(1, storage.Contents.UsedCapacity); Assert.Equal(2, blueprint.Remaining(ItemIds.Wood));
    }
}
