using Simulation;

namespace Simulation.Tests;

public sealed class SettlementMarketTests
{
    private static readonly TradeGoodsCatalog Catalog = TradeGoodsCatalog.LoadFromFile(
        Path.Combine(AppContext.BaseDirectory, "data", "trade_goods.json"));

    private sealed record Setup(World World, SettlementMarket Market, ResourceContainer MarketBox, ResourceContainer Buyer);

    private static Setup Create(int marketCap = 100, int partyCap = 100)
    {
        var world = new World();
        var box = new ResourceContainer("market", marketCap);
        Assert.Equal(ResourceMutationResult.Success, box.TryAdd(ItemIds.Wood, 10));
        Assert.Equal(ResourceMutationResult.Success, box.TryAdd(ItemIds.Coin, 20));
        world.AddStockpile("market", System.Numerics.Vector3.Zero, box);
        var party = new ResourceContainer("caravan", partyCap);
        Assert.Equal(ResourceMutationResult.Success, party.TryAdd(ItemIds.Wood, 5));
        Assert.Equal(ResourceMutationResult.Success, party.TryAdd(ItemIds.Coin, 30));
        return new Setup(world, new SettlementMarket(world, Catalog, "market"), box, party);
    }

    private static int[] Totals(params ResourceContainer[] boxes) =>
        boxes.SelectMany(b => new[]
        {
            b.Counts.GetValueOrDefault(ItemIds.Wood), b.Counts.GetValueOrDefault(ItemIds.Coin), b.UsedCapacity
        }).ToArray();

    private static void AssertRejected(Setup s, MarketTransactionRequest request, MarketTransactionStatus expected)
    {
        var before = Totals(s.MarketBox, s.Buyer);
        var result = s.Market.TryExecute(request);
        Assert.Equal(expected, result.Status);
        Assert.False(result.Succeeded);
        Assert.Equal(before, Totals(s.MarketBox, s.Buyer));
    }

    [Fact]
    public void PurchaseMovesGoodsToCounterpartyAndFixedCoinToMarket()
    {
        var s = Create();
        var result = s.Market.TryExecute(new(MarketTradeSide.Purchase, ItemIds.Wood, 4, s.Buyer));
        Assert.Equal(new MarketTransactionResult(MarketTransactionStatus.Success, 4, 8), result);
        Assert.Equal(new[] { 6, 28, 34, 9, 22, 31 }, Totals(s.MarketBox, s.Buyer));
    }

