using System.Numerics;

namespace Simulation;

public enum SquadResult { Accepted, Unchanged, InvalidEntity, IneligibleEntity, NotMember, EmptySquad, InvalidTarget, PlayerUnavailable, InvalidOrder }
public enum SquadOrderKind { None, MoveTo, FollowPlayer, Stop }
public sealed record SquadOrderResult(SquadResult Code, string Reason);

/// <summary>One transient squad order; membership belongs exclusively to World.</summary>
public sealed class SquadOrderService(World world, Func<EntityRecord, INavigationAdapter> navigationForEntity) : ITransientSimulationState
{
    private sealed record Trip(EntityRecord Entity, INavigationAdapter Navigator, string RequestId, Vector3 Target);
    private readonly Dictionary<string, Trip> _trips = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector3> _finishedTargets = new(StringComparer.Ordinal);
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
        if (Order == SquadOrderKind.None) return;
        if (Order == SquadOrderKind.FollowPlayer && !TryPlayer(out _))
        {
            Reset();
            Status = "Follow stopped: player unavailable.";
            return;
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
            var goal = Order == SquadOrderKind.FollowPlayer ? GetPlayerPosition() : Target!.Value;
            if (_trips.TryGetValue(id, out var trip))
            {
                // Retarget follow only after the player moves a meaningful distance.
                if (Order == SquadOrderKind.FollowPlayer && Vector3.DistanceSquared(goal, trip.Target) > 1)
                    CancelTrip(id);
                else
                {
                    // Position is authoritative; the engine may have already consumed its terminal poll.
                    var outcome = Vector3.DistanceSquared(entity.Position, trip.Target) <= 0.25f
                        ? NavigationOutcome.Arrived : trip.Navigator.Poll(trip.RequestId, 0);
                    if (outcome == NavigationOutcome.InProgress) continue;
                    var arrived = outcome == NavigationOutcome.Arrived && Vector3.DistanceSquared(entity.Position, trip.Target) <= 0.25f;
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
                && (_failedMembers.Contains(id) || Vector3.DistanceSquared(entity.Position, goal) <= 0.25f)) continue;
            if (Vector3.DistanceSquared(entity.Position, goal) <= 0.25f)
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
            requestId = navigator.Request(new NavigationRequest(goal, arrivalTolerance: 0.5, timeoutSeconds: 120));
            _failedMembers.Remove(id);
            _trips[id] = new Trip(entity, navigator, requestId, goal);
            entity.Activity = $"Squad {Order}";
            entity.WaitingReason = null;
        }
    }

    public void Reset()
    {
        foreach (var id in _trips.Keys.ToArray()) CancelTrip(id);
        _finishedTargets.Clear();
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
    private Vector3 GetPlayerPosition() { TryPlayer(out var player); return player!.Position; }
    private static bool Finite(Vector3 target) => float.IsFinite(target.X) && float.IsFinite(target.Y) && float.IsFinite(target.Z);
    private static SquadOrderResult Reject(SquadResult result, string reason) => new(result, $"Rejected: {reason}");
}
