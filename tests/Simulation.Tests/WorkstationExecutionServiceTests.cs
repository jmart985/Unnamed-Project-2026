using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class WorkstationExecutionServiceTests
{
    private static WorkstationRecipe CompleteRecipe()
    {
        var recipe = new WorkstationRecipe("meal", new Dictionary<string,int> { [ItemIds.Wood] = 2 }, new Dictionary<string,int> { [ItemIds.Food] = 1 }, 1);
        recipe.ApplyWork(1); return recipe;
    }

    [Fact]
    public void CompleteRecipeMovesInputsToOutputs()
    {
        var inputs = new ResourceContainer("inputs", 5); inputs.TryAdd(ItemIds.Wood, 2);
        var outputs = new ResourceContainer("outputs", 2);
        Assert.Equal(RecipeExecutionResult.Success, WorkstationExecutionService.TryExecute(CompleteRecipe(), inputs, outputs));
        Assert.Equal(0, inputs.UsedCapacity); Assert.Equal(1, outputs.UsedCapacity);
    }

    [Fact]
    public void FailurePathsDoNotPartiallyMutateContainers()
    {
        var inputs = new ResourceContainer("inputs", 5); inputs.TryAdd(ItemIds.Wood, 1);
        var outputs = new ResourceContainer("outputs", 0);
        Assert.Equal(RecipeExecutionResult.MissingInput, WorkstationExecutionService.TryExecute(CompleteRecipe(), inputs, outputs));
        inputs.TryAdd(ItemIds.Wood, 1);
        Assert.Equal(RecipeExecutionResult.OutputCapacityExceeded, WorkstationExecutionService.TryExecute(CompleteRecipe(), inputs, outputs));
        Assert.Equal(2, inputs.UsedCapacity);
    }
}

