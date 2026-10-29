using System.Numerics;

namespace Simulation;

public enum EntityOrderKind
{
    Follow,
    Stay
}

/// <summary>
/// Durable command intent. Navigation paths are transient and rebuilt from this record.
/// Follow stores its issuer; Stay stores the position at which it was issued.
/// </summary>
public sealed record EntityOrder(EntityOrderKind Kind, string IssuerId, Vector3? Position = null);

/// <summary>Executes individual Follow and Stay intents through each entity's normal navigation adapter.</summary>
public sealed class OrderService : ITransientSimulationState
{
    private const float StayArrivalTolerance = 0.5f;
    private const float NavigationArrivalTolerance = 0.2f;
    private const float FollowArrivalTolerance = SquadOrderService.FollowStopDistance + 0.01f;
    private const double NavigationTimeoutSeconds = 120;

    private sealed class ActiveOrder(EntityRecord entity, EntityOrder intent, string actionId, INavigationAdapter navigator)
    {
        public EntityRecord Entity { get; } = entity;
        public EntityOrder Intent { get; } = intent;
        public string ActionId { get; } = actionId;
        public INavigationAdapter Navigator { get; } = navigator;
        public string? NavigationRequestId { get; set; }
        public Vector3 Target { get; set; }
        public bool Failed { get; set; }
        public bool Holding { get; set; }
    }

    private readonly World _world;
    private readonly Func<EntityRecord, INavigationAdapter> _navigationForEntity;
    private readonly Func<string, bool>? _isBehaviorSuppressed;
    private readonly Dictionary<string, ActiveOrder> _activeOrders = new(StringComparer.Ordinal);
    private long _nextActionId;

    public OrderService(
        World world,
        Func<EntityRecord, INavigationAdapter> navigationForEntity,
        Func<string, bool>? isBehaviorSuppressed = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(navigationForEntity);
        _world = world;
        _navigationForEntity = navigationForEntity;
        _isBehaviorSuppressed = isBehaviorSuppressed;
    }

    /// <summary>Autonomous work pauses for explicit orders, but survival behavior may still run.</summary>
    public bool ShouldSuppressAutonomousBehavior(EntityRecord entity)
        => entity.OrderIntent is not null && !IsInterrupted(entity);

    public void Advance()
    {
        foreach (var missingId in _activeOrders.Keys.Where(id => !_world.TryGetEntity(id, out _)).ToArray())
            RetireActive(missingId);

        foreach (var entity in _world.Entities.OrderBy(entity => entity.Id, StringComparer.Ordinal))
        {
            if (entity.Id == _world.PlayerEntityId)
            {
                entity.OrderIntent = null;
                CancelActive(entity);
                continue;
            }

            var intent = entity.OrderIntent;
            if (intent is null)
            {
                CancelActive(entity);
                continue;
            }

            if (!IsValidIntent(intent))
            {
                entity.OrderIntent = null;
                CancelActive(entity);
                continue;
            }

            EntityRecord? issuer = null;
            if (intent.Kind == EntityOrderKind.Follow
                && (!_world.TryGetEntity(intent.IssuerId, out issuer) || issuer is null || issuer.Health.IsDead || issuer.Id == entity.Id))
            {
                // A stale Follow cannot be reconstructed safely. Clear the saved intent rather
                // than leaving an entity permanently suppressing its ordinary behavior.
                entity.OrderIntent = null;
                CancelActive(entity);
                continue;
            }

            if (entity.Health.IsDead || entity.Health.IsIncapacitated)
            {
                CancelActive(entity);
                continue;
            }

            if (_isBehaviorSuppressed?.Invoke(entity.Id) == true)
            {
                CancelActive(entity);
                continue;
            }

            if (IsInterrupted(entity))
            {
                CancelActive(entity);
                continue;
            }

            if (_activeOrders.TryGetValue(entity.Id, out var active) && active.Intent != intent)
            {
                CancelActive(entity);
                active = null;
            }

            if (active is not null && entity.Actions.Current?.Id != active.ActionId)
            {
                RetireActive(entity.Id);
                active = null;
            }

            if (active is null)
            {
                var actionId = $"entity-order-{entity.Id}-{(++_nextActionId).ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                var navigator = _navigationForEntity(entity);
                var start = entity.Actions.TryStart(
                    new ActionCandidate(actionId, ActionKind.CommandMove),
                    () => OnActionFinished(entity.Id, actionId));
                if (start is ActionStartResult.Rejected or ActionStartResult.CargoPending)
                    continue;

                active = new ActiveOrder(entity, intent, actionId, navigator);
                _activeOrders[entity.Id] = active;
            }

            AdvanceActive(active, issuer);
        }
    }

