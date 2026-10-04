namespace Simulation;

public static class SettlementRecipes
{
    public const string BasicMealId = "basic-meal";

    public static WorkstationRecipe CreateBasicMeal() => new(
        BasicMealId,
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ItemIds.Plant] = 1,
            [ItemIds.Wood] = 1
        },
        new Dictionary<string, int>(StringComparer.Ordinal) { [ItemIds.Food] = 2 },
        workRequired: 5);
}
