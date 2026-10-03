namespace Simulation;

public enum MarketTradeSide
{
    /// <summary>The counterparty buys goods from the market and pays coin.</summary>
    Purchase,
    /// <summary>The counterparty sells goods to the market and receives coin.</summary>
    Sale
}

public enum MarketTransactionStatus
{
    Success,
    InvalidRequest,
    InvalidMarket,
    InvalidCounterparty,
    NotTradeable,
    InvalidQuantity,
    ArithmeticOverflow,
    InsufficientMarketGoods,
    InsufficientMarketCoin,
    InsufficientCounterpartyGoods,
    InsufficientCounterpartyCoin,
    MarketCapacityExceeded,
    CounterpartyCapacityExceeded
}

public sealed record MarketTransactionRequest(
    MarketTradeSide Side,
    string? ResourceId,
    int Quantity,
    ResourceContainer? Counterparty);

public readonly record struct MarketTransactionResult(
    MarketTransactionStatus Status,
    int Quantity,
    int CoinAmount)
{
    public bool Succeeded => Status == MarketTransactionStatus.Success;
}

/// <summary>
/// Fixed-price market over an authoritative World stockpile. One rule handles purchase and sale:
/// goods and coin move in opposite directions in a single atomic exchange.
/// </summary>
public sealed class SettlementMarket
{
    private readonly World _world;
    private readonly TradeGoodsCatalog _catalog;
    private readonly string _marketLocationId;

    public SettlementMarket(World world, TradeGoodsCatalog catalog, string marketLocationId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(marketLocationId))
        {
            throw new ArgumentException("A market stockpile ID is required.", nameof(marketLocationId));
        }

        _world = world;
        _catalog = catalog;
        _marketLocationId = marketLocationId;
    }

    public MarketTransactionResult TryExecute(MarketTransactionRequest? request)
    {
        if (request is null || !Enum.IsDefined(request.Side))
        {
            return Reject(MarketTransactionStatus.InvalidRequest);
        }

        // Resolved on every call so a removed market stockpile is rejected, not mutated.
        var market = _world.Stockpiles.FirstOrDefault(pile =>
            string.Equals(pile.Id, _marketLocationId, StringComparison.Ordinal))?.Contents;
        if (market is null)
        {
            return Reject(MarketTransactionStatus.InvalidMarket);
        }

        var counterparty = request.Counterparty;
        if (counterparty is null || ReferenceEquals(counterparty, market))
        {
            return Reject(MarketTransactionStatus.InvalidCounterparty);
        }

        if (!_catalog.TryGetCoinValue(request.ResourceId, out var unitValue))
        {
            return Reject(MarketTransactionStatus.NotTradeable);
        }

        if (request.Quantity <= 0)
        {
            return Reject(MarketTransactionStatus.InvalidQuantity);
        }

        var total = (long)unitValue * request.Quantity;
        if (total > int.MaxValue)
        {
            return Reject(MarketTransactionStatus.ArithmeticOverflow);
        }

        var coinAmount = (int)total;
        var resourceId = request.ResourceId!;
        var purchase = request.Side == MarketTradeSide.Purchase;
        var goodsSource = purchase ? market : counterparty;
        var coinSource = purchase ? counterparty : market;

        var exchange = ResourceTransferService.TryExchange(
            goodsSource,
            coinSource,
            resourceId,
            request.Quantity,
            ItemIds.Coin,
            coinAmount);

        var status = exchange switch
        {
            ResourceExchangeResult.Success => MarketTransactionStatus.Success,
            ResourceExchangeResult.InsufficientFirst => purchase
                ? MarketTransactionStatus.InsufficientMarketGoods
                : MarketTransactionStatus.InsufficientCounterpartyGoods,
            ResourceExchangeResult.InsufficientSecond => purchase
                ? MarketTransactionStatus.InsufficientCounterpartyCoin
                : MarketTransactionStatus.InsufficientMarketCoin,
            ResourceExchangeResult.CapacityExceededFirst => purchase
                ? MarketTransactionStatus.MarketCapacityExceeded
                : MarketTransactionStatus.CounterpartyCapacityExceeded,
            ResourceExchangeResult.CapacityExceededSecond => purchase
                ? MarketTransactionStatus.CounterpartyCapacityExceeded
                : MarketTransactionStatus.MarketCapacityExceeded,
            _ => MarketTransactionStatus.InvalidRequest
        };

        return status == MarketTransactionStatus.Success
            ? new MarketTransactionResult(status, request.Quantity, coinAmount)
            : Reject(status);
    }

    private static MarketTransactionResult Reject(MarketTransactionStatus status) => new(status, 0, 0);
}
