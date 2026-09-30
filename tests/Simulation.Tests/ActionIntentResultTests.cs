using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class ActionIntentResultTests
{
    [Fact]
    public void FoodSelectionMapsAcceptedAndDeferredOutcomes()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", Vector3.Zero);
        var source = world.AddFoodSource("food", Vector3.One, new ResourceContainer("food", 10));
        var accepted = ActionIntentResult.FromFoodSelection(entity, new FoodSelectionResult(FoodSelectionStatus.Selected, source, "eat-1"));
        var deferred = ActionIntentResult.FromFoodSelection(entity, new FoodSelectionResult(FoodSelectionStatus.Waiting, WaitingReason: "no reachable food"));

        Assert.Equal(("entity-000001", ActionIntentStatus.Accepted, "food_selected", "food"), (accepted.EntityId, accepted.Status, accepted.ReasonCode, accepted.TargetId));
        Assert.Equal((ActionIntentStatus.Deferred, "waiting_for_food"), (deferred.Status, deferred.ReasonCode));
    }

    [Fact]
    public void HaulingMapsRejectedOutcomeWithoutSceneReferences()
    {
        var world = new World();
        var entity = world.SpawnEntity("creature", Vector3.Zero);
        var result = ActionIntentResult.FromWoodHauling(entity, new WoodHaulingResult(WoodHaulingStatus.NotHauler));

        Assert.Equal("entity-000001", result.EntityId);
        Assert.Equal(ActionIntentStatus.Rejected, result.Status);
        Assert.Equal("not_hauler", result.ReasonCode);
        Assert.Null(result.TargetId);
    }
}
