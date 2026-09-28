using System.Globalization;
using System.Numerics;

namespace Simulation;

public enum WanderStatus
{
    Hungry,
    Traveling,
    Arrived,
    InvalidEntity,
    InvalidSpecies,
    InvalidGoal,
    ActionRejected
}

public sealed record WanderResult(
    WanderStatus Status,
    string? ActionId = null,
    Vector3? Goal = null);

public sealed class WanderService
{
    private readonly World _world;
    private readonly Func<EntityRecord, INavigationAdapter> _navigationForEntity;
    private readonly Dictionary<string, ActiveWander> _activeWanders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _nextGoalIndices = new(StringComparer.Ordinal);
    private long _nextActionId;

    public WanderService(World world, Func<EntityRecord, INavigationAdapter> navigationForEntity)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(navigationForEntity);

        _world = world;
        _navigationForEntity = navigationForEntity;
    }

    public WanderResult Advance(
        EntityRecord entity,
        SpeciesDefinition species,
        IReadOnlyList<Vector3> boundedGoals)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(boundedGoals);

        if (!OwnsEntity(entity))
        {
            return new WanderResult(WanderStatus.InvalidEntity);
        }

        if (!string.Equals(entity.SpeciesId, species.Id, StringComparison.Ordinal))
        {
            return new WanderResult(WanderStatus.InvalidSpecies);
        }

        if (boundedGoals.Count == 0 || boundedGoals.Any(goal => !IsFinite(goal)))
        {
            return new WanderResult(WanderStatus.InvalidGoal);
        }

        if (_activeWanders.TryGetValue(entity.Id, out var activeWander))
        {
            if (!IsCurrentAction(entity, activeWander))
            {
                CancelWander(entity.Id);
            }
            else
            {
                var outcome = activeWander.Navigator.Poll(activeWander.NavigationRequestId, 0);
                if (outcome == NavigationOutcome.InProgress)
                {
                    entity.Activity = "Wandering";
                    entity.WaitingReason = null;
                    return new WanderResult(WanderStatus.Traveling, activeWander.ActionId, activeWander.Goal);
                }

                entity.Activity = "Idle";
                entity.WaitingReason = null;
                if (outcome == NavigationOutcome.Arrived)
                {
                    entity.Actions.Complete();
                    return new WanderResult(WanderStatus.Arrived, activeWander.ActionId, activeWander.Goal);
                }

                entity.Actions.Cancel();
                return new WanderResult(WanderStatus.ActionRejected, activeWander.ActionId, activeWander.Goal);
            }
        }

        if (entity.Needs.IsHungry)
        {
            return new WanderResult(WanderStatus.Hungry);
        }

        if (entity.Actions.Current is not null)
        {
            return new WanderResult(WanderStatus.ActionRejected);
        }

        var goalIndex = _nextGoalIndices.GetValueOrDefault(entity.Id) % boundedGoals.Count;
        var goal = boundedGoals[goalIndex];
        _nextGoalIndices[entity.Id] = goalIndex + 1;

        var actionId = $"wander-{entity.Id}-{(++_nextActionId).ToString("D6", CultureInfo.InvariantCulture)}";
        var startResult = entity.Actions.TryStart(
            new ActionCandidate(actionId, ActionKind.Wander),
            () => CancelWander(entity.Id));
        if (startResult == ActionStartResult.Rejected)
        {
            return new WanderResult(WanderStatus.ActionRejected);
        }

        INavigationAdapter? navigator = null;
        string? requestId = null;
        var wanderStarted = false;

        try
        {
            navigator = _navigationForEntity(entity)
                ?? throw new InvalidOperationException("The entity has no navigation adapter.");
            requestId = navigator.Request(new NavigationRequest(goal, timeoutSeconds: 30));
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new InvalidOperationException("The navigation adapter returned an empty request ID.");
            }

            _activeWanders.Add(entity.Id, new ActiveWander(actionId, requestId, navigator, goal));
            wanderStarted = true;
        }
        finally
        {
            if (!wanderStarted)
            {
                if (requestId is not null && navigator is not null)
                {
                    navigator.Cancel(requestId);
                }

                entity.Actions.Cancel();
            }
        }

        entity.Activity = "Wandering";
        entity.WaitingReason = null;
        return new WanderResult(WanderStatus.Traveling, actionId, goal);
    }

    public void Reset()
    {
        foreach (var entityId in _activeWanders.Keys.ToArray())
        {
            CancelWander(entityId);
        }

        _nextGoalIndices.Clear();
    }

    private void CancelWander(string entityId)
    {
        if (_activeWanders.Remove(entityId, out var activeWander))
        {
            activeWander.Navigator.Cancel(activeWander.NavigationRequestId);
        }
    }

    private bool OwnsEntity(EntityRecord entity)
    {
        return _world.TryGetEntity(entity.Id, out var worldEntity)
            && ReferenceEquals(entity, worldEntity);
    }

    private static bool IsCurrentAction(EntityRecord entity, ActiveWander activeWander)
    {
        return string.Equals(entity.Actions.Current?.Id, activeWander.ActionId, StringComparison.Ordinal);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X)
            && float.IsFinite(value.Y)
            && float.IsFinite(value.Z);
    }

    private sealed record ActiveWander(
        string ActionId,
        string NavigationRequestId,
        INavigationAdapter Navigator,
        Vector3 Goal);
}