    /// <summary>Prevents an active Follow route from crossing its issuer's live two-metre boundary.</summary>
    public Vector3 ConstrainFollowStep(string entityId, Vector3 current, Vector3 proposed)
    {
        if (!_activeOrders.TryGetValue(entityId, out var active)
            || active.Intent.Kind != EntityOrderKind.Follow
            || active.NavigationRequestId is null
            || !_world.TryGetEntity(active.Intent.IssuerId, out var issuer)
            || issuer is null
            || !Finite(current)
            || !Finite(proposed))
            return proposed;

        var offset = new Vector2(current.X - issuer.Position.X, current.Z - issuer.Position.Z);
        var step = new Vector2(proposed.X - current.X, proposed.Z - current.Z);
        var radiusSquared = SquadOrderService.FollowStopDistance * SquadOrderService.FollowStopDistance;
        if (offset.LengthSquared() <= radiusSquared) return current;

        var a = step.LengthSquared();
        var b = Vector2.Dot(offset, step);
        if (a <= 1e-12f || b >= 0) return proposed;
        var discriminant = b * b - a * (offset.LengthSquared() - radiusSquared);
        if (discriminant <= 0) return proposed;
        var entry = (-b - MathF.Sqrt(discriminant)) / a;
        if (entry < 0 || entry > 1) return proposed;
        return Vector3.Lerp(current, proposed, MathF.Max(0, entry - 0.0001f / MathF.Sqrt(a)));
    }

    public void Reset()
    {
        foreach (var entity in _world.Entities)
            CancelActive(entity);
        foreach (var id in _activeOrders.Keys.ToArray())
            RetireActive(id);
    }

