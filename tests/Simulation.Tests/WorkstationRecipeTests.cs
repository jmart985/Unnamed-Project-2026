using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class WorkstationRecipeTests
{
    [Fact]
    public void RecipeProgressIsBoundedAndDefinitionsRemainStable()
    {
        var recipe = new WorkstationRecipe("meal", new Dictionary<string,int> { [ItemIds.Food] = 1 }, new Dictionary<string,int> { [ItemIds.Food] = 2 }, 5);
        Assert.True(recipe.ApplyWork(3));
        Assert.True(recipe.ApplyWork(9));
        Assert.True(recipe.IsComplete);
        Assert.Equal(5, recipe.WorkProgress);
        Assert.Equal(1, recipe.Inputs[ItemIds.Food]);
    }

    [Fact]
    public void UnknownOrEmptyMaterialsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new WorkstationRecipe("bad", new Dictionary<string,int> { ["unknown"] = 1 }, new Dictionary<string,int> { [ItemIds.Food] = 1 }, 1));
        Assert.Throws<ArgumentException>(() => new WorkstationRecipe("bad", new Dictionary<string,int>(), new Dictionary<string,int>(), 1));
    }
}
