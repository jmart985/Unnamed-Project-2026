namespace Simulation;

public enum CaravanVisitState
{
    Open,
    Arrived,
    Departed,
    Cancelled
}

public enum CaravanVisitStatus
{
    Success,
    InvalidRequest,
    InvalidCargo,
    CargoAlreadyUsed,
    InvalidMarket,
    DuplicateVisit,
    VisitInProgress,
    StaleVisit,
    NotArrived,
    AlreadyArrived,
    AlreadyDeparted,
    AlreadyCancelled,
    TradeRejected
}

public sealed class CaravanVisit
{
    internal CaravanVisit(string id, ResourceContainer cargo)
    {
        Id = id;
        Cargo = cargo;
    }

    public string Id { get; }
    public ResourceContainer Cargo { get; }
    public CaravanVisitState State { get; internal set; } = CaravanVisitState.Open;
}

public readonly record struct CaravanVisitResult(
    CaravanVisitStatus Status,
    CaravanVisit? Visit = null,
    MarketTransactionResult? Transaction = null)
{
    public bool Succeeded => Status == CaravanVisitStatus.Success;
}

/// <summary>Runs one explicit, transient caravan visit against a settlement market.</summary>
public sealed class CaravanVisitService : ITransientSimulationState
{
    private readonly SettlementMarket _market;
    private readonly Dictionary<string, CaravanVisit> _visits = new(StringComparer.Ordinal);
    private CaravanVisit? _activeVisit;

    public CaravanVisitService(SettlementMarket market)
    {
        ArgumentNullException.ThrowIfNull(market);
        _market = market;
    }

    public CaravanVisitResult TryOpen(string? visitId, ResourceContainer? cargo)
    {
        if (string.IsNullOrWhiteSpace(visitId) || cargo is null)
        {
            return Reject(CaravanVisitStatus.InvalidRequest);
        }

        if (_visits.ContainsKey(visitId))
        {
            return Reject(CaravanVisitStatus.DuplicateVisit);
        }

        if (_activeVisit is not null)
        {
            return Reject(CaravanVisitStatus.VisitInProgress);
        }

        if (!_market.IsAvailable)
        {
            return Reject(CaravanVisitStatus.InvalidMarket);
        }

        if (_visits.Values.Any(existing => ReferenceEquals(existing.Cargo, cargo)))
        {
            return Reject(CaravanVisitStatus.CargoAlreadyUsed);
        }

        if (!_market.IsValidCounterparty(cargo) || !HasValidManifest(cargo))
        {
            return Reject(CaravanVisitStatus.InvalidCargo);
        }

        var visit = new CaravanVisit(visitId, cargo);
        _visits.Add(visit.Id, visit);
        _activeVisit = visit;
        return new CaravanVisitResult(CaravanVisitStatus.Success, visit);
    }

    public CaravanVisitResult TryArrive(CaravanVisit? visit)
    {
        var validation = ValidateVisit(visit);
        if (validation.Status != CaravanVisitStatus.Success)
        {
            return validation;
        }

        if (visit!.State == CaravanVisitState.Arrived)
        {
            return Reject(CaravanVisitStatus.AlreadyArrived, visit);
        }

        if (visit.State != CaravanVisitState.Open)
        {
            return Reject(GetClosedStatus(visit.State), visit);
        }

        if (!_market.IsAvailable)
        {
            return Reject(CaravanVisitStatus.InvalidMarket, visit);
        }

        visit.State = CaravanVisitState.Arrived;
        return new CaravanVisitResult(CaravanVisitStatus.Success, visit);
    }

    public CaravanVisitResult TryTrade(
        CaravanVisit? visit,
        MarketTradeSide side,
        string? resourceId,
        int quantity)
    {
        var validation = ValidateVisit(visit);
        if (validation.Status != CaravanVisitStatus.Success)
        {
            return validation;
        }

        if (visit!.State == CaravanVisitState.Open)
        {
            return Reject(CaravanVisitStatus.NotArrived, visit);
        }

        if (visit.State != CaravanVisitState.Arrived)
        {
            return Reject(GetClosedStatus(visit.State), visit);
        }

        var transaction = _market.TryExecute(
            new MarketTransactionRequest(side, resourceId, quantity, visit.Cargo));
        return transaction.Succeeded
            ? new CaravanVisitResult(CaravanVisitStatus.Success, visit, transaction)
            : new CaravanVisitResult(CaravanVisitStatus.TradeRejected, visit, transaction);
    }

    public CaravanVisitResult TryDepart(CaravanVisit? visit)
    {
        var validation = ValidateVisit(visit);
        if (validation.Status != CaravanVisitStatus.Success)
        {
            return validation;
        }

        if (visit!.State != CaravanVisitState.Arrived)
        {
            return Reject(
                visit.State == CaravanVisitState.Open
                    ? CaravanVisitStatus.NotArrived
                    : GetClosedStatus(visit.State),
                visit);
        }

        visit.State = CaravanVisitState.Departed;
        _activeVisit = null;
        return new CaravanVisitResult(CaravanVisitStatus.Success, visit);
    }

    public CaravanVisitResult TryCancel(CaravanVisit? visit)
    {
        var validation = ValidateVisit(visit);
        if (validation.Status != CaravanVisitStatus.Success)
        {
            return validation;
        }

        if (visit!.State is CaravanVisitState.Departed or CaravanVisitState.Cancelled)
        {
            return Reject(GetClosedStatus(visit.State), visit);
        }

        visit.State = CaravanVisitState.Cancelled;
        _activeVisit = null;
        return new CaravanVisitResult(CaravanVisitStatus.Success, visit);
    }

    public void Reset()
    {
        if (_activeVisit is not null)
        {
            _activeVisit.State = CaravanVisitState.Cancelled;
            _activeVisit = null;
        }
    }

    private bool HasValidManifest(ResourceContainer cargo)
    {
        var hasTradeGoods = false;
        var hasCoin = false;
        foreach (var (resourceId, quantity) in cargo.Counts)
        {
            if (resourceId == ItemIds.Coin)
            {
                hasCoin = quantity > 0;
            }
            else if (_market.IsTradeable(resourceId))
            {
                hasTradeGoods |= quantity > 0;
            }
            else
            {
                return false;
            }
        }

        return hasTradeGoods && hasCoin;
    }

    private CaravanVisitResult ValidateVisit(CaravanVisit? visit)
    {
        if (visit is null)
        {
            return Reject(CaravanVisitStatus.InvalidRequest);
        }

        if (!_visits.TryGetValue(visit.Id, out var registered) || !ReferenceEquals(registered, visit))
        {
            return Reject(CaravanVisitStatus.StaleVisit);
        }

        return new CaravanVisitResult(CaravanVisitStatus.Success, visit);
    }

    private static CaravanVisitStatus GetClosedStatus(CaravanVisitState state) => state switch
    {
        CaravanVisitState.Departed => CaravanVisitStatus.AlreadyDeparted,
        CaravanVisitState.Cancelled => CaravanVisitStatus.AlreadyCancelled,
        _ => CaravanVisitStatus.InvalidRequest
    };

    private static CaravanVisitResult Reject(CaravanVisitStatus status, CaravanVisit? visit = null) =>
        new(status, visit);
}
