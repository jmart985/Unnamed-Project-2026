using System.Numerics;

namespace Simulation;

public enum WanderingStatus
{
    NotWanderer,
    Traveling,
    Arrived,
    NeedsFood,
    Waiting,
    ActionRejected,
    InvalidEntity,
    InvalidSpecies
}

public sealed record WanderingResult(
    WanderingStatus Status,
    Vector3? Target = null,
    string? WaitingReason = null);

public sealed class WanderingService
{
    private const double NavigationTimeoutSeconds = 30;
    private const double ArrivalTolerance = 0.1;

    private readonly World _world;
    private readonly Func<EntityRecord, INavigationAdapter> _navigationForEntity;
    private readonly IReadOnlyList<Vector3> _goals;
    private readonly Dictionary<string, ActiveTrip> _activeTrips = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _nextGoalIndexes = new(StringComparer.Ordinal);

    public WanderingService(
        World world,
        Func<EntityRecord, INavigationAdapter> navigationForEntity,
        IEnumerable<Vector3> goals)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(navigationForEntity);
        ArgumentNullException.ThrowIfNull(goals);

        _goals = goals.ToArray();
        if (_goals.Count == 0)
        {
            throw new ArgumentException("At least one wandering goal is required.", nameof(goals));
        }

        if (_goals.Any(goal => !float.IsFinite(goal.X)
                               || !float.IsFinite(goal.Y)
                               || !float.IsFinite(goal.Z)))
        {
            throw new ArgumentException("Wandering goals must have finite coordinates.", nameof(goals));
        }

        _world = world;
        _navigationForEntity = navigationForEntity;
    }

    public WanderingResult Advance(EntityRecord entity, SpeciesDefinition species)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(species);

        if (!_world.TryGetEntity(entity.Id, out var worldEntity)
            || !ReferenceEquals(entity, worldEntity))
        {
            return new WanderingResult(WanderingStatus.InvalidEntity);
        }

        if (!string.Equals(entity.SpeciesId, species.Id, StringComparison.Ordinal))
        {
            return new WanderingResult(WanderingStatus.InvalidSpecies);
        }

        if (species.CanHaul)
        {
            return new WanderingResult(WanderingStatus.NotWanderer);
        }

        if (entity.Needs.IsHungry)
        {
            CancelTrip(entity);
            return new WanderingResult(WanderingStatus.NeedsFood);
        }

        if (_activeTrips.TryGetValue(entity.Id, out var activeTrip))
        {
            if (entity.Actions.Current?.Id != activeTrip.ActionId)
            {
                _activeTrips.Remove(entity.Id);
            }
            else
            {
                return AdvanceTrip(entity, activeTrip);
            }
        }

        if (entity.Actions.Current is not null)
        {
            return new WanderingResult(WanderingStatus.ActionRejected);
        }

        return BeginTrip(entity);
    }

    public void Reset()
    {
        foreach (var entityId in _activeTrips.Keys.ToArray())
        {
            if (_world.TryGetEntity(entityId, out var entity) && entity is not null)
            {
                CancelTrip(entity);
            }
            else
            {
                _activeTrips.Remove(entityId);
            }
        }

        _activeTrips.Clear();
        _nextGoalIndexes.Clear();
    }

    private WanderingResult BeginTrip(EntityRecord entity)
    {
        var goalIndex = _nextGoalIndexes.GetValueOrDefault(entity.Id);
        var target = _goals[goalIndex];
        _nextGoalIndexes[entity.Id] = (goalIndex + 1) % _goals.Count;

        INavigationAdapter? navigator = null;
        string? requestId = null;
        try
        {
            navigator = _navigationForEntity(entity)
                ?? throw new InvalidOperationException("The entity has no navigation adapter.");
            requestId = navigator.Request(new NavigationRequest(
                target,
                arrivalTolerance: ArrivalTolerance,
                timeoutSeconds: NavigationTimeoutSeconds));
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new InvalidOperationException("The navigation adapter returned an empty request ID.");
            }

            var actionId = $"wander-{entity.Id}-{goalIndex}";
            var startResult = entity.Actions.TryStart(
                new ActionCandidate(actionId, ActionKind.Wander),
                () => navigator.Cancel(requestId));
            if (startResult is not (ActionStartResult.Started or ActionStartResult.Preempted))
            {
                navigator.Cancel(requestId);
                return new WanderingResult(WanderingStatus.ActionRejected, target);
            }

            _activeTrips[entity.Id] = new ActiveTrip(actionId, target, navigator, requestId);
            entity.Activity = "Wandering";
            entity.WaitingReason = null;
            return new WanderingResult(WanderingStatus.Traveling, target);
        }
        catch
        {
            if (requestId is not null && navigator is not null)
            {
                navigator.Cancel(requestId);
            }

            return new WanderingResult(
                WanderingStatus.Waiting,
                target,
                "Wandering navigation could not be started.");
        }
    }

    private WanderingResult AdvanceTrip(EntityRecord entity, ActiveTrip trip)
    {
        var outcome = trip.Navigator.Poll(trip.NavigationRequestId, 0);
        if (outcome == NavigationOutcome.InProgress)
        {
            return new WanderingResult(WanderingStatus.Traveling, trip.Target);
        }

        if (outcome == NavigationOutcome.Arrived
            && IsWithinArrivalTolerance(entity.Position, trip.Target))
        {
            _activeTrips.Remove(entity.Id);
            entity.Actions.Complete();
            entity.Activity = "Idle";
            entity.WaitingReason = null;
            return new WanderingResult(WanderingStatus.Arrived, trip.Target);
        }

        CancelTrip(entity);
        entity.Activity = "Waiting";
        entity.WaitingReason = $"Wandering target failed: {outcome}.";
        return new WanderingResult(WanderingStatus.Waiting, trip.Target, entity.WaitingReason);
    }

    private void CancelTrip(EntityRecord entity)
    {
        if (_activeTrips.Remove(entity.Id, out var trip))
        {
            trip.Navigator.Cancel(trip.NavigationRequestId);
            if (entity.Actions.Current?.Id == trip.ActionId)
            {
                entity.Actions.Cancel();
            }
        }
    }

    private static bool IsWithinArrivalTolerance(Vector3 position, Vector3 target)
    {
        return Vector3.DistanceSquared(position, target) <= ArrivalTolerance * ArrivalTolerance;
    }

    private sealed record ActiveTrip(
        string ActionId,
        Vector3 Target,
        INavigationAdapter Navigator,
        string NavigationRequestId);
}
