using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class CaravanVisitServiceTests
{
    private static readonly TradeGoodsCatalog Catalog = TradeGoodsCatalog.LoadFromFile(
        Path.Combine(AppContext.BaseDirectory, "data", "trade_goods.json"));

    private sealed record Setup(
        World World,
        CaravanVisitService Visits,
        ResourceContainer Settlement,
        ResourceContainer Cargo);

    private sealed record ScenarioResult(
        CaravanVisitStatus Arrival,
        MarketTransactionResult Purchase,
        MarketTransactionResult Sale,
        CaravanVisitStatus Departure,
        int[] SettlementTotals,
        int[] CaravanTotals);

    private static Setup Create(
        int settlementWood = 10,
        int settlementCoin = 20,
        int caravanWood = 5,
        int caravanCoin = 30)
    {
        var world = new World();
        var settlement = new ResourceContainer("settlement", 100);
        AddIfPositive(settlement, ItemIds.Wood, settlementWood);
        AddIfPositive(settlement, ItemIds.Coin, settlementCoin);
        world.AddStockpile("market", Vector3.Zero, settlement);

        var cargo = new ResourceContainer("caravan", 100);
        AddIfPositive(cargo, ItemIds.Wood, caravanWood);
        AddIfPositive(cargo, ItemIds.Coin, caravanCoin);

        var market = new SettlementMarket(world, Catalog, "market");
        return new Setup(world, new CaravanVisitService(market), settlement, cargo);
    }

    [Fact]
    public void VisitArrivesTradesAndDepartsWithExactOppositeDeltas()
    {
        var setup = Create();
        var opened = setup.Visits.TryOpen("visit-1", setup.Cargo);
        Assert.Equal(CaravanVisitStatus.Success, opened.Status);
        Assert.NotNull(opened.Visit);

        var beforeNotArrived = Snapshot(setup.Settlement, setup.Cargo);
        var notArrived = setup.Visits.TryTrade(opened.Visit, MarketTradeSide.Purchase, ItemIds.Wood, 1);
        Assert.Equal(CaravanVisitStatus.NotArrived, notArrived.Status);
        AssertUnchanged(beforeNotArrived, setup.Settlement, setup.Cargo);

        var arrival = setup.Visits.TryArrive(opened.Visit);
        Assert.Equal(CaravanVisitStatus.Success, arrival.Status);
        Assert.Equal(CaravanVisitState.Arrived, opened.Visit.State);

        var purchase = setup.Visits.TryTrade(opened.Visit, MarketTradeSide.Purchase, ItemIds.Wood, 2);
        Assert.Equal(CaravanVisitStatus.Success, purchase.Status);
        Assert.Equal(new MarketTransactionResult(MarketTransactionStatus.Success, 2, 4), purchase.Transaction);
        Assert.Equal((8, 24), (Count(setup.Settlement, ItemIds.Wood), Count(setup.Settlement, ItemIds.Coin)));
        Assert.Equal((7, 26), (Count(setup.Cargo, ItemIds.Wood), Count(setup.Cargo, ItemIds.Coin)));

        var sale = setup.Visits.TryTrade(opened.Visit, MarketTradeSide.Sale, ItemIds.Wood, 1);
        Assert.Equal(new MarketTransactionResult(MarketTransactionStatus.Success, 1, 2), sale.Transaction);
        Assert.Equal((9, 22), (Count(setup.Settlement, ItemIds.Wood), Count(setup.Settlement, ItemIds.Coin)));
        Assert.Equal((6, 28), (Count(setup.Cargo, ItemIds.Wood), Count(setup.Cargo, ItemIds.Coin)));

        Assert.Equal(CaravanVisitStatus.AlreadyArrived, setup.Visits.TryArrive(opened.Visit).Status);
        Assert.Equal(CaravanVisitStatus.Success, setup.Visits.TryDepart(opened.Visit).Status);
        Assert.Equal(CaravanVisitState.Departed, opened.Visit.State);

        var beforeRejectedRequests = Snapshot(setup.Settlement, setup.Cargo);
        Assert.Equal(CaravanVisitStatus.AlreadyDeparted,
            setup.Visits.TryTrade(opened.Visit, MarketTradeSide.Purchase, ItemIds.Wood, 1).Status);
        Assert.Equal(CaravanVisitStatus.AlreadyDeparted, setup.Visits.TryArrive(opened.Visit).Status);
        Assert.Equal(CaravanVisitStatus.AlreadyDeparted, setup.Visits.TryDepart(opened.Visit).Status);
        Assert.Equal(beforeRejectedRequests, Snapshot(setup.Settlement, setup.Cargo));
        var nextCargo = CreateCargo("next-caravan", wood: 1, coin: 1);
        Assert.Equal(CaravanVisitStatus.Success, setup.Visits.TryOpen("visit-2", nextCargo).Status);
    }

    [Fact]
    public void DuplicateAndConcurrentVisitsAreRejectedWithoutMutation()
    {
        var setup = Create();
        var first = setup.Visits.TryOpen("visit-1", setup.Cargo);
        Assert.Equal(CaravanVisitStatus.Success, first.Status);

        var before = Snapshot(setup.Settlement, setup.Cargo);
        var duplicate = setup.Visits.TryOpen("visit-1", setup.Cargo);
        var otherCargo = CreateCargo("other-caravan", wood: 1, coin: 1);
        var otherCargoBefore = Snapshot(otherCargo);
        var concurrent = setup.Visits.TryOpen("visit-2", otherCargo);

        Assert.Equal(CaravanVisitStatus.DuplicateVisit, duplicate.Status);
        Assert.Equal(CaravanVisitStatus.VisitInProgress, concurrent.Status);
        AssertUnchanged(before, setup.Settlement, setup.Cargo);
        AssertUnchanged(otherCargoBefore, otherCargo);
    }

    [Fact]
    public void StaleVisitsFromAnotherServiceAreRejectedWithoutMutation()
    {
        var first = Create();
        var opened = first.Visits.TryOpen("visit-1", first.Cargo);
        Assert.Equal(CaravanVisitStatus.Success, opened.Status);

        var second = Create();
        var before = Snapshot(second.Settlement, second.Cargo);
        var firstBefore = Snapshot(first.Settlement, first.Cargo);
        Assert.Equal(CaravanVisitStatus.StaleVisit,
            second.Visits.TryArrive(opened.Visit).Status);
        Assert.Equal(CaravanVisitStatus.StaleVisit,
            second.Visits.TryTrade(opened.Visit, MarketTradeSide.Purchase, ItemIds.Wood, 1).Status);
        Assert.Equal(before, Snapshot(second.Settlement, second.Cargo));
        AssertUnchanged(firstBefore, first.Settlement, first.Cargo);
    }

    [Fact]
    public void RejectedTradeLeavesTotalsUntouchedAndVisitCanRecover()
    {
        var setup = Create(settlementWood: 0, settlementCoin: 20, caravanWood: 2, caravanCoin: 10);
        var visit = setup.Visits.TryOpen("visit-1", setup.Cargo).Visit!;
        Assert.Equal(CaravanVisitStatus.Success, setup.Visits.TryArrive(visit).Status);

        var beforeRejectedTrade = Snapshot(setup.Settlement, setup.Cargo);
        var rejected = setup.Visits.TryTrade(visit, MarketTradeSide.Purchase, ItemIds.Wood, 1);
        Assert.Equal(CaravanVisitStatus.TradeRejected, rejected.Status);
        Assert.Equal(MarketTransactionStatus.InsufficientMarketGoods, rejected.Transaction?.Status);
        Assert.Equal(beforeRejectedTrade, Snapshot(setup.Settlement, setup.Cargo));

        var sale = setup.Visits.TryTrade(visit, MarketTradeSide.Sale, ItemIds.Wood, 1);
        Assert.Equal(CaravanVisitStatus.Success, sale.Status);
        Assert.Equal(new MarketTransactionResult(MarketTransactionStatus.Success, 1, 2), sale.Transaction);
        Assert.Equal((1, 18), (Count(setup.Settlement, ItemIds.Wood), Count(setup.Settlement, ItemIds.Coin)));
        Assert.Equal((1, 12), (Count(setup.Cargo, ItemIds.Wood), Count(setup.Cargo, ItemIds.Coin)));
    }

    [Fact]
    public void FiniteStockExhaustionAndRejectedTradesDoNotMutateAnyContainer()
    {
        var setup = Create(settlementWood: 2, settlementCoin: 20, caravanWood: 2, caravanCoin: 10);
        var visit = setup.Visits.TryOpen("visit-1", setup.Cargo).Visit!;
        setup.Visits.TryArrive(visit);

        Assert.Equal(CaravanVisitStatus.Success,
            setup.Visits.TryTrade(visit, MarketTradeSide.Purchase, ItemIds.Wood, 2).Status);
        Assert.Equal(24, Count(setup.Settlement, ItemIds.Coin));
        Assert.Equal(0, Count(setup.Settlement, ItemIds.Wood));

        var before = Snapshot(setup.Settlement, setup.Cargo);
        var exhausted = setup.Visits.TryTrade(visit, MarketTradeSide.Purchase, ItemIds.Wood, 1);
        var invalid = setup.Visits.TryTrade(visit, (MarketTradeSide)99, ItemIds.Wood, 1);
        Assert.Equal(CaravanVisitStatus.TradeRejected, exhausted.Status);
        Assert.Equal(MarketTransactionStatus.InsufficientMarketGoods, exhausted.Transaction?.Status);
        Assert.Equal(CaravanVisitStatus.TradeRejected, invalid.Status);
        Assert.Equal(MarketTransactionStatus.InvalidRequest, invalid.Transaction?.Status);
        Assert.Equal(before, Snapshot(setup.Settlement, setup.Cargo));

        var duplicateId = setup.Visits.TryOpen("visit-1", setup.Cargo);
        Assert.Equal(CaravanVisitStatus.DuplicateVisit, duplicateId.Status);
        Assert.Equal(before, Snapshot(setup.Settlement, setup.Cargo));
    }

    [Fact]
    public void CancellationClosesOnceAndAllowsASeparateVisit()
    {
        var setup = Create();
        var first = setup.Visits.TryOpen("visit-1", setup.Cargo).Visit!;
        setup.Visits.TryArrive(first);
        Assert.Equal(CaravanVisitStatus.Success, setup.Visits.TryCancel(first).Status);
        Assert.Equal(CaravanVisitState.Cancelled, first.State);

        var beforeRejectedRequests = Snapshot(setup.Settlement, setup.Cargo);
        Assert.Equal(CaravanVisitStatus.AlreadyCancelled,
            setup.Visits.TryCancel(first).Status);
        Assert.Equal(CaravanVisitStatus.AlreadyCancelled,
            setup.Visits.TryTrade(first, MarketTradeSide.Purchase, ItemIds.Wood, 1).Status);
        Assert.Equal(CaravanVisitStatus.AlreadyCancelled, setup.Visits.TryDepart(first).Status);
        Assert.Equal(CaravanVisitStatus.DuplicateVisit,
            setup.Visits.TryOpen("visit-1", setup.Cargo).Status);
        Assert.Equal(CaravanVisitStatus.CargoAlreadyUsed,
            setup.Visits.TryOpen("visit-2", setup.Cargo).Status);
        Assert.Equal(beforeRejectedRequests, Snapshot(setup.Settlement, setup.Cargo));

        var second = setup.Visits.TryOpen("visit-3", CreateCargo("next-caravan", wood: 1, coin: 5)).Visit!;
        Assert.Equal(CaravanVisitStatus.Success, setup.Visits.TryArrive(second).Status);
        Assert.Equal(CaravanVisitStatus.Success,
            setup.Visits.TryTrade(second, MarketTradeSide.Purchase, ItemIds.Wood, 1).Status);
    }

    [Fact]
    public void InvalidManifestAndUnavailableMarketAreRejected()
    {
        var setup = Create();
        var beforeInvalidRequest = Snapshot(setup.Settlement, setup.Cargo);
        Assert.Equal(CaravanVisitStatus.InvalidRequest, setup.Visits.TryOpen(null, setup.Cargo).Status);
        Assert.Equal(CaravanVisitStatus.InvalidRequest, setup.Visits.TryArrive(null).Status);
        Assert.Equal(CaravanVisitStatus.InvalidRequest,
            setup.Visits.TryTrade(null, MarketTradeSide.Purchase, ItemIds.Wood, 1).Status);
        Assert.Equal(CaravanVisitStatus.InvalidRequest, setup.Visits.TryDepart(null).Status);
        Assert.Equal(CaravanVisitStatus.InvalidRequest, setup.Visits.TryCancel(null).Status);
        Assert.Equal(beforeInvalidRequest, Snapshot(setup.Settlement, setup.Cargo));

        var coinOnly = new ResourceContainer("coin-only", 5);
        coinOnly.TryAdd(ItemIds.Coin, 1);
        var before = Snapshot(setup.Settlement, setup.Cargo, coinOnly);
        Assert.Equal(CaravanVisitStatus.InvalidCargo,
            setup.Visits.TryOpen("invalid", coinOnly).Status);
        Assert.Equal(before, Snapshot(setup.Settlement, setup.Cargo, coinOnly));

        var beforeMarketRemoval = Snapshot(setup.Settlement, setup.Cargo);
        Assert.True(setup.World.TryRemoveStockpile("market"));
        Assert.Equal(CaravanVisitStatus.InvalidMarket,
            setup.Visits.TryOpen("visit-2", CreateCargo("next-caravan", wood: 1, coin: 1)).Status);
        Assert.Equal(beforeMarketRemoval, Snapshot(setup.Settlement, setup.Cargo));
    }

    [Fact]
    public void ArrivalRejectsRemovedMarketWithoutMutatingCargo()
    {
        var setup = Create();
        var visit = setup.Visits.TryOpen("visit-1", setup.Cargo).Visit!;
        var before = Snapshot(setup.Settlement, setup.Cargo);

        Assert.True(setup.World.TryRemoveStockpile("market"));
        Assert.Equal(CaravanVisitStatus.InvalidMarket, setup.Visits.TryArrive(visit).Status);
        Assert.Equal(CaravanVisitState.Open, visit.State);
        Assert.Equal(before, Snapshot(setup.Settlement, setup.Cargo));
    }

    [Fact]
    public void ResetCancelsTransientVisitWithoutChangingCargoAndInvalidatesItsId()
    {
        var setup = Create();
        var visit = setup.Visits.TryOpen("visit-1", setup.Cargo).Visit!;
        var before = Snapshot(setup.Settlement, setup.Cargo);

        setup.Visits.Reset();

        Assert.Equal(CaravanVisitState.Cancelled, visit.State);
        Assert.Equal(CaravanVisitStatus.AlreadyCancelled,
            setup.Visits.TryArrive(visit).Status);
        Assert.Equal(CaravanVisitStatus.DuplicateVisit,
            setup.Visits.TryOpen("visit-1", setup.Cargo).Status);
        Assert.Equal(CaravanVisitStatus.Success,
            setup.Visits.TryOpen("visit-2", CreateCargo("next-caravan", wood: 1, coin: 1)).Status);
        Assert.Equal(before, Snapshot(setup.Settlement, setup.Cargo));
    }

    [Fact]
    public void IdenticalVisitInputsAndOperationOrderProduceIdenticalResultsAndTotals()
    {
        var first = RunScenario();
        var second = RunScenario();
        Assert.Equal(
            (first.Arrival, first.Purchase, first.Sale, first.Departure),
            (second.Arrival, second.Purchase, second.Sale, second.Departure));
        Assert.Equal(first.SettlementTotals, second.SettlementTotals);
        Assert.Equal(first.CaravanTotals, second.CaravanTotals);
    }

    private static ScenarioResult RunScenario()
    {
        var setup = Create();
        var visit = setup.Visits.TryOpen("fixed-visit", setup.Cargo).Visit!;
        var arrival = setup.Visits.TryArrive(visit);
        var purchase = setup.Visits.TryTrade(visit, MarketTradeSide.Purchase, ItemIds.Wood, 2);
        var sale = setup.Visits.TryTrade(visit, MarketTradeSide.Sale, ItemIds.Wood, 1);
        var departure = setup.Visits.TryDepart(visit);

        return new ScenarioResult(
            arrival.Status,
            purchase.Transaction!.Value,
            sale.Transaction!.Value,
            departure.Status,
            ContainerTotals(setup.Settlement),
            ContainerTotals(setup.Cargo));
    }

    private static int Count(ResourceContainer container, string itemId) =>
        container.Counts.GetValueOrDefault(itemId);

    private static ResourceContainer CreateCargo(string name, int wood, int coin)
    {
        var cargo = new ResourceContainer(name, 10);
        AddIfPositive(cargo, ItemIds.Wood, wood);
        AddIfPositive(cargo, ItemIds.Coin, coin);
        return cargo;
    }

    private static void AddIfPositive(ResourceContainer container, string itemId, int quantity)
    {
        if (quantity > 0)
        {
            Assert.Equal(ResourceMutationResult.Success, container.TryAdd(itemId, quantity));
        }
    }

    private static int[] ContainerTotals(ResourceContainer container) =>
        [Count(container, ItemIds.Wood), Count(container, ItemIds.Coin), container.UsedCapacity];

    private static string[] Snapshot(params ResourceContainer[] containers) =>
        containers.SelectMany((container, index) =>
            new[]
            {
                $"container:{index}",
                $"capacity:{container.Capacity}",
                $"used:{container.UsedCapacity}",
                $"remaining:{container.RemainingCapacity}"
            }.Concat(container.Counts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}:{pair.Value}"))).ToArray();

    private static void AssertUnchanged(string[] before, params ResourceContainer[] containers) =>
        Assert.Equal(before, Snapshot(containers));
}
