namespace Simulation;

public enum CaravanVisitStatus
{
    Opened,
    Traded,
    Departed,
    Cancelled,
    InvalidRequest,
    InvalidMarket,
    InvalidCaravan,
    DuplicateVisit,
    DuplicateCaravan,
    StaleVisit,
    TradeRejected,
    AlreadyDeparted,
    AlreadyCancelled,
    Invalidated
}

public readonly record struct CaravanVisitResult(
    CaravanVisitStatus Status,
    MarketTransactionResult? Transaction = null)
{
    public bool Succeeded => Status is
        CaravanVisitStatus.Opened or
        CaravanVisitStatus.Traded or
        CaravanVisitStatus.Departed or
        CaravanVisitStatus.Cancelled;
}

/// <summary>
/// Owns transient, deterministic visits to settlement markets. Caravan cargo stays in its
/// bounded resource container; visits never copy cargo or reserve market resources.
/// </summary>
public sealed class CaravanVisitService : ITransientSimulationState
{
    private readonly World _world;
    private readonly Dictionary<string, Visit> _visits = new(StringComparer.Ordinal);
    private readonly Dictionary<ResourceContainer, string> _activeCaravans =
        new(ReferenceEqualityComparer.Instance);

    internal CaravanVisitService(World world)
    {
        _world = world;
    }

    public CaravanVisitResult TryOpen(
        string? visitId,
        SettlementMarket? market,
        ResourceContainer? caravan)
    {
        if (string.IsNullOrWhiteSpace(visitId))
        {
            return Result(CaravanVisitStatus.InvalidRequest);
        }

        if (_visits.ContainsKey(visitId))
        {
            return Result(CaravanVisitStatus.DuplicateVisit);
        }

        if (market is null || !market.IsAvailableFor(_world))
        {
            return Result(CaravanVisitStatus.InvalidMarket);
        }

        if (caravan is null || market.IsMarketContainer(caravan))
        {
            return Result(CaravanVisitStatus.InvalidCaravan);
        }

        if (_activeCaravans.ContainsKey(caravan))
        {
            return Result(CaravanVisitStatus.DuplicateCaravan);
        }

        _visits.Add(visitId, new Visit(market, caravan));
        _activeCaravans.Add(caravan, visitId);
        return Result(CaravanVisitStatus.Opened);
    }

    public CaravanVisitResult TryTrade(
        string? visitId,
        MarketTradeSide side,
        string? resourceId,
        int quantity)
    {
        if (string.IsNullOrWhiteSpace(visitId))
        {
            return Result(CaravanVisitStatus.InvalidRequest);
        }

        if (!TryGetActiveVisit(visitId, out var visit, out var closedStatus))
        {
            return Result(closedStatus);
        }

        var transaction = visit.Market!.TryExecute(
            new MarketTransactionRequest(side, resourceId, quantity, visit.Caravan));
        return transaction.Succeeded
            ? new CaravanVisitResult(CaravanVisitStatus.Traded, transaction)
            : new CaravanVisitResult(CaravanVisitStatus.TradeRejected, transaction);
    }

    public CaravanVisitResult TryDepart(string? visitId) =>
        TryClose(visitId, VisitState.Departed, CaravanVisitStatus.Departed);

    public CaravanVisitResult TryCancel(string? visitId) =>
        TryClose(visitId, VisitState.Cancelled, CaravanVisitStatus.Cancelled);

    public void Reset()
    {
        foreach (var visit in _visits.Values)
        {
            if (visit.State == VisitState.Active)
            {
                visit.Close(VisitState.Invalidated);
            }
        }

        _activeCaravans.Clear();
    }

    private CaravanVisitResult TryClose(
        string? visitId,
        VisitState terminalState,
        CaravanVisitStatus successStatus)
    {
        if (string.IsNullOrWhiteSpace(visitId))
        {
            return Result(CaravanVisitStatus.InvalidRequest);
        }

        if (!_visits.TryGetValue(visitId, out var visit))
        {
            return Result(CaravanVisitStatus.StaleVisit);
        }

        if (visit.State != VisitState.Active)
        {
            return Result(GetClosedStatus(visit.State));
        }

        if (visit.Caravan is { } caravan)
        {
            _activeCaravans.Remove(caravan);
        }

        visit.Close(terminalState);
        return Result(successStatus);
    }

    private bool TryGetActiveVisit(
        string visitId,
        out Visit visit,
        out CaravanVisitStatus closedStatus)
    {
        if (!_visits.TryGetValue(visitId, out visit!))
        {
            closedStatus = CaravanVisitStatus.StaleVisit;
            return false;
        }

        if (visit.State == VisitState.Active)
        {
            closedStatus = default;
            return true;
        }

        closedStatus = GetClosedStatus(visit.State);
        return false;
    }

    private static CaravanVisitStatus GetClosedStatus(VisitState state) => state switch
    {
        VisitState.Departed => CaravanVisitStatus.AlreadyDeparted,
        VisitState.Cancelled => CaravanVisitStatus.AlreadyCancelled,
        _ => CaravanVisitStatus.Invalidated
    };

    private static CaravanVisitResult Result(CaravanVisitStatus status) => new(status);

    private enum VisitState
    {
        Active,
        Departed,
        Cancelled,
        Invalidated
    }

    private sealed class Visit(SettlementMarket market, ResourceContainer caravan)
    {
        public SettlementMarket? Market { get; private set; } = market;
        public ResourceContainer? Caravan { get; private set; } = caravan;
        public VisitState State { get; private set; } = VisitState.Active;

        public void Close(VisitState state)
        {
            State = state;
            Market = null;
            Caravan = null;
        }
    }
}
