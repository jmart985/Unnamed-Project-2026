using System.Numerics;

namespace Simulation;

public enum SquadResult { Accepted, Unchanged, InvalidEntity, IneligibleEntity, NotMember, EmptySquad, InvalidTarget, PlayerUnavailable, InvalidOrder }
public enum SquadOrderKind { None, MoveTo, FollowPlayer, Stop }
public sealed record SquadOrderResult(SquadResult Code, string Reason);

/// <summary>One transient squad order; membership belongs exclusively to World.</summary>
public sealed class SquadOrderService(World world, Func<EntityRecord, INavigationAdapter> navigationForEntity) : ITransientSimulationState
{
    // Current capsules total at most 0.82 m. Keep a comfortable gap, then use a separate
    // resume band so idle followers do not repeatedly chase tiny player movements.
    public const float FollowStopDistance = 2;
    public const float FollowResumeDistance = 3;
    private const float FollowArrivalTolerance = FollowStopDistance + 0.01f;
    private const float FollowPathArrivalTolerance = 0.5f;
    private const float FollowStandOffDistance = FollowStopDistance + 0.1f;
    private const float MoveToFormationSpacing = 1.25f;
    private sealed record Trip(EntityRecord Entity, INavigationAdapter Navigator, string RequestId, Vector3 Target);
    private readonly Dictionary<string, Trip> _trips = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector3> _finishedTargets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector3> _moveTargets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _followOffsets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failedMembers = new(StringComparer.Ordinal);
    private long _nextActionId;
    public SquadOrderKind Order { get; private set; }
    public Vector3? Target { get; private set; }
    public string Status { get; private set; } = "No squad order.";

    public SquadOrderResult Execute(SquadOrderKind order, Vector3? target = null)
    {
        if (!Enum.IsDefined(order) || order == SquadOrderKind.None) return Reject(SquadResult.InvalidOrder, "Choose move, follow, or stop.");
        if (world.SquadMemberIds.Count == 0) return Reject(SquadResult.EmptySquad, "Add a human NPC first.");
        if (world.SquadMemberIds.Any(id => !world.TryGetEntity(id, out var entity) || entity is null || entity.Health.IsDead))
            return Reject(SquadResult.InvalidEntity, "A squad member is unavailable.");
        if (order == SquadOrderKind.MoveTo && (target is null || !Finite(target.Value)))
            return Reject(SquadResult.InvalidTarget, "Move target must have finite coordinates.");
        if (order == SquadOrderKind.FollowPlayer && !TryPlayer(out _))
            return Reject(SquadResult.PlayerUnavailable, "The player is unavailable.");
        var effectiveTarget = order == SquadOrderKind.MoveTo ? target : null;
        if (Order == order && Target == effectiveTarget) return new(SquadResult.Unchanged, "Squad order already active.");
        Reset();
        Order = order;
        Target = effectiveTarget;
        if (order == SquadOrderKind.MoveTo)
        {
            AssignMoveTargets(effectiveTarget!.Value);
        }
        else if (order == SquadOrderKind.FollowPlayer && TryPlayer(out var player))
        {
            AssignFollowOffsets(player!.Position);
        }

        foreach (var id in world.SquadMemberIds)
            if (world.TryGetEntity(id, out var entity) && entity is not null && entity.Actions.Current?.Kind != ActionKind.Eat)
                entity.Actions.Cancel();
        Status = $"Accepted: {order}.";
        return new(SquadResult.Accepted, Status);
    }

