using Simulation;
using System.Numerics;

namespace Simulation.Tests;

public sealed class CaravanVisitServiceTests
{
    private static readonly TradeGoodsCatalog Catalog = TradeGoodsCatalog.LoadFromFile(
        Path.Combine(AppContext.BaseDirectory, "data", "trade_goods.json"));

    private sealed record Setup(
        World World,
        SettlementMarket Market,
        ResourceContainer MarketBox,
        ResourceContainer Caravan);

    private static Setup Create()
    {
        var world = new World();
        var marketBox = new ResourceContainer("market", 40);
        Assert.Equal(ResourceMutationResult.Success, marketBox.TryAdd(ItemIds.Wood, 5));
        Assert.Equal(ResourceMutationResult.Success, marketBox.TryAdd(ItemIds.Coin, 10));
        world.AddStockpile("market", Vector3.Zero, marketBox);

        var caravan = new ResourceContainer("caravan", 40);
        Assert.Equal(ResourceMutationResult.Success, caravan.TryAdd(ItemIds.Coin, 20));
        return new Setup(world, new SettlementMarket(world, Catalog, "market"), marketBox, caravan);
    }

    private static int[] Totals(ResourceContainer market, ResourceContainer caravan) =>
    [
        market.Counts.GetValueOrDefault(ItemIds.Wood),
        market.Counts.GetValueOrDefault(ItemIds.Coin),
        market.UsedCapacity,
        caravan.Counts.GetValueOrDefault(ItemIds.Wood),
        caravan.Counts.GetValueOrDefault(ItemIds.Coin),
        caravan.UsedCapacity
    ];

    [Fact]
    public void ValidVisitTradesAndDepartsOnceWithoutLosingResources()
    {
        var setup = Create();

        Assert.Equal(
            CaravanVisitStatus.Opened,
            setup.World.CaravanVisits.TryOpen("visit-1", setup.Market, setup.Caravan).Status);

        var trade = setup.World.CaravanVisits.TryTrade(
            "visit-1",
            MarketTradeSide.Purchase,
            ItemIds.Wood,
            2);

        Assert.Equal(CaravanVisitStatus.Traded, trade.Status);
        Assert.Equal(
            new MarketTransactionResult(MarketTransactionStatus.Success, 2, 4),
            trade.Transaction);
        Assert.Equal(new[] { 3, 14, 17, 2, 16, 18 }, Totals(setup.MarketBox, setup.Caravan));

        Assert.Equal(CaravanVisitStatus.Departed, setup.World.CaravanVisits.TryDepart("visit-1").Status);
        var closedTotals = Totals(setup.MarketBox, setup.Caravan);
        Assert.Equal(CaravanVisitStatus.AlreadyDeparted, setup.World.CaravanVisits.TryDepart("visit-1").Status);
        Assert.Equal(
            CaravanVisitStatus.AlreadyDeparted,
            setup.World.CaravanVisits.TryTrade(
                "visit-1",
                MarketTradeSide.Purchase,
                ItemIds.Wood,
                1).Status);
        Assert.Equal(closedTotals, Totals(setup.MarketBox, setup.Caravan));
    }

    [Fact]
    public void FiniteMarketStockExhaustionRejectsTradeWithoutMutation()
    {
        var setup = Create();
        Assert.Equal(
            CaravanVisitStatus.Opened,
            setup.World.CaravanVisits.TryOpen("visit-stock", setup.Market, setup.Caravan).Status);
        Assert.Equal(
            CaravanVisitStatus.Traded,
            setup.World.CaravanVisits.TryTrade(
                "visit-stock",
                MarketTradeSide.Purchase,
                ItemIds.Wood,
                5).Status);

        var beforeRejectedTrade = Totals(setup.MarketBox, setup.Caravan);
        var rejected = setup.World.CaravanVisits.TryTrade(
            "visit-stock",
            MarketTradeSide.Purchase,
            ItemIds.Wood,
            1);

        Assert.Equal(CaravanVisitStatus.TradeRejected, rejected.Status);
        Assert.Equal(MarketTransactionStatus.InsufficientMarketGoods, rejected.Transaction?.Status);
        Assert.Equal(beforeRejectedTrade, Totals(setup.MarketBox, setup.Caravan));
        Assert.Equal(CaravanVisitStatus.Departed, setup.World.CaravanVisits.TryDepart("visit-stock").Status);
    }

