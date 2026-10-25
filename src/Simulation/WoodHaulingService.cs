using System.Globalization;
using System.Numerics;

namespace Simulation;

public enum WoodHaulingStatus
{
    NotHauler,
    NeedsFood,
    TravelingToSource,
    TravelingToStockpile,
    Delivered,
    NoWork,
    Waiting,
    RetryDeferred,
    ActionRejected,
    InvalidEntity,
    InvalidSpecies,
    Resting
}

public sealed record WoodHaulingResult(
    WoodHaulingStatus Status,
    ResourceLocation? Source = null,
    ResourceLocation? Stockpile = null,
    int Amount = 0,
    string? WaitingReason = null);

public sealed class WoodHaulingService : ITransientSimulationState
{
    private const double NavigationTimeoutSeconds = 30;
    private const double ArrivalTolerance = 0.1;
    public const int DefaultRetryDelayTicks = RetryScheduler.DefaultDelayTicks;

    private readonly World _world;
    private readonly Func<EntityRecord, INavigationAdapter> _navigationForEntity;
    private readonly RetryScheduler _retryScheduler;
    private readonly Dictionary<string, bool> _waitingForDelivery = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WoodHaulingResult> _waitingContexts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActiveTrip> _activeTrips = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActiveHomeTrip> _activeHomeTrips = new(StringComparer.Ordinal);
    private long _nextActionId;

    public WoodHaulingService(
        World world,
        Func<EntityRecord, INavigationAdapter> navigationForEntity,
        int retryDelayTicks = DefaultRetryDelayTicks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(navigationForEntity);

        _world = world;
        _navigationForEntity = navigationForEntity;
        _retryScheduler = new RetryScheduler(retryDelayTicks);
    }

