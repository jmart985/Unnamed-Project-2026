using System.Text;
using Simulation;

namespace Simulation.Tests;

public sealed class TradeGoodsCatalogTests
{
    [Fact]
    public void LoadsFixedTradeGoodsAndSupportsReadOnlyLookups()
    {
        var catalog = TradeGoodsCatalog.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "data", "trade_goods.json"));

        Assert.Equal(
            new[]
            {
                new TradeGoodDefinition(ItemIds.Food, 2),
                new TradeGoodDefinition(ItemIds.Fruit, 3),
                new TradeGoodDefinition(ItemIds.Meat, 4),
                new TradeGoodDefinition(ItemIds.Plant, 1),
                new TradeGoodDefinition(ItemIds.Wood, 2)
            },
            catalog.TradeGoods);
        Assert.True(ItemIds.IsKnown(ItemIds.Coin));
        Assert.True(catalog.IsTradeable(ItemIds.Food));
        Assert.True(catalog.TryGetCoinValue(ItemIds.Food, out var coinValue));
        Assert.Equal(2, coinValue);
        Assert.False(catalog.IsTradeable(ItemIds.Coin));
        Assert.False(catalog.TryGetCoinValue(ItemIds.Coin, out _));
        Assert.False(catalog.IsTradeable("unknown"));
        Assert.False(catalog.IsTradeable(null));
    }

    [Fact]
    public void RepeatedLoadsAreStableAndIndependentOfInputOrder()
    {
        const string forward =
            """[{"resourceId":"wood","coinValue":2},{"resourceId":"food","coinValue":1}]""";
        const string reverse =
            """[{"resourceId":"food","coinValue":1},{"resourceId":"wood","coinValue":2}]""";

        var first = Load(forward);
        var second = Load(reverse);

        Assert.Equal(first.TradeGoods, second.TradeGoods);
        Assert.Equal(
            new[] { ItemIds.Food, ItemIds.Wood },
            first.TradeGoods.Select(tradeGood => tradeGood.ResourceId));
        Assert.True(first.TryGetCoinValue(ItemIds.Wood, out var firstValue));
        Assert.True(second.TryGetCoinValue(ItemIds.Wood, out var secondValue));
        Assert.Equal(firstValue, secondValue);
    }

    [Theory]
    [InlineData("""[{"coinValue":1}]""", "nonblank resourceId")]
    [InlineData("""[{"resourceId":" ","coinValue":1}]""", "nonblank resourceId")]
    [InlineData("""[{"resourceId":"grain","coinValue":1}]""", "unknown resource ID")]
    [InlineData("""[{"resourceId":"coin","coinValue":1}]""", "reserved as currency")]
    [InlineData("""[{"resourceId":"food","coinValue":1},{"resourceId":"food","coinValue":2}]""", "defined more than once")]
    [InlineData("""[{"resourceId":"food","coinValue":0}]""", "positive integer")]
    [InlineData("""[{"resourceId":"food","coinValue":-1}]""", "positive integer")]
    [InlineData("""[{"resourceId":"food"}]""", "must define a coinValue")]
    [InlineData("""[{"resourceId":"food","coinValue":1.5}]""", "JSON array of valid trade-good records")]
    [InlineData("""[{"resourceId":"food","coinValue":1,"extra":true}]""", "JSON array of valid trade-good records")]
    public void RejectsInvalidTradeGoodDefinitions(string json, string expectedMessage)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[null]")]
    public void RejectsMalformedOrEmptyCatalogData(string json)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.NotEmpty(exception.Message);
    }

    private static TradeGoodsCatalog Load(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return TradeGoodsCatalog.Load(stream);
    }
}
