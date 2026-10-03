using System.Text;
using Simulation;

namespace Simulation.Tests;

public sealed class TradeGoodCatalogTests
{
    [Fact]
    public void LoadsDataDrivenFixedValuesAndSupportsReadOnlyLookup()
    {
        var catalog = TradeGoodCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "trade_goods.json"));

        Assert.True(ItemIds.IsKnown(ItemIds.Coin));
        Assert.Equal(
            new[] { ItemIds.Food, ItemIds.Fruit, ItemIds.Meat, ItemIds.Plant, ItemIds.Wood },
            catalog.TradeGoods.Select(good => good.Id));
        Assert.True(catalog.IsTradeable(ItemIds.Wood));
        Assert.Equal(2, catalog.GetCoinValue(ItemIds.Wood));
        Assert.True(catalog.TryGetCoinValue(ItemIds.Food, out var foodValue));
        Assert.Equal(1, foodValue);
        Assert.False(catalog.IsTradeable(ItemIds.Coin));
        Assert.False(catalog.TryGetCoinValue(ItemIds.Coin, out _));
        Assert.False(catalog.IsTradeable("unknown"));
        Assert.False(catalog.TryGetCoinValue(null, out _));
    }

    [Fact]
    public void IdenticalAndReorderedLoadsExposeStableValuesAndIDs()
    {
        const string firstJson = """[{"id":"wood","coinValue":2},{"id":"food","coinValue":1}]""";
        const string reorderedJson = """[{"id":"food","coinValue":1},{"id":"wood","coinValue":2}]""";
        var first = Load(firstJson);
        var repeated = Load(firstJson);
        var reordered = Load(reorderedJson);

        Assert.Equal(first.TradeGoods, repeated.TradeGoods);
        Assert.Equal(
            first.TradeGoods,
            reordered.TradeGoods);
        Assert.Equal(2, repeated.GetCoinValue(ItemIds.Wood));
        Assert.Equal(1, reordered.GetCoinValue(ItemIds.Food));
    }

    [Theory]
    [InlineData("""[{"coinValue":1}]""", "must define a non-blank item ID")]
    [InlineData("""[{"id":"","coinValue":1}]""", "must define a non-blank item ID")]
    [InlineData("""[{"id":"   ","coinValue":1}]""", "must define a non-blank item ID")]
    [InlineData("""[{"id":"grain","coinValue":1}]""", "unknown item ID 'grain'")]
    [InlineData("""[{"id":"wood","coinValue":1},{"id":"wood","coinValue":2}]""", "defined more than once")]
    [InlineData("""[{"id":"coin","coinValue":1}]""", "cannot be configured as a trade good")]
    [InlineData("""[{"id":"wood","coinValue":0}]""", "must be a positive integer")]
    [InlineData("""[{"id":"wood","coinValue":-1}]""", "must be a positive integer")]
    [InlineData("""[{"id":"wood"}]""", "must define coinValue")]
    [InlineData("""[{"id":"wood","coinValue":1.5}]""", "valid trade-good records")]
    [InlineData("""[{"id":"wood","coinValue":1,"extra":true}]""", "valid trade-good records")]
    [InlineData("""[{"id":"wood","coinValue":1},{"id":"food","coinValue":0}]""", "must be a positive integer")]
    [InlineData("""[null]""", "cannot contain a null trade-good record")]
    [InlineData("""{}""", "valid trade-good records")]
    [InlineData("""[{"id":"wood","coinValue":1}""", "valid trade-good records")]
    [InlineData("""[]""", "at least one trade good")]
    public void RejectsInvalidCatalogDataWithoutExposingPartialCatalog(string json, string expectedMessage)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.Contains(expectedMessage, exception.Message);
    }

    private static TradeGoodCatalog Load(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return TradeGoodCatalog.Load(stream);
    }
}