    [Fact]
    public void InvalidDuplicateAndStaleVisitRequestsDoNotMutateResources()
    {
        var setup = Create();
        var before = Totals(setup.MarketBox, setup.Caravan);
        Assert.Equal(
            CaravanVisitStatus.InvalidRequest,
            setup.World.CaravanVisits.TryOpen(" ", setup.Market, setup.Caravan).Status);
        Assert.Equal(
            CaravanVisitStatus.InvalidMarket,
            setup.World.CaravanVisits.TryOpen("no-market", null, setup.Caravan).Status);
        Assert.Equal(
            CaravanVisitStatus.InvalidCaravan,
            setup.World.CaravanVisits.TryOpen("no-caravan", setup.Market, null).Status);
        Assert.Equal(
            CaravanVisitStatus.InvalidCaravan,
            setup.World.CaravanVisits.TryOpen("market-as-caravan", setup.Market, setup.MarketBox).Status);

        var otherWorld = new World();
        otherWorld.AddStockpile(
            "other-market",
            Vector3.Zero,
            new ResourceContainer("other-market", 10));
        var foreignMarket = new SettlementMarket(otherWorld, Catalog, "other-market");
        Assert.Equal(
            CaravanVisitStatus.InvalidMarket,
            setup.World.CaravanVisits.TryOpen("foreign-market", foreignMarket, setup.Caravan).Status);

        Assert.Equal(
            CaravanVisitStatus.Opened,
            setup.World.CaravanVisits.TryOpen("visit-duplicate", setup.Market, setup.Caravan).Status);
        Assert.Equal(
            CaravanVisitStatus.DuplicateVisit,
            setup.World.CaravanVisits.TryOpen("visit-duplicate", setup.Market, setup.Caravan).Status);
        Assert.Equal(
            CaravanVisitStatus.DuplicateCaravan,
            setup.World.CaravanVisits.TryOpen("visit-same-caravan", setup.Market, setup.Caravan).Status);
        Assert.Equal(
            CaravanVisitStatus.StaleVisit,
            setup.World.CaravanVisits.TryTrade(
                "unknown-visit",
                MarketTradeSide.Purchase,
                ItemIds.Wood,
                1).Status);
        Assert.Equal(
            CaravanVisitStatus.InvalidRequest,
            setup.World.CaravanVisits.TryTrade(
                null,
                MarketTradeSide.Purchase,
                ItemIds.Wood,
                1).Status);
        Assert.Equal(before, Totals(setup.MarketBox, setup.Caravan));
    }

    [Fact]
    public void RejectedTradeKeepsVisitOpenAndCancellationReleasesCaravan()
    {
        var setup = Create();
        Assert.Equal(
            CaravanVisitStatus.Opened,
            setup.World.CaravanVisits.TryOpen("visit-cancel", setup.Market, setup.Caravan).Status);

        var before = Totals(setup.MarketBox, setup.Caravan);
        var rejected = setup.World.CaravanVisits.TryTrade(
            "visit-cancel",
            MarketTradeSide.Purchase,
            ItemIds.Wood,
            0);
        Assert.Equal(CaravanVisitStatus.TradeRejected, rejected.Status);
        Assert.Equal(MarketTransactionStatus.InvalidQuantity, rejected.Transaction?.Status);
        Assert.Equal(before, Totals(setup.MarketBox, setup.Caravan));

        Assert.Equal(CaravanVisitStatus.Cancelled, setup.World.CaravanVisits.TryCancel("visit-cancel").Status);
        Assert.Equal(
            CaravanVisitStatus.AlreadyCancelled,
            setup.World.CaravanVisits.TryCancel("visit-cancel").Status);
        Assert.Equal(
            CaravanVisitStatus.AlreadyCancelled,
            setup.World.CaravanVisits.TryDepart("visit-cancel").Status);
        Assert.Equal(
            CaravanVisitStatus.AlreadyCancelled,
            setup.World.CaravanVisits.TryTrade(
                "visit-cancel",
                MarketTradeSide.Purchase,
                ItemIds.Wood,
                1).Status);
        Assert.Equal(before, Totals(setup.MarketBox, setup.Caravan));

        Assert.Equal(
            CaravanVisitStatus.DuplicateVisit,
            setup.World.CaravanVisits.TryOpen("visit-cancel", setup.Market, setup.Caravan).Status);
        Assert.Equal(
            CaravanVisitStatus.Opened,
            setup.World.CaravanVisits.TryOpen("visit-after-cancel", setup.Market, setup.Caravan).Status);
        setup.World.Reset();
        Assert.Equal(
            CaravanVisitStatus.Invalidated,
            setup.World.CaravanVisits.TryTrade(
                "visit-after-cancel",
                MarketTradeSide.Purchase,
                ItemIds.Wood,
                1).Status);
        Assert.Equal(before, Totals(setup.MarketBox, setup.Caravan));
    }

    [Fact]
    public void RepeatingTheSameVisitInputsProducesIdenticalResultsAndTotals()
    {
        static (CaravanVisitResult Open, CaravanVisitResult Trade, CaravanVisitResult Depart, int[] Totals) Run()
        {
            var setup = Create();
            var open = setup.World.CaravanVisits.TryOpen("repeatable-visit", setup.Market, setup.Caravan);
            var trade = setup.World.CaravanVisits.TryTrade(
                "repeatable-visit",
                MarketTradeSide.Purchase,
                ItemIds.Wood,
                2);
            var depart = setup.World.CaravanVisits.TryDepart("repeatable-visit");
            return (open, trade, depart, Totals(setup.MarketBox, setup.Caravan));
        }

        var first = Run();
        var second = Run();

        Assert.Equal(first.Open, second.Open);
        Assert.Equal(first.Trade, second.Trade);
        Assert.Equal(first.Depart, second.Depart);
        Assert.Equal(first.Totals, second.Totals);
    }
}
