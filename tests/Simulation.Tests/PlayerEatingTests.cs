using System.Numerics;
using System.Text;
using Simulation;

namespace Simulation.Tests;

public sealed class PlayerEatingTests
{
    private static string SpeciesPath => Path.Combine(AppContext.BaseDirectory, "data", "species.json");

    private static (NewGameSession Session, FoodConsumptionService Food, FoodSource Source) Create(
        SpeciesCatalog? catalog = null,
        string item = ItemIds.Food,
        int count = 3,
        Vector3? offset = null)
    {
        catalog ??= SpeciesCatalog.LoadFromFile(SpeciesPath);
        var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Ada"));
        var contents = new ResourceContainer("player-test-food", 100);
        if (count > 0) Assert.Equal(ResourceMutationResult.Success, contents.TryAdd(item, count));
        var source = session.World.AddFoodSource(
            "player-test-food", session.Player.Position + (offset ?? new Vector3(1, 0, 0)), contents);
        return (session, new FoodConsumptionService(session.World, catalog, _ => throw new InvalidOperationException()), source);
    }

    [Fact]
    public void InRangeRequestConsumesOneUnitAndAppliesNutritionOnce()
    {
        var (session, food, source) = Create();
        var before = session.Player.Needs.Satiety;
        var remainingBefore = food.FoodUnitsRemaining;

        var result = food.TryPlayerEat(source.Id);

        Assert.Equal(PlayerEatStatus.Ate, result.Status);
        Assert.Equal(Math.Min(100, before + session.Player.Needs.NutritionPerUnit), session.Player.Needs.Satiety);
        Assert.Equal(result.Satiety, session.Player.Needs.Satiety);
        Assert.True(source.Contents.TryGetCount(ItemIds.Food, out var left));
        Assert.Equal(2, left);
        Assert.Equal(1, food.FoodUnitsConsumed);
        Assert.Equal(remainingBefore - 1, food.FoodUnitsRemaining);
    }

    [Fact]
    public void RepeatedRequestsConserveFoodAndClampSatiety()
    {
        var (session, food, source) = Create(count: 20);
        var total = food.FoodUnitsRemaining;

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(PlayerEatStatus.Ate, food.TryPlayerEat(source.Id).Status);
            Assert.InRange(session.Player.Needs.Satiety, 0, 100);
            Assert.Equal(total, food.FoodUnitsRemaining + food.FoodUnitsConsumed);
        }

        Assert.Equal(100, session.Player.Needs.Satiety);
        Assert.Equal(20, food.FoodUnitsConsumed);
    }

    [Fact]
    public void OutOfRangeRequestIsRejectedWithoutMutation()
    {
        var (session, food, source) = Create(offset: new Vector3(10, 0, 0));
        AssertRejected(session, food, source, PlayerEatStatus.OutOfRange);
    }

    [Fact]
    public void EmptySourceRequestIsRejectedWithoutMutation()
    {
        var (session, food, source) = Create(count: 0);
        AssertRejected(session, food, source, PlayerEatStatus.SourceEmpty);
    }

    [Fact]
    public void MissingSourceRequestIsRejectedWithoutMutation()
    {
        var (session, food, _) = Create();
        var satiety = session.Player.Needs.Satiety;
        var remaining = food.FoodUnitsRemaining;

        Assert.Equal(PlayerEatStatus.SourceNotFound, food.TryPlayerEat("missing").Status);
        Assert.Equal(PlayerEatStatus.SourceNotFound, food.TryPlayerEat(null).Status);
        Assert.Equal(satiety, session.Player.Needs.Satiety);
        Assert.Equal(remaining, food.FoodUnitsRemaining);
    }

    [Fact]
    public void IncompatibleFoodRequestIsRejectedWithoutMutation()
    {
        var json = File.ReadAllText(SpeciesPath)
            .Replace("\"diet\": [\"food\", \"plant\", \"fruit\", \"meat\"]", "\"diet\": [\"meat\"]", StringComparison.Ordinal)
            .Replace("\"dietaryProfile\": \"Omnivore\"", "\"dietaryProfile\": \"Carnivore\"", StringComparison.Ordinal);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var (session, food, source) = Create(SpeciesCatalog.Load(stream), ItemIds.Plant);

        AssertRejected(session, food, source, PlayerEatStatus.IncompatibleFood);
    }

    private static void AssertRejected(
        NewGameSession session, FoodConsumptionService food, FoodSource source, PlayerEatStatus expected)
    {
        var satiety = session.Player.Needs.Satiety;
        var remaining = food.FoodUnitsRemaining;
        var counts = source.Contents.Counts.ToDictionary(p => p.Key, p => p.Value);

        var result = food.TryPlayerEat(source.Id);

        Assert.Equal(expected, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.Equal(satiety, session.Player.Needs.Satiety);
        Assert.Equal(remaining, food.FoodUnitsRemaining);
        Assert.Equal(0, food.FoodUnitsConsumed);
        Assert.Equal(counts, source.Contents.Counts.ToDictionary(p => p.Key, p => p.Value));
    }
}