    public void Advance()
    {
        foreach (var id in _trips.Keys.Where(id => !world.SquadMemberIds.Contains(id)).ToArray()) CancelTrip(id);
        foreach (var id in _finishedTargets.Keys.Where(id => !world.SquadMemberIds.Contains(id)).ToArray()) _finishedTargets.Remove(id);
        foreach (var id in _moveTargets.Keys.Where(id => !world.SquadMemberIds.Contains(id)).ToArray()) _moveTargets.Remove(id);
        foreach (var id in _followOffsets.Keys.Where(id => !world.SquadMemberIds.Contains(id)).ToArray()) _followOffsets.Remove(id);
        if (Order == SquadOrderKind.None) return;
        if (Order == SquadOrderKind.FollowPlayer && !TryPlayer(out _))
        {
            Reset();
            Status = "Follow stopped: player unavailable.";
            return;
        }
        if (Order == SquadOrderKind.FollowPlayer && _followOffsets.Count != world.SquadMemberIds.Count)
        {
            // Membership may change while Follow remains active. Rebuild the formation
            // atomically so new members do not all target the player's exact position.
            foreach (var id in _trips.Keys.ToArray()) CancelTrip(id);
            foreach (var id in world.SquadMemberIds)
                if (world.TryGetEntity(id, out var member) && member is not null
                    && member.Actions.Current?.Kind != ActionKind.Eat)
                    member.Actions.Cancel();
            _finishedTargets.Clear();
            _failedMembers.Clear();
            AssignFollowOffsets(GetPlayerPosition());
        }
        foreach (var id in world.SquadMemberIds)
        {
            if (!world.TryGetEntity(id, out var entity) || entity is null) continue;
            if (entity.Health.IsDead) { CancelTrip(id); continue; }
            if (Order == SquadOrderKind.Stop)
            {
                if (entity.Actions.Current is null) entity.Activity = "Squad stopped";
                continue;
            }
            var following = Order == SquadOrderKind.FollowPlayer;
            var playerPosition = following ? GetPlayerPosition() : default;
            var goal = following
                ? AddHorizontal(playerPosition, _followOffsets.GetValueOrDefault(id))
                : _moveTargets.GetValueOrDefault(id, Target!.Value);
            var arrivalTolerance = following ? FollowPathArrivalTolerance : 0.5f;
            var distanceSquared = following ? HorizontalDistanceSquared(entity.Position, playerPosition) : Vector3.DistanceSquared(entity.Position, goal);
            if (following && distanceSquared <= (_trips.ContainsKey(id) ? FollowArrivalTolerance * FollowArrivalTolerance : FollowResumeDistance * FollowResumeDistance))
            {
                CancelTrip(id);
                _finishedTargets[id] = goal;
                _failedMembers.Remove(id);
                if (entity.Actions.Current is null) entity.Activity = "Squad following: holding distance";
                continue;
            }
            if (_trips.TryGetValue(id, out var trip))
            {
                // Retarget follow only after the player moves a meaningful distance.
                if (Order == SquadOrderKind.FollowPlayer && Vector3.DistanceSquared(goal, trip.Target) > 1)
                    CancelTrip(id);
                else
                {
                    // Position is authoritative; the engine may have already consumed its terminal poll.
                    var outcome = Vector3.DistanceSquared(entity.Position, trip.Target) <= arrivalTolerance * arrivalTolerance
                        ? NavigationOutcome.Arrived : trip.Navigator.Poll(trip.RequestId, 0);
                    if (outcome == NavigationOutcome.InProgress) continue;
                    var arrived = outcome == NavigationOutcome.Arrived && Vector3.DistanceSquared(entity.Position, trip.Target) <= arrivalTolerance * arrivalTolerance;
                    CancelTrip(id);
                    _finishedTargets[id] = trip.Target;
                    if (arrived) _failedMembers.Remove(id); else _failedMembers.Add(id);
                    entity.Activity = arrived ? "Squad arrived" : "Squad waiting";
                    entity.WaitingReason = arrived ? null : $"Squad navigation: {outcome}.";
                    continue;
                }
            }
            if (entity.Actions.Current?.Kind == ActionKind.Eat || entity.Needs.IsHungry) continue;
            if (_finishedTargets.TryGetValue(id, out var finished) && Vector3.DistanceSquared(goal, finished) <= 1
                && (_failedMembers.Contains(id) || distanceSquared <= arrivalTolerance * arrivalTolerance)) continue;
            if (distanceSquared <= arrivalTolerance * arrivalTolerance)
            {
                _finishedTargets[id] = goal;
                entity.Activity = "Squad arrived";
                continue;
            }
            var navigator = navigationForEntity(entity);
            string? requestId = null;
            var started = entity.Actions.TryStart(new ActionCandidate($"squad-{++_nextActionId}", ActionKind.SquadMove), () =>
            {
                if (requestId is not null) navigator.Cancel(requestId);
                _trips.Remove(id);
            });
            if (started is ActionStartResult.Rejected or ActionStartResult.CargoPending) continue;
            requestId = navigator.Request(new NavigationRequest(goal, arrivalTolerance: arrivalTolerance, timeoutSeconds: 120));
            _failedMembers.Remove(id);
            _trips[id] = new Trip(entity, navigator, requestId, goal);
            entity.Activity = $"Squad {Order}";
            entity.WaitingReason = null;
        }
    }