    private void AdvanceActive(ActiveOrder active, EntityRecord? issuer)
    {
        var entity = active.Entity;
        var isFollowing = active.Intent.Kind == EntityOrderKind.Follow;
        var goal = isFollowing ? GetFollowGoal(entity.Position, issuer!.Position) : active.Intent.Position!.Value;
        var followDistance = active.Holding ? SquadOrderService.FollowResumeDistance : FollowArrivalTolerance;
        var atGoal = isFollowing
            ? HorizontalDistanceSquared(entity.Position, issuer!.Position)
                <= followDistance * followDistance
            : Vector3.DistanceSquared(entity.Position, goal) <= StayArrivalTolerance * StayArrivalTolerance;

        if (atGoal)
        {
            CancelNavigation(active);
            active.Failed = false;
            active.Target = goal;
            if (isFollowing) active.Holding = true;
            entity.Activity = isFollowing ? "Order: following" : "Order: staying";
            entity.WaitingReason = null;
            return;
        }

        if (active.NavigationRequestId is not null)
        {
            if (isFollowing && Vector3.DistanceSquared(goal, active.Target) > 1)
            {
                CancelNavigation(active);
                active.Failed = false;
            }
            else
            {
                var outcome = Vector3.DistanceSquared(entity.Position, active.Target)
                    <= StayArrivalTolerance * StayArrivalTolerance
                    ? NavigationOutcome.Arrived
                    : active.Navigator.Poll(active.NavigationRequestId, 0);
                if (outcome == NavigationOutcome.InProgress) return;

                CancelNavigation(active);
                if (outcome == NavigationOutcome.Arrived && (isFollowing
                    ? HorizontalDistanceSquared(entity.Position, issuer!.Position)
                        <= FollowArrivalTolerance * FollowArrivalTolerance
                    : Vector3.DistanceSquared(entity.Position, goal) <= StayArrivalTolerance * StayArrivalTolerance))
                {
                    active.Failed = false;
                    if (isFollowing) active.Holding = true;
                    entity.Activity = isFollowing ? "Order: following" : "Order: staying";
                    entity.WaitingReason = null;
                    return;
                }

                if (outcome != NavigationOutcome.Arrived)
                {
                    active.Failed = true;
                    entity.Activity = "Order: waiting";
                    entity.WaitingReason = $"Order navigation: {outcome}.";
                    return;
                }

                // A path can report arrival within its tolerance while the follower is still
                // outside the holding radius. Rebuild from the current position once.
                active.Failed = false;
            }
        }

        if (active.Failed && Vector3.DistanceSquared(goal, active.Target) <= 1)
        {
            entity.Activity = "Order: waiting";
            return;
        }

        active.Target = goal;
        active.NavigationRequestId = active.Navigator.Request(
            new NavigationRequest(goal, arrivalTolerance: NavigationArrivalTolerance, timeoutSeconds: NavigationTimeoutSeconds));
        active.Failed = false;
        active.Holding = false;
        entity.Activity = isFollowing ? "Order: following" : "Order: moving to stay position";
        entity.WaitingReason = null;
    }

    private static Vector3 GetFollowGoal(Vector3 followerPosition, Vector3 issuerPosition)
    {
        var outward = new Vector2(followerPosition.X - issuerPosition.X, followerPosition.Z - issuerPosition.Z);
        if (outward.LengthSquared() <= 0.0001f)
            outward = Vector2.UnitX;
        else
            outward = Vector2.Normalize(outward);

        // Keep the requested path endpoint inside the holding radius even when Godot
        // accepts arrival before reaching the exact point.
        var distance = SquadOrderService.FollowStopDistance - StayArrivalTolerance;
        return new Vector3(issuerPosition.X + outward.X * distance, issuerPosition.Y, issuerPosition.Z + outward.Y * distance);
    }

    private static float HorizontalDistanceSquared(Vector3 first, Vector3 second)
        => Vector2.DistanceSquared(new(first.X, first.Z), new(second.X, second.Z));

    private static bool IsInterrupted(EntityRecord entity)
        => entity.Needs.IsHungry
            || entity.SleepState == SleepState.Sleeping
            || entity.Actions.Current?.InterruptsMovementOrders == true;

    private static bool IsValidIntent(EntityOrder intent)
        => Enum.IsDefined(intent.Kind)
            && !string.IsNullOrWhiteSpace(intent.IssuerId)
            && (intent.Kind == EntityOrderKind.Follow
                ? intent.Position is null
                : intent.Position is { } position && Finite(position));

    private static bool Finite(Vector3 position)
        => float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);

    private void CancelActive(EntityRecord entity)
    {
        if (!_activeOrders.TryGetValue(entity.Id, out var active)) return;
        if (entity.Actions.Current?.Id == active.ActionId)
            entity.Actions.Cancel();
        else
            RetireActive(entity.Id);
    }

    private void OnActionFinished(string entityId, string actionId)
    {
        if (_activeOrders.TryGetValue(entityId, out var active) && active.ActionId == actionId)
            RetireActive(entityId);
    }

    private void RetireActive(string entityId)
    {
        if (!_activeOrders.Remove(entityId, out var active)) return;
        CancelNavigation(active);
    }

    private static void CancelNavigation(ActiveOrder active)
    {
        if (active.NavigationRequestId is not { } requestId) return;
        active.Navigator.Cancel(requestId);
        active.NavigationRequestId = null;
    }
}