    [Fact]
    public void SaleMovesGoodsToMarketAndFixedCoinToCounterparty()
    {
        var s = Create();
        var result = s.Market.TryExecute(new(MarketTradeSide.Sale, ItemIds.Wood, 3, s.Buyer));
        Assert.True(result.Succeeded);
        Assert.Equal(6, result.CoinAmount);
        Assert.Equal(new[] { 13, 14, 27, 2, 36, 38 }, Totals(s.MarketBox, s.Buyer));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveQuantityIsRejected(int quantity)
    {
        var s = Create();
        AssertRejected(s, new(MarketTradeSide.Purchase, ItemIds.Wood, quantity, s.Buyer), MarketTransactionStatus.InvalidQuantity);
    }

    [Fact]
    public void OverflowingPriceArithmeticIsRejected()
    {
        var s = Create();
        AssertRejected(s, new(MarketTradeSide.Sale, ItemIds.Wood, int.MaxValue, s.Buyer), MarketTransactionStatus.ArithmeticOverflow);
    }

    [Fact]
    public void InvalidRequestsAreRejected()
    {
        var s = Create();
        AssertRejected(s, new(MarketTradeSide.Purchase, ItemIds.Coin, 1, s.Buyer), MarketTransactionStatus.NotTradeable);
        AssertRejected(s, new(MarketTradeSide.Purchase, "gold", 1, s.Buyer), MarketTransactionStatus.NotTradeable);
        AssertRejected(s, new(MarketTradeSide.Purchase, ItemIds.Wood, 1, null), MarketTransactionStatus.InvalidCounterparty);
        AssertRejected(s, new(MarketTradeSide.Purchase, ItemIds.Wood, 1, s.MarketBox), MarketTransactionStatus.InvalidCounterparty);
        AssertRejected(s, new((MarketTradeSide)99, ItemIds.Wood, 1, s.Buyer), MarketTransactionStatus.InvalidRequest);
        Assert.Equal(MarketTransactionStatus.InvalidRequest, s.Market.TryExecute(null).Status);
    }

    [Fact]
    public void RemovedMarketIsRejected()
    {
        var s = Create();
        Assert.True(s.World.TryRemoveStockpile("market"));
        AssertRejected(s, new(MarketTradeSide.Purchase, ItemIds.Wood, 1, s.Buyer), MarketTransactionStatus.InvalidMarket);
    }

    [Fact]
    public void InsufficientStockAndFundsAreRejected()
    {
        var s = Create();
        AssertRejected(s, new(MarketTradeSide.Purchase, ItemIds.Wood, 11, s.Buyer), MarketTransactionStatus.InsufficientMarketGoods);
        AssertRejected(s, new(MarketTradeSide.Purchase, ItemIds.Fruit, 1, s.Buyer), MarketTransactionStatus.InsufficientMarketGoods);
        AssertRejected(s, new(MarketTradeSide.Sale, ItemIds.Wood, 6, s.Buyer), MarketTransactionStatus.InsufficientCounterpartyGoods);
        var poor = new ResourceContainer("poor", 50);
        poor.TryAdd(ItemIds.Coin, 1);
        var before = Totals(s.MarketBox, poor);
        Assert.Equal(MarketTransactionStatus.InsufficientCounterpartyCoin,
            s.Market.TryExecute(new(MarketTradeSide.Purchase, ItemIds.Wood, 1, poor)).Status);
        Assert.Equal(before, Totals(s.MarketBox, poor));
        s.Buyer.TryAdd(ItemIds.Wood, 20);
        AssertRejected(s, new(MarketTradeSide.Sale, ItemIds.Wood, 11, s.Buyer), MarketTransactionStatus.InsufficientMarketCoin);
    }

    [Fact]
    public void CapacityFailuresAreRejectedWithoutMutation()
    {
        var full = Create(marketCap: 30, partyCap: 100);
        AssertRejected(full, new(MarketTradeSide.Purchase, ItemIds.Wood, 1, full.Buyer), MarketTransactionStatus.MarketCapacityExceeded);

        var tight = Create(marketCap: 100, partyCap: 35);
        AssertRejected(tight, new(MarketTradeSide.Sale, ItemIds.Wood, 1, tight.Buyer), MarketTransactionStatus.CounterpartyCapacityExceeded);
    }

    [Fact]
    public void FullContainersCanSwapBecauseLegsNetOut()
    {
        var world = new World();
        var box = new ResourceContainer("market", 10);
        box.TryAdd(ItemIds.Plant, 5);
        box.TryAdd(ItemIds.Coin, 5);
        world.AddStockpile("market", System.Numerics.Vector3.Zero, box);
        var party = new ResourceContainer("caravan", 5);
        party.TryAdd(ItemIds.Coin, 5);
        var market = new SettlementMarket(world, Catalog, "market");

        Assert.True(market.TryExecute(new(MarketTradeSide.Purchase, ItemIds.Plant, 5, party)).Succeeded);
        Assert.Equal(10, box.UsedCapacity);
        Assert.Equal(10, box.Counts[ItemIds.Coin]);
        Assert.Equal(5, party.Counts[ItemIds.Plant]);
    }
    [Fact]
    public void RepeatedTransactionsApplyEachLegOnceAndNeverOverdraw()
    {
        var s = Create();
        for (var i = 0; i < 5; i++)
        {
            Assert.True(s.Market.TryExecute(new(MarketTradeSide.Purchase, ItemIds.Wood, 2, s.Buyer)).Succeeded);
        }

        Assert.Equal(0, s.MarketBox.Counts.GetValueOrDefault(ItemIds.Wood));
        Assert.Equal(40, s.MarketBox.Counts[ItemIds.Coin]);
        Assert.Equal(15, s.Buyer.Counts[ItemIds.Wood]);
        Assert.Equal(10, s.Buyer.Counts[ItemIds.Coin]);
        AssertRejected(s, new(MarketTradeSide.Purchase, ItemIds.Wood, 1, s.Buyer), MarketTransactionStatus.InsufficientMarketGoods);
        Assert.Equal(50, s.MarketBox.Counts[ItemIds.Coin] + s.Buyer.Counts[ItemIds.Coin]);
    }

    [Fact]
    public void ExchangeHelperRejectsSameContainerAndSameItem()
    {
        var s = Create();
        Assert.Equal(ResourceExchangeResult.InvalidDestination,
            ResourceTransferService.TryExchange(s.Buyer, s.Buyer, ItemIds.Wood, 1, ItemIds.Coin, 1));
        Assert.Equal(ResourceExchangeResult.InvalidAmount,
            ResourceTransferService.TryExchange(s.MarketBox, s.Buyer, ItemIds.Wood, 1, ItemIds.Wood, 1));
    }
}