    /// <summary>Clip the entire follow step against the live player, before the engine moves a collider.
    /// A stale path must not cross the player even when its endpoint lies beyond them.</summary>
    public Vector3 ConstrainFollowStep(string entityId, Vector3 current, Vector3 proposed)
    {
        if (Order != SquadOrderKind.FollowPlayer || !_trips.ContainsKey(entityId)) return proposed;
        if (!TryPlayer(out var player) || !Finite(current) || !Finite(proposed)) return current;
        var offset = new Vector2(current.X - player!.Position.X, current.Z - player.Position.Z);
        var step = new Vector2(proposed.X - current.X, proposed.Z - current.Z);
        var radiusSquared = FollowStopDistance * FollowStopDistance;
        if (offset.LengthSquared() <= radiusSquared) return current;
        var a = step.LengthSquared();
        var b = Vector2.Dot(offset, step);
        if (a <= 1e-12f || b >= 0) return proposed;
        var discriminant = b * b - a * (offset.LengthSquared() - radiusSquared);
        if (discriminant <= 0) return proposed;
        var entry = (-b - MathF.Sqrt(discriminant)) / a;
        if (entry < 0 || entry > 1) return proposed;
        // Leave a tiny numeric margin outside the boundary, without snapping/teleporting.
        return Vector3.Lerp(current, proposed, MathF.Max(0, entry - 0.0001f / MathF.Sqrt(a)));
    }

    private static float HorizontalDistanceSquared(Vector3 first, Vector3 second)
        => Vector2.DistanceSquared(new(first.X, first.Z), new(second.X, second.Z));

    public void Reset()
    {
        foreach (var id in _trips.Keys.ToArray()) CancelTrip(id);
        _finishedTargets.Clear();
        _moveTargets.Clear();
        _followOffsets.Clear();
        _failedMembers.Clear();
        Order = SquadOrderKind.None;
        Target = null;
        Status = "No squad order.";
    }

    private void CancelTrip(string id)
    {
        if (!_trips.Remove(id, out var trip)) return;
        trip.Navigator.Cancel(trip.RequestId);
        if (trip.Entity.Actions.Current?.Kind == ActionKind.SquadMove) trip.Entity.Actions.Cancel();
        trip.Entity.Activity = "Idle";
        trip.Entity.WaitingReason = null;
    }

    private bool TryPlayer(out EntityRecord? player) => world.TryGetEntity(world.PlayerEntityId, out player) && player is not null && !player.Health.IsDead;

