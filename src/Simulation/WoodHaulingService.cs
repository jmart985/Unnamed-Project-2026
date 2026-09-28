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
    ActionRejected,
    InvalidEntity,
    InvalidSpecies
}

public sealed record WoodHaulingResult(
    WoodHaulingStatus Status,
    ResourceLocation? Source = null,
    ResourceLocation? Stockpile = null,
    int Amount = 0,
    string? WaitingReason = null);

public sealed class WoodHaulingService
{
    private const double NavigationTimeoutSeconds = 30;
    private const double ArrivalTolerance = 0.1;

    private readonly World _world;
    private readonly Func<EntityRecord, INavigationAdapter> _navigationForEntity;
    private readonly Dictionary<string, ActiveTrip> _activeTrips = new(StringComparer.Ordinal);
    private long _nextActionId;

    public WoodHaulingService(
        World world,
        Func<EntityRecord, INavigationAdapter> navigationForEntity)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(navigationForEntity);

        _world = world;
        _navigationForEntity = navigationForEntity;
    }

    public WoodHaulingResult Advance(EntityRecord entity, SpeciesDefinition species)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(species);

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

        if (entity.Needs.IsHungry)
        {
            return new WoodHaulingResult(WoodHaulingStatus.NeedsFood, WaitingReason: entity.WaitingReason);
        }

        if (_activeTrips.TryGetValue(entity.Id, out var trip))
        {
            if (!IsCurrentAction(entity, trip))
            {
                CancelTrip(entity.Id);
            }
            else
            {
                return AdvanceTrip(entity, trip);
            }
        }

        if (entity.Actions.Current is not null)
        {
            return new WoodHaulingResult(WoodHaulingStatus.ActionRejected);
        }

        return entity.Inventory.GetValueOrDefault(ItemIds.Wood) > 0
            ? BeginDelivery(entity)
            : BeginPickup(entity);
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

        _activeTrips.Clear();
        _nextActionId = 0;
    }

    private WoodHaulingResult BeginPickup(EntityRecord entity)
    {
        var sourceCandidates = _world.ResourcePiles
            .Where(source => source.Contents.Counts.GetValueOrDefault(ItemIds.Wood) > 0)
            .OrderBy(source => DistanceSquared(entity.Position, source.Position))
            .ThenBy(source => source.Id, StringComparer.Ordinal)
            .ToArray();

        if (sourceCandidates.Length == 0)
        {
            entity.Activity = "Idle";
            entity.WaitingReason = null;
            return new WoodHaulingResult(WoodHaulingStatus.NoWork);
        }

        var remainingCapacity = RemainingCargoCapacity(entity);
        if (remainingCapacity <= 0)
        {
            return Wait(entity, "No inventory capacity available for wood.");
        }

        if (_world.Stockpiles.Count == 0)
        {
            return Wait(entity, "No stockpile is available.");
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
        return Wait(entity, "Wood or stockpile capacity is already reserved.");
    }

    private WoodHaulingResult BeginDelivery(EntityRecord entity)
    {
        var amount = entity.Inventory.GetValueOrDefault(ItemIds.Wood);
        var stockpiles = _world.Stockpiles
            .Where(target => _world.Reservations.GetAvailableCapacity(target.Contents) >= amount)
            .OrderBy(target => DistanceSquared(entity.Position, target.Position))
            .ThenBy(target => target.Id, StringComparer.Ordinal)
            .ToArray();

        if (stockpiles.Length == 0)
        {
            return Wait(entity, "No stockpile has enough capacity for carried wood.");
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
        return Wait(entity, "Stockpile capacity is already reserved.");
    }

    private WoodHaulingResult AdvanceTrip(EntityRecord entity, ActiveTrip trip)
    {
        if (trip.Stockpile is null || !_world.Stockpiles.Contains(trip.Stockpile))
        {
            return FailTrip(entity, trip, "The stockpile target was removed.");
        }

        if (trip.Stage == TripStage.ToSource
            && (trip.Source is null || !_world.ResourcePiles.Contains(trip.Source)))
        {
            return FailTrip(entity, trip, "The wood source was removed.");
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
            return FailTrip(entity, trip, $"Navigation to '{target.Id}' failed: {outcome}.");
        }

        if (!IsWithinArrivalTolerance(entity.Position, target.Position))
        {
            return FailTrip(entity, trip, $"Navigation to '{target.Id}' reported arrival outside tolerance.");
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
                return FailTrip(entity, trip, $"Wood pickup failed: {transfer}.");
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
            return FailTrip(entity, trip, $"Wood deposit failed: {deposit}.");
        }

        entity.Actions.Complete();
        entity.Activity = "Idle";
        entity.WaitingReason = null;
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
        return ToTravelResult(_activeTrips[entity.Id]);
    }

    private bool TryStartAction(EntityRecord entity, string actionId)
    {
        return entity.Actions.TryStart(
            new ActionCandidate(actionId, ActionKind.Haul),
            () => CancelTrip(entity.Id)) is ActionStartResult.Started or ActionStartResult.Preempted;
    }

    private WoodHaulingResult FailTrip(EntityRecord entity, ActiveTrip trip, string reason)
    {
        entity.Actions.Cancel();
        entity.Activity = "Waiting";
        entity.WaitingReason = reason;
        return new WoodHaulingResult(
            WoodHaulingStatus.Waiting,
            trip.Source,
            trip.Stockpile,
            trip.Amount,
            reason);
    }

    private static WoodHaulingResult Wait(EntityRecord entity, string reason)
    {
        entity.Activity = "Waiting";
        entity.WaitingReason = reason;
        return new WoodHaulingResult(WoodHaulingStatus.Waiting, WaitingReason: reason);
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
        var carriedUnits = entity.Inventory.Values.Sum();
        return Math.Max(0, entity.InventoryCapacity - carriedUnits);
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
}
