namespace Simulation;

public enum RecipeExecutionResult { Success, IncompleteRecipe, MissingInput, OutputCapacityExceeded, InvalidContainer }

public static class WorkstationExecutionService
{
    public static RecipeExecutionResult TryExecute(WorkstationRecipe? recipe, ResourceContainer? inputs, ResourceContainer? outputs)
    {
        if (recipe is null || inputs is null || outputs is null) return RecipeExecutionResult.InvalidContainer;
        if (!recipe.IsComplete) return RecipeExecutionResult.IncompleteRecipe;
        foreach (var requirement in recipe.Inputs)
        {
            if (!inputs.HasQuantity(requirement.Key, requirement.Value)) return RecipeExecutionResult.MissingInput;
        }
        var outputAmount = recipe.Outputs.Values.Sum();
        if (outputAmount > outputs.RemainingCapacity) return RecipeExecutionResult.OutputCapacityExceeded;
        foreach (var requirement in recipe.Inputs) inputs.RemoveKnown(requirement.Key, requirement.Value);
        foreach (var output in recipe.Outputs) outputs.AddKnown(output.Key, output.Value);
        return RecipeExecutionResult.Success;
    }
}