    private void AssignMoveTargets(Vector3 requestedTarget)
    {
        var members = world.SquadMemberIds
            .Select(id => world.TryGetEntity(id, out var entity) ? entity : null)
            .Where(entity => entity is not null)
            .Select(entity => entity!)
            .ToArray();
        if (members.Length == 0) return;

        var center = members.Aggregate(Vector3.Zero, (sum, entity) => sum + entity.Position) / members.Length;
        var direction = new Vector2(requestedTarget.X - center.X, requestedTarget.Z - center.Z);
        if (direction.LengthSquared() <= 1e-8f)
        {
            direction = new Vector2(0, -1);
        }
        else
        {
            direction = Vector2.Normalize(direction);
        }

        var tangent = new Vector2(-direction.Y, direction.X);
        var orderedMembers = members
            .Select(entity => new
            {
                Entity = entity,
                Projection = entity.Position.X * tangent.X + entity.Position.Z * tangent.Y
            })
            .OrderBy(entry => entry.Projection)
            .ThenBy(entry => entry.Entity.Id, StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < orderedMembers.Length; index++)
        {
            var offset = (index - (orderedMembers.Length - 1) * 0.5f) * MoveToFormationSpacing;
            var entity = orderedMembers[index].Entity;
            _moveTargets[entity.Id] = new Vector3(
                requestedTarget.X + tangent.X * offset,
                requestedTarget.Y,
                requestedTarget.Z + tangent.Y * offset);
        }
    }

    private void AssignFollowOffsets(Vector3 playerPosition)
    {
        var members = world.SquadMemberIds
            .Select(id => world.TryGetEntity(id, out var entity) ? entity : null)
            .Where(entity => entity is not null)
            .Select(entity => entity!)
            .ToArray();
        if (members.Length == 0) return;

        var center = members.Aggregate(Vector3.Zero, (sum, entity) => sum + entity.Position) / members.Length;
        var outward = new Vector2(center.X - playerPosition.X, center.Z - playerPosition.Z);
        if (outward.LengthSquared() <= 1e-8f) outward = new Vector2(0, -1);
        else outward = Vector2.Normalize(outward);
        var tangent = new Vector2(-outward.Y, outward.X);
        var radius = FollowStandOffDistance;
        var useFullCircle = members.Length > 6;
        var orderedMembers = members
            .Select(entity =>
            {
                var relative = new Vector2(entity.Position.X - playerPosition.X, entity.Position.Z - playerPosition.Z);
                var angle = MathF.Atan2(Vector2.Dot(relative, tangent), Vector2.Dot(relative, outward));
                return new
                {
                    Entity = entity,
                    Projection = entity.Position.X * tangent.X + entity.Position.Z * tangent.Y,
                    Angle = angle
                };
            })
            .OrderBy(entry => useFullCircle ? entry.Angle : entry.Projection)
            .ThenBy(entry => entry.Entity.Id, StringComparer.Ordinal)
            .ToArray();
        if (useFullCircle)
        {
            radius = MathF.Max(radius, MoveToFormationSpacing / (2 * MathF.Sin(MathF.PI / orderedMembers.Length)));
        }

        var angleStep = orderedMembers.Length > 1 && !useFullCircle
            ? 2 * MathF.Asin(MathF.Min(1, MoveToFormationSpacing / (2 * radius)))
            : 0;
        var circleStartAngle = useFullCircle ? orderedMembers[0].Angle : 0;
        for (var index = 0; index < orderedMembers.Length; index++)
        {
            var angle = useFullCircle
                ? circleStartAngle + MathF.Tau * index / orderedMembers.Length
                : (index - (orderedMembers.Length - 1) * 0.5f) * angleStep;
            _followOffsets[orderedMembers[index].Entity.Id] = outward * (radius * MathF.Cos(angle))
                + tangent * (radius * MathF.Sin(angle));
        }
    }

    private static Vector3 AddHorizontal(Vector3 anchor, Vector2 offset)
        => new(anchor.X + offset.X, anchor.Y, anchor.Z + offset.Y);

    private Vector3 GetPlayerPosition() { TryPlayer(out var player); return player!.Position; }
    private static bool Finite(Vector3 target) => float.IsFinite(target.X) && float.IsFinite(target.Y) && float.IsFinite(target.Z);
    private static SquadOrderResult Reject(SquadResult result, string reason) => new(result, $"Rejected: {reason}");
}