    public WoodHaulingResult Advance(EntityRecord entity, SpeciesDefinition species, long simulationTick, double simulationTime = 0)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(species);
        if (simulationTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationTick), "Simulation tick cannot be negative.");
        }

        if (!OwnsEntity(entity))
        {
            return new WoodHaulingResult(WoodHaulingStatus.InvalidEntity);
        }

        if (!string.Equals(entity.SpeciesId, species.Id, StringComparison.Ordinal))
        {
            return new WoodHaulingResult(WoodHaulingStatus.InvalidSpecies);
        }

        if (!species.CanHaul)
        {
            return new WoodHaulingResult(WoodHaulingStatus.NotHauler);
        }

        var resting = ReconcileSchedule(entity, simulationTime);

        if (entity.Needs.IsHungry)
        {
            return new WoodHaulingResult(WoodHaulingStatus.NeedsFood, WaitingReason: entity.WaitingReason);
        }
        if (resting) return new WoodHaulingResult(WoodHaulingStatus.Resting);

        if (_activeTrips.TryGetValue(entity.Id, out var trip))
        {
            if (!IsCurrentAction(entity, trip))
            {
                CancelTrip(entity.Id);
            }
            else
            {
                _retryScheduler.Clear(entity.Id);
                return AdvanceTrip(entity, trip, simulationTick);
            }
        }

        if (entity.Actions.Current is not null)
        {
            return new WoodHaulingResult(WoodHaulingStatus.ActionRejected);
        }

        var hasCarriedWood = entity.Inventory.GetValueOrDefault(ItemIds.Wood) > 0;
        if (_retryScheduler.IsWaiting(entity.Id, simulationTick))
        {
            if (hasCarriedWood && !_waitingForDelivery.GetValueOrDefault(entity.Id))
            {
                ClearWaitingState(entity.Id);
            }
            else
            {
                if (_waitingContexts.TryGetValue(entity.Id, out var waitingContext))
                {
                    return waitingContext with { Status = WoodHaulingStatus.RetryDeferred };
                }

                return new WoodHaulingResult(
                    WoodHaulingStatus.RetryDeferred,
                    WaitingReason: entity.WaitingReason);
            }
        }

        return hasCarriedWood
            ? BeginDelivery(entity, simulationTick)
            : BeginPickup(entity, species, simulationTick);
    }

    // Call before navigation advances so an off-shift pickup cannot move or collect on the boundary tick.
    public bool ReconcileSchedule(EntityRecord entity, double simulationTime)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!OwnsEntity(entity)) return false;
        var resting = _world.HasDailySchedule(entity) && !_world.GetWorkSchedule(entity.Id).Evaluate(simulationTime).IsWorking;
        if (!resting)
        {
            CancelHomeTrip(entity.Id);
            if (entity.Activity is "Resting" or "Going home") entity.Activity = "Idle";
            return false;
        }
        if (entity.Inventory.GetValueOrDefault(ItemIds.Wood) > 0) return false;
        if (entity.Actions.Current?.Kind == ActionKind.Haul) entity.Actions.Cancel();
        ClearWaitingState(entity.Id);
        if (entity.Needs.IsHungry) return true;
        if (entity.Actions.Current is not null && !_activeHomeTrips.ContainsKey(entity.Id)) return true;
        if (_world.TryGetHomePosition(entity.Id, out var home) && Vector3.Distance(entity.Position, home) > 0.6f)
        {
            AdvanceHomeTrip(entity, home);
            return true;
        }
        CancelHomeTrip(entity.Id, completeAction: true);
        if (entity.Actions.Current is null)
        {
            entity.Activity = "Resting at home";
            entity.WaitingReason = null;
        }
        return true;
    }

    public void Reset()
    {
        foreach (var entityId in _activeTrips.Keys.ToArray())
        {
            if (_world.TryGetEntity(entityId, out var entity)
                && entity is not null
                && entity.Actions.Current?.Kind == ActionKind.Haul)
            {
                entity.Actions.Cancel();
            }
            else
            {
                CancelTrip(entityId);
            }
        }

        foreach (var entityId in _activeHomeTrips.Keys.ToArray()) CancelHomeTrip(entityId);
        _activeTrips.Clear();
        _activeHomeTrips.Clear();
        _nextActionId = 0;
        _retryScheduler.Reset();
        _waitingForDelivery.Clear();
        _waitingContexts.Clear();
    }

    private WoodHaulingResult BeginPickup(
        EntityRecord entity,
        SpeciesDefinition species,
        long simulationTick)
    {
        var sourceCandidates = _world.ResourcePiles
            .Where(source => source.Contents.Counts.GetValueOrDefault(ItemIds.Wood) > 0)
            .OrderBy(source => DistanceSquared(entity.Position, source.Position))
            .ThenBy(source => source.Id, StringComparer.Ordinal)
            .ToArray();

        if (sourceCandidates.Length == 0)
        {
            return Wait(entity, "No wood is available.", simulationTick, waitingForDelivery: false);
        }

        var individualTemperament = IndividualTemperament.ForEntity(entity.Id, species.Temperament);
        var preferredLoad = 1 + (int)Math.Round(individualTemperament.Boldness * 9, MidpointRounding.AwayFromZero);
        var remainingCapacity = Math.Min(RemainingCargoCapacity(entity), preferredLoad);
        if (remainingCapacity <= 0)
        {
            return Wait(entity, "The carried wood stack is full.", simulationTick, waitingForDelivery: false);
        }

        if (_world.Stockpiles.Count == 0)
        {
            return Wait(entity, "No stockpile is available.", simulationTick, waitingForDelivery: false);
        }

        var actionId = NextActionId(entity);
        if (!TryStartAction(entity, actionId))
        {
            return new WoodHaulingResult(WoodHaulingStatus.ActionRejected);
        }

        foreach (var source in sourceCandidates)
        {
            var stockpileCandidates = _world.Stockpiles
                .OrderBy(stockpile => DistanceSquared(source.Position, stockpile.Position))
                .ThenBy(stockpile => stockpile.Id, StringComparer.Ordinal);

            foreach (var stockpile in stockpileCandidates)
            {
                var maximumAmount = Math.Min(
                    _world.Reservations.GetAvailableResourceQuantity(source.Contents, ItemIds.Wood),
                    Math.Min(
                        remainingCapacity,
                        _world.Reservations.GetAvailableCapacity(stockpile.Contents)));
                if (maximumAmount <= 0)
                {
                    continue;
                }

                var sourceReservation = _world.Reservations.TryReserveResource(
                    entity.Id,
                    actionId,
                    source.Id,
                    source.Contents,
                    ItemIds.Wood,
                    maximumAmount);
                if (sourceReservation != ReservationResult.Reserved)
                {
                    continue;
                }

                var capacityReservation = _world.Reservations.TryReserveCapacity(
                    entity.Id,
                    actionId,
                    stockpile.Id,
                    stockpile.Contents,
                    maximumAmount);
                if (capacityReservation == ReservationResult.Reserved)
                {
                    return StartTravel(
                        entity,
                        new ActiveTrip(
                            actionId,
                            source,
                            stockpile,
                            maximumAmount,
                            TripStage.ToSource,
                            Navigator: null,
                            NavigationRequestId: null));
                }

                _world.Reservations.ReleaseForAction(entity.Id, actionId);
            }
        }

        entity.Actions.Cancel();
        return Wait(entity, "Wood or stockpile capacity is already reserved.", simulationTick, waitingForDelivery: false);
    }

    private WoodHaulingResult BeginDelivery(EntityRecord entity, long simulationTick)
    {
        var amount = entity.Inventory.GetValueOrDefault(ItemIds.Wood);
        var stockpiles = _world.Stockpiles
            .Where(target => _world.Reservations.GetAvailableCapacity(target.Contents) >= amount)
            .OrderBy(target => DistanceSquared(entity.Position, target.Position))
            .ThenBy(target => target.Id, StringComparer.Ordinal)
            .ToArray();

        if (stockpiles.Length == 0)
        {
            return Wait(entity, "No stockpile has enough capacity for carried wood.", simulationTick, waitingForDelivery: true);
        }

        var actionId = NextActionId(entity);
        if (!TryStartAction(entity, actionId))
        {
            return new WoodHaulingResult(WoodHaulingStatus.ActionRejected);
        }

        foreach (var stockpile in stockpiles)
        {
            var reservation = _world.Reservations.TryReserveCapacity(
                entity.Id,
                actionId,
                stockpile.Id,
                stockpile.Contents,
                amount);
            if (reservation == ReservationResult.Reserved)
            {
                return StartTravel(
                    entity,
                    new ActiveTrip(
                        actionId,
                        Source: null,
                        stockpile,
                        amount,
                        TripStage.ToStockpile,
                        Navigator: null,
                        NavigationRequestId: null));
            }
        }

        entity.Actions.Cancel();
        return Wait(entity, "Stockpile capacity is already reserved.", simulationTick, waitingForDelivery: true);
    }

    private WoodHaulingResult AdvanceTrip(EntityRecord entity, ActiveTrip trip, long simulationTick)
    {
        if (trip.Stockpile is null || !_world.Stockpiles.Contains(trip.Stockpile))
        {
            return FailTrip(entity, trip, "The stockpile target was removed.", simulationTick);
        }

        if (trip.Stage == TripStage.ToSource
            && (trip.Source is null || !_world.ResourcePiles.Contains(trip.Source)))
        {
            return FailTrip(entity, trip, "The wood source was removed.", simulationTick);
        }

        if (trip.Navigator is null || trip.NavigationRequestId is null)
        {
            throw new InvalidOperationException("An active haul trip must have a navigation request.");
        }

        var outcome = trip.Navigator.Poll(trip.NavigationRequestId, 0);
        if (outcome == NavigationOutcome.InProgress)
        {
            return ToTravelResult(trip);
        }

        var target = trip.Stage == TripStage.ToSource ? trip.Source! : trip.Stockpile;
        if (outcome != NavigationOutcome.Arrived)
        {
            return FailTrip(entity, trip, $"Navigation to '{target.Id}' failed: {outcome}.", simulationTick);
        }

        if (!IsWithinArrivalTolerance(entity.Position, target.Position))
        {
            return FailTrip(entity, trip, $"Navigation to '{target.Id}' reported arrival outside tolerance.", simulationTick);
        }

        if (trip.Stage == TripStage.ToSource)
        {
            var transfer = ResourceTransferService.TryPickUp(
                trip.Source!.Contents,
                entity,
                ItemIds.Wood,
                trip.Amount);
            if (transfer != ResourceMutationResult.Success)
            {
                return FailTrip(entity, trip, $"Wood pickup failed: {transfer}.", simulationTick);
            }

            _world.Reservations.ReleaseResourceReservation(
                entity.Id,
                trip.ActionId,
                trip.Source.Id,
                trip.Source.Contents,
                ItemIds.Wood);
            return StartTravel(entity, trip with
            {
                Stage = TripStage.ToStockpile,
                Navigator = null,
                NavigationRequestId = null
            });
        }

        var deposit = ResourceTransferService.TryDeliver(
            entity,
            trip.Stockpile.Contents,
            ItemIds.Wood,
            trip.Amount);
        if (deposit != ResourceMutationResult.Success)
        {
            return FailTrip(entity, trip, $"Wood deposit failed: {deposit}.", simulationTick);
        }

        entity.Actions.Complete();
        entity.Activity = "Idle";
        entity.WaitingReason = null;
        ClearWaitingState(entity.Id);
        return new WoodHaulingResult(
            WoodHaulingStatus.Delivered,
            trip.Source,
            trip.Stockpile,
            trip.Amount);
    }

    private WoodHaulingResult StartTravel(EntityRecord entity, ActiveTrip trip)
    {
        var target = trip.Stage == TripStage.ToSource ? trip.Source! : trip.Stockpile;
        var request = new NavigationRequest(target.Position, ArrivalTolerance, NavigationTimeoutSeconds);
        INavigationAdapter? navigator = null;
        string? requestId = null;
        var travelStarted = false;

        try
        {
            navigator = _navigationForEntity(entity)
                ?? throw new InvalidOperationException("The entity has no navigation adapter.");
            requestId = navigator.Request(request);
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new InvalidOperationException("The navigation adapter returned an empty request ID.");
            }

            _activeTrips[entity.Id] = trip with
            {
                Navigator = navigator,
                NavigationRequestId = requestId
            };
            travelStarted = true;
        }
        finally
        {
            if (!travelStarted)
            {
                if (requestId is not null && navigator is not null)
                {
                    navigator.Cancel(requestId);
                }

                entity.Actions.Cancel();
            }
        }

        entity.Activity = trip.Stage == TripStage.ToSource ? "SeekingWood" : "DeliveringWood";
        entity.WaitingReason = null;
        ClearWaitingState(entity.Id);
        return ToTravelResult(_activeTrips[entity.Id]);
    }

    private bool TryStartAction(EntityRecord entity, string actionId)
    {
        return entity.Actions.TryStart(
            new ActionCandidate(actionId, ActionKind.Haul),
            () => CancelTrip(entity.Id)) is ActionStartResult.Started or ActionStartResult.Preempted;
    }

    private WoodHaulingResult FailTrip(EntityRecord entity, ActiveTrip trip, string reason, long simulationTick)
    {
        entity.Actions.Cancel();
        var waiting = Wait(entity, reason, simulationTick, waitingForDelivery: trip.Stage == TripStage.ToStockpile);
        var withContext = waiting with
        {
            Source = trip.Source,
            Stockpile = trip.Stockpile,
            Amount = trip.Amount
        };
        _waitingContexts[entity.Id] = withContext;
        return withContext;
    }

    private WoodHaulingResult Wait(
        EntityRecord entity,
        string reason,
        long simulationTick,
        bool waitingForDelivery)
    {
        entity.Activity = "Waiting";
        entity.WaitingReason = reason;
        _retryScheduler.ScheduleRetry(entity.Id, simulationTick);
        _waitingForDelivery[entity.Id] = waitingForDelivery;
        var waiting = new WoodHaulingResult(WoodHaulingStatus.Waiting, WaitingReason: reason);
        _waitingContexts[entity.Id] = waiting;
        return waiting;
    }

    private void ClearWaitingState(string entityId)
    {
        _retryScheduler.Clear(entityId);
        _waitingForDelivery.Remove(entityId);
        _waitingContexts.Remove(entityId);
    }

    private static WoodHaulingResult ToTravelResult(ActiveTrip trip)
    {
        var status = trip.Stage == TripStage.ToSource
            ? WoodHaulingStatus.TravelingToSource
            : WoodHaulingStatus.TravelingToStockpile;
        return new WoodHaulingResult(status, trip.Source, trip.Stockpile, trip.Amount);
    }

    private string NextActionId(EntityRecord entity)
    {
        _nextActionId = checked(_nextActionId + 1);
        return $"haul-{entity.Id}-{_nextActionId.ToString("D6", CultureInfo.InvariantCulture)}";
    }

    private void CancelTrip(string entityId)
    {
        if (_activeTrips.Remove(entityId, out var trip)
            && trip.Navigator is not null
            && trip.NavigationRequestId is not null)
        {
            trip.Navigator.Cancel(trip.NavigationRequestId);
        }
    }

    private void AdvanceHomeTrip(EntityRecord entity, Vector3 home)
    {
        if (_activeHomeTrips.TryGetValue(entity.Id, out var active))
        {
            if (!string.Equals(entity.Actions.Current?.Id, active.ActionId, StringComparison.Ordinal))
            {
                CancelHomeTrip(entity.Id);
                return;
            }

            var outcome = active.Navigator.Poll(active.NavigationRequestId, 0);
            if (outcome == NavigationOutcome.Arrived || Vector3.Distance(entity.Position, home) <= 0.6f)
            {
                CancelHomeTrip(entity.Id, completeAction: true);
                entity.Activity = "Resting at home";
                entity.WaitingReason = null;
            }
            else if (outcome is NavigationOutcome.Unreachable or NavigationOutcome.TimedOut or NavigationOutcome.Cancelled or NavigationOutcome.Removed)
            {
                CancelHomeTrip(entity.Id, completeAction: true);
                entity.Activity = "Resting";
                entity.WaitingReason = "Home path unavailable";
            }
            else
            {
                entity.Activity = "Going home";
                entity.WaitingReason = null;
            }
            return;
        }

        var actionId = $"home-{entity.Id}-{checked(++_nextActionId).ToString("D6", CultureInfo.InvariantCulture)}";
        if (entity.Actions.TryStart(new ActionCandidate(actionId, ActionKind.Wander), () => CancelHomeTrip(entity.Id)) == ActionStartResult.Rejected)
            return;
        var navigator = _navigationForEntity(entity);
        var requestId = navigator.Request(new NavigationRequest(home, timeoutSeconds: NavigationTimeoutSeconds, arrivalTolerance: 0.6));
        _activeHomeTrips[entity.Id] = new ActiveHomeTrip(actionId, navigator, requestId);
        entity.Activity = "Going home";
        entity.WaitingReason = null;
    }

    private void CancelHomeTrip(string entityId, bool completeAction = false)
    {
        if (_activeHomeTrips.Remove(entityId, out var trip))
        {
            trip.Navigator.Cancel(trip.NavigationRequestId);
            if (_world.TryGetEntity(entityId, out var entity) && entity is not null
                && string.Equals(entity.Actions.Current?.Id, trip.ActionId, StringComparison.Ordinal))
            {
                if (completeAction) entity.Actions.Complete();
                else entity.Actions.Cancel();
            }
        }
    }

    private bool OwnsEntity(EntityRecord entity)
    {
        return _world.TryGetEntity(entity.Id, out var worldEntity)
            && ReferenceEquals(entity, worldEntity);
    }

    private static bool IsCurrentAction(EntityRecord entity, ActiveTrip trip)
    {
        return string.Equals(entity.Actions.Current?.Id, trip.ActionId, StringComparison.Ordinal);
    }

    private static bool IsWithinArrivalTolerance(Vector3 position, Vector3 target)
    {
        var x = (double)position.X - target.X;
        var y = (double)position.Y - target.Y;
        var z = (double)position.Z - target.Z;
        return x * x + y * y + z * z <= ArrivalTolerance * ArrivalTolerance;
    }

    private static double DistanceSquared(Vector3 first, Vector3 second)
    {
        var x = (double)first.X - second.X;
        var y = (double)first.Y - second.Y;
        var z = (double)first.Z - second.Z;
        return x * x + y * y + z * z;
    }

    private static int RemainingCargoCapacity(EntityRecord entity)
    {
        return entity.RemainingCarriedCapacity(ItemIds.Wood);
    }

    private enum TripStage
    {
        ToSource,
        ToStockpile
    }

    private sealed record ActiveTrip(
        string ActionId,
        ResourceLocation? Source,
        ResourceLocation Stockpile,
        int Amount,
        TripStage Stage,
        INavigationAdapter? Navigator,
        string? NavigationRequestId);

    private sealed record ActiveHomeTrip(string ActionId, INavigationAdapter Navigator, string NavigationRequestId);
}
