using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class EconomyScenarioTests
{
    [Fact]
    public void ProductionToCaravanTradeIsRepeatableAndRecoversWithoutResourceLoss()
    {
        Assert.Equal(RunScenario(), RunScenario());
    }

    private static string RunScenario()
    {
        var species = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var catalog = TradeGoodsCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "trade_goods.json"));
        var world = new World();
        var worker = world.SpawnEntity("human", Vector3.Zero);
        var inputs = AddStorage(world, "inputs", 10);
        var outputs = AddStorage(world, "outputs", 10);
        var settlement = AddStorage(world, "market", 40);
        Add(inputs, ItemIds.Wood, 2);
        Add(settlement, ItemIds.Coin, 10);

        var queue = new SettlementWorkOrderQueue();
        var order = new SettlementWorkOrder("meal-order", "recipe", 1, "meal");
        Assert.True(queue.TryEnqueue(order));
        Assert.Same(order, queue.NextAvailable());
        Assert.True(order.TryClaim(worker.Id));

        // Farming returns a finite yield; the existing scenario boundary deposits it.
        // Farm plots and work orders are not durable World snapshot state.
        var farm = new FarmPlot("plot", 1);
        Assert.True(farm.TryPlant(new CropDefinition("wheat", 5, 3), 1));
        Assert.Equal(0, farm.Harvest());
        Assert.True(farm.AdvanceGrowth(2));
        Assert.True(farm.AdvanceGrowth(3));
        var harvested = farm.Harvest();
        Assert.Equal(3, harvested);
        Add(inputs, ItemIds.Food, harvested);
        Assert.Equal(0, farm.Harvest());

        var recipe = new WorkstationRecipe("meal",
            new Dictionary<string, int> { [ItemIds.Food] = 3, [ItemIds.Wood] = 2 },
            new Dictionary<string, int> { [ItemIds.Food] = 6 }, 2);
        var beforeProduction = WorldSnapshotSerializer.Serialize(world, 0);
        Assert.Equal(RecipeExecutionResult.IncompleteRecipe,
            WorkstationExecutionService.TryExecute(recipe, inputs, outputs));
        Assert.Equal(beforeProduction, WorldSnapshotSerializer.Serialize(world, 0));
        Assert.True(recipe.ApplyWork(1));
        Assert.False(recipe.IsComplete);
        Assert.True(recipe.ApplyWork(1));
        Assert.Equal(RecipeExecutionResult.Success,
            WorkstationExecutionService.TryExecute(recipe, inputs, outputs));
        Assert.True(order.TryComplete(worker.Id));
        Assert.Equal(WorkOrderStatus.Completed, order.Status);
        Assert.Null(queue.NextAvailable());
        Assert.Empty(inputs.Counts);
        Assert.Equal(6, Count(outputs, ItemIds.Food));
        Assert.Equal(ResourceMutationResult.Success,
            ResourceTransferService.TryTransfer(outputs, settlement, ItemIds.Food, 6));
        Assert.Empty(outputs.Counts);

        var cargo = new ResourceContainer("caravan", 13);
        Add(cargo, ItemIds.Food, 1);
        Add(cargo, ItemIds.Coin, 12);
        var visits = new CaravanVisitService(new SettlementMarket(world, catalog, "market"));
        var opened = visits.TryOpen("fixed-visit", cargo);
        Assert.Equal(CaravanVisitStatus.Success, opened.Status);
        var visit = Assert.IsType<CaravanVisit>(opened.Visit);
        Assert.Equal(CaravanVisitStatus.Success, visits.TryArrive(visit).Status);

        var beforeInvalidData = State(world, cargo);
        var originalDefinitions = catalog.TradeGoods.ToArray();
        using (var invalid = new MemoryStream(Encoding.UTF8.GetBytes(
            "[{\"resourceId\":\"food\",\"coinValue\":99},{\"resourceId\":\"coin\",\"coinValue\":1}]")))
        {
            Assert.Throws<InvalidDataException>(() => TradeGoodsCatalog.Load(invalid));
        }
        Assert.Equal(originalDefinitions, catalog.TradeGoods);
        Assert.True(catalog.TryGetCoinValue(ItemIds.Food, out var foodValue));
        Assert.Equal(2, foodValue);
        Assert.Equal(beforeInvalidData, State(world, cargo));

        RejectTrade(MarketTradeSide.Purchase, ItemIds.Food, 7, MarketTransactionStatus.InsufficientMarketGoods);
        RejectTrade(MarketTradeSide.Purchase, ItemIds.Food, 0, MarketTransactionStatus.InvalidQuantity);
        RejectTrade(MarketTradeSide.Purchase, ItemIds.Coin, 1, MarketTransactionStatus.NotTradeable);

        var trade = visits.TryTrade(visit, MarketTradeSide.Purchase, ItemIds.Food, 3);
        Assert.Equal(CaravanVisitStatus.Success, trade.Status);
        Assert.Equal(new MarketTransactionResult(MarketTransactionStatus.Success, 3, 6), trade.Transaction);
        Assert.Equal((3, 16), (Count(settlement, ItemIds.Food), Count(settlement, ItemIds.Coin)));
        Assert.Equal((4, 6), (Count(cargo, ItemIds.Food), Count(cargo, ItemIds.Coin)));
        Assert.Equal(7, Count(settlement, ItemIds.Food) + Count(cargo, ItemIds.Food));
        Assert.Equal(22, Count(settlement, ItemIds.Coin) + Count(cargo, ItemIds.Coin));
        RejectTrade(MarketTradeSide.Sale, ItemIds.Food, 4, MarketTransactionStatus.CounterpartyCapacityExceeded);
        RejectTrade(MarketTradeSide.Sale, ItemIds.Food, 5, MarketTransactionStatus.InsufficientCounterpartyGoods);

        Assert.Equal(CaravanVisitStatus.Success, visits.TryDepart(visit).Status);
        var afterDeparture = State(world, cargo);
        Assert.Equal(CaravanVisitStatus.AlreadyDeparted,
            visits.TryTrade(visit, MarketTradeSide.Purchase, ItemIds.Food, 1).Status);
        Assert.Equal(CaravanVisitStatus.AlreadyDeparted, visits.TryDepart(visit).Status);
        Assert.Equal(afterDeparture, State(world, cargo));

        const double simulationTime = 12.5;
        var snapshot = WorldSnapshotSerializer.Serialize(world, simulationTime);
        var invalidSnapshot = JsonNode.Parse(snapshot)!.AsObject();
        invalidSnapshot["Stockpiles"]!.AsArray().Single(node => node!["Id"]!.GetValue<string>() == "market")!
            ["Contents"]!["Counts"]![ItemIds.Coin] = -1;
        var reset = new ResetProbe();
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            world, invalidSnapshot.ToJsonString(), species, reset));
        Assert.Equal(0, reset.ResetCount);
        Assert.Same(worker, Assert.Single(world.Entities));
        Assert.Same(settlement, world.Stockpiles.Single(pile => pile.Id == "market").Contents);
        Assert.Equal(snapshot, WorldSnapshotSerializer.Serialize(world, simulationTime));
        Assert.Equal(afterDeparture, State(world, cargo));

        var restoredTime = WorldSnapshotSerializer.Load(world, snapshot, species, visits);
        Assert.Equal(simulationTime, restoredTime);
        Assert.Equal(snapshot, WorldSnapshotSerializer.Serialize(world, restoredTime));
        var restoredMarket = world.Stockpiles.Single(pile => pile.Id == "market").Contents;
        Assert.Equal((3, 16), (Count(restoredMarket, ItemIds.Food), Count(restoredMarket, ItemIds.Coin)));
        Assert.Equal(CaravanVisitState.Departed, visit.State);
        Assert.Equal(CaravanVisitStatus.AlreadyDeparted,
            visits.TryTrade(visit, MarketTradeSide.Purchase, ItemIds.Food, 1).Status);
        Assert.Equal(afterDeparture, State(world, cargo));
        return $"{harvested}:{recipe.WorkProgress}:{order.Status}:{trade.Transaction}:{visit.State}:{snapshot}:{State(world, cargo)}";

        void RejectTrade(MarketTradeSide side, string resourceId, int quantity, MarketTransactionStatus expected)
        {
            var before = State(world, cargo);
            var rejected = visits.TryTrade(visit, side, resourceId, quantity);
            Assert.Equal(CaravanVisitStatus.TradeRejected, rejected.Status);
            Assert.Equal(new MarketTransactionResult(expected, 0, 0), rejected.Transaction);
            Assert.Equal(CaravanVisitState.Arrived, visit.State);
            Assert.Equal(before, State(world, cargo));
        }
    }

    private static ResourceContainer AddStorage(World world, string id, int capacity)
    {
        var container = new ResourceContainer(id, capacity);
        world.AddStockpile(id, Vector3.Zero, container);
        return container;
    }

    private static void Add(ResourceContainer container, string itemId, int quantity) =>
        Assert.Equal(ResourceMutationResult.Success, container.TryAdd(itemId, quantity));

    private static int Count(ResourceContainer container, string itemId) => container.Counts.GetValueOrDefault(itemId);

    private static string State(World world, ResourceContainer cargo) =>
        WorldSnapshotSerializer.Serialize(world, 0) + ":" + cargo.Capacity + ":" + cargo.UsedCapacity + ":" +
        string.Join(",", cargo.Counts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));

    private sealed class ResetProbe : ITransientSimulationState
    {
        public int ResetCount { get; private set; }
        public void Reset() => ResetCount++;
    }
}
