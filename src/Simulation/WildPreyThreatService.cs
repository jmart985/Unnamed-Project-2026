using System.Numerics;

namespace Simulation;

public enum WildPreyThreatStatus
{
    NotEligible,
    NoThreat,
    Waiting,
    Fleeing
}

public sealed record WildPreyThreatResult(
    WildPreyThreatStatus Status,
    string? ThreatEntityId = null,
    Vector3? FleeTarget = null,
    string? Reason = null);

/// <summary>Moves wild prey away from configured predators using transient navigation.</summary>
public sealed class WildPreyThreatService : ITransientSimulationState
{
    private const double FleeDistance = 10;
    private const double ArrivalTolerance = 1.2;
    private const double NavigationTimeoutSeconds = 30;
    private const double RepathDistance = 2;

    private readonly World _world;
    private readonly SpeciesCatalog _catalog;
    private readonly Func<EntityRecord, INavigationAdapter> _navigationForEntity;
    private readonly Dictionary<string, ActiveFlee> _activeFlees = new(StringComparer.Ordinal);
    private readonly HashSet<string> _suppressedEntities = new(StringComparer.Ordinal);
    private readonly RetryScheduler _retries = new();
    private long _nextActionId;

    public WildPreyThreatService(
        World world,
        SpeciesCatalog catalog,
        Func<EntityRecord, INavigationAdapter> navigationForEntity)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _navigationForEntity = navigationForEntity ?? throw new ArgumentNullException(nameof(navigationForEntity));
    }

    public bool IsFleeing(string? entityId) =>
        !string.IsNullOrWhiteSpace(entityId) && _activeFlees.ContainsKey(entityId);

    public bool SuppressesOtherBehavior(string? entityId) =>
        !string.IsNullOrWhiteSpace(entityId) && _suppressedEntities.Contains(entityId);

    /// <summary>Releases transient escape state when the world removes an entity.</summary>
    public void OnEntityRemoved(string? entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
            return;
        if (_activeFlees.Remove(entityId, out var active))
        {
            CancelNavigation(active);
            if (_world.TryGetEntity(entityId, out var entity) && entity is not null)
            {
                if (entity.Actions.Current?.Id == active.ActionId)
                    entity.Actions.Complete();
                if (string.Equals(entity.TargetId, active.ThreatEntityId, StringComparison.Ordinal))
                    entity.TargetId = null;
            }
        }
        _suppressedEntities.Remove(entityId);
        _retries.Clear(entityId);
    }

    public WildPreyThreatResult Advance(EntityRecord prey, long simulationTick)
    {
        ArgumentNullException.ThrowIfNull(prey);
        if (simulationTick < 0)
            throw new ArgumentOutOfRangeException(nameof(simulationTick));
        if (!_world.TryGetEntity(prey.Id, out var livePrey) || !ReferenceEquals(prey, livePrey))
        {
            CancelFlee(prey);
            _suppressedEntities.Remove(prey.Id);
            _retries.Clear(prey.Id);
            return new(WildPreyThreatStatus.NotEligible, Reason: "The prey is not part of this world.");
        }
        if (!_catalog.TryGet(prey.SpeciesId, out var preySpecies) || preySpecies is null
            || prey.CaptivityState != CreatureCaptivityState.Wild
            || prey.Health.IsDead || prey.Health.IsIncapacitated
            || preySpecies.ThreatDetectionRange <= 0)
        {
            CancelFlee(prey);
            ClearFailedEscapeWaiting(prey);
            _suppressedEntities.Remove(prey.Id);
            return new(WildPreyThreatStatus.NotEligible);
        }

        var threat = SelectThreat(prey, preySpecies);
        if (threat is null)
        {
            CancelFlee(prey);
            ClearFailedEscapeWaiting(prey);
            _suppressedEntities.Remove(prey.Id);
            _retries.Clear(prey.Id);
            return new(WildPreyThreatStatus.NoThreat);
        }

        var decision = WildCreatureDecisionPolicy.Decide(
            prey,
            new WildCreatureDecisionContext(threat: new WildCreatureDecisionTarget(threat.Id, threat.Position)));
        if (decision.Decision != WildCreatureDecision.Flee || decision.Target is null)
            return new(WildPreyThreatStatus.NoThreat);

        if (_activeFlees.TryGetValue(prey.Id, out var active)
            && prey.Actions.Current?.Id == active.ActionId)
        {
            _suppressedEntities.Add(prey.Id);
            return AdvanceFlee(prey, active, threat, simulationTick);
        }

        CancelFlee(prey);
        if (_retries.IsWaiting(prey.Id, simulationTick))
        {
            _suppressedEntities.Add(prey.Id);
            return new(WildPreyThreatStatus.Waiting, threat.Id, Reason: "Waiting before retrying the escape route.");
        }

        var result = StartFlee(prey, threat, simulationTick);
        if (result.Status is WildPreyThreatStatus.Fleeing or WildPreyThreatStatus.Waiting)
            _suppressedEntities.Add(prey.Id);
        return result;
    }

    public void Reset()
    {
        foreach (var entityId in _activeFlees.Keys.ToArray())
            if (_world.TryGetEntity(entityId, out var entity) && entity is not null)
                CancelFlee(entity);
        _activeFlees.Clear();
        _suppressedEntities.Clear();
        _retries.Reset();
        _nextActionId = 0;
    }

    private EntityRecord? SelectThreat(EntityRecord prey, SpeciesDefinition preySpecies)
    {
        var rangeSquared = preySpecies.ThreatDetectionRange * preySpecies.ThreatDetectionRange;
        return _world.Entities
            .Where(candidate => !ReferenceEquals(candidate, prey)
                && !candidate.Health.IsDead
                && !candidate.Health.IsIncapacitated
                && _catalog.TryGet(candidate.SpeciesId, out var candidateSpecies)
                && candidateSpecies is { IsPredator: true }
                && candidateSpecies.PreySpeciesIds.Contains(preySpecies.Id, StringComparer.Ordinal)
                && !AreFriendly(prey, candidate)
                && DistanceSquared(prey.Position, candidate.Position) <= rangeSquared)
            .OrderBy(candidate => DistanceSquared(prey.Position, candidate.Position))
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private WildPreyThreatResult StartFlee(EntityRecord prey, EntityRecord threat, long simulationTick)
    {
        var actionId = $"flee-{prey.Id}-{checked(++_nextActionId):D6}";
        var active = new ActiveFlee(actionId, threat.Id, threat.Position);
        var start = prey.Actions.TryStart(
            new ActionCandidate(actionId, ActionKind.Flee),
            () => OnActionFinished(prey.Id, active));
        if (start is not (ActionStartResult.Started or ActionStartResult.Preempted))
            return new(WildPreyThreatStatus.Waiting, threat.Id, Reason: "Another higher-priority action is active.");

        _activeFlees[prey.Id] = active;
        prey.TargetId = threat.Id;
        prey.Activity = "Fleeing";
        prey.WaitingReason = null;
        if (!RequestNavigation(prey, active, threat))
        {
            prey.Actions.Cancel();
            _retries.ScheduleRetry(prey.Id, simulationTick);
            prey.Activity = "Waiting";
            prey.WaitingReason = "The escape route could not be started.";
            return new(WildPreyThreatStatus.Waiting, threat.Id, Reason: prey.WaitingReason);
        }

        _retries.Clear(prey.Id);
        return new(WildPreyThreatStatus.Fleeing, threat.Id, active.NavigationTarget);
    }

    private WildPreyThreatResult AdvanceFlee(
        EntityRecord prey,
        ActiveFlee active,
        EntityRecord threat,
        long simulationTick)
    {
        if (!string.Equals(active.ThreatEntityId, threat.Id, StringComparison.Ordinal)
            || DistanceSquared(active.LastThreatPosition, threat.Position) >= RepathDistance * RepathDistance)
        {
            CancelNavigation(active);
            if (!RequestNavigation(prey, active, threat))
                return FailFlee(prey, active, threat, simulationTick, "The updated escape route could not be started.");
        }

        var outcome = active.Navigator!.Poll(active.NavigationRequestId!, 0);
        if (outcome == NavigationOutcome.InProgress)
            return new(WildPreyThreatStatus.Fleeing, threat.Id, active.NavigationTarget);
        if (outcome == NavigationOutcome.Arrived)
        {
            CancelNavigation(active);
            if (!RequestNavigation(prey, active, threat))
                return FailFlee(prey, active, threat, simulationTick, "A follow-up escape route could not be started.");
            return new(WildPreyThreatStatus.Fleeing, threat.Id, active.NavigationTarget);
        }

        return FailFlee(prey, active, threat, simulationTick, $"The escape route failed: {outcome}.");
    }

    private WildPreyThreatResult FailFlee(
        EntityRecord prey,
        ActiveFlee active,
        EntityRecord threat,
        long simulationTick,
        string reason)
    {
        CancelFlee(prey);
        _suppressedEntities.Add(prey.Id);
        _retries.ScheduleRetry(prey.Id, simulationTick);
        prey.Activity = "Waiting";
        prey.WaitingReason = reason;
        return new(WildPreyThreatStatus.Waiting, threat.Id, Reason: reason);
    }

    private bool RequestNavigation(EntityRecord prey, ActiveFlee active, EntityRecord threat)
    {
        try
        {
            active.Navigator = _navigationForEntity(prey)
                ?? throw new InvalidOperationException("The prey has no navigation adapter.");
            active.ThreatEntityId = threat.Id;
            active.LastThreatPosition = threat.Position;
            prey.TargetId = threat.Id;
            active.NavigationTarget = CreateFleeTarget(prey.Position, threat.Position);
            active.NavigationRequestId = active.Navigator.Request(new NavigationRequest(
                active.NavigationTarget.Value,
                ArrivalTolerance,
                NavigationTimeoutSeconds));
            return !string.IsNullOrWhiteSpace(active.NavigationRequestId);
        }
        catch
        {
            CancelNavigation(active);
            return false;
        }
    }

    private static Vector3 CreateFleeTarget(Vector3 preyPosition, Vector3 threatPosition)
    {
        var direction = new Vector3(preyPosition.X - threatPosition.X, 0, preyPosition.Z - threatPosition.Z);
        if (direction.LengthSquared() <= 0.0001f)
            direction = Vector3.UnitX;
        else
            direction = Vector3.Normalize(direction);
        return preyPosition + direction * (float)FleeDistance;
    }

    private void CancelFlee(EntityRecord prey)
    {
        if (!_activeFlees.Remove(prey.Id, out var active))
            return;
        CancelNavigation(active);
        if (prey.Actions.Current?.Id == active.ActionId)
            prey.Actions.Complete();
        if (string.Equals(prey.TargetId, active.ThreatEntityId, StringComparison.Ordinal))
            prey.TargetId = null;
        if (prey.Activity == "Fleeing" || prey.Activity == "Waiting")
        {
            prey.Activity = "Idle";
            prey.WaitingReason = null;
        }
    }

    private static void ClearFailedEscapeWaiting(EntityRecord prey)
    {
        if (prey.Activity == "Waiting"
            && prey.WaitingReason?.Contains("escape route", StringComparison.Ordinal) == true)
        {
            prey.Activity = "Idle";
            prey.WaitingReason = null;
        }
    }

    private void OnActionFinished(string preyId, ActiveFlee active)
    {
        if (!_activeFlees.TryGetValue(preyId, out var current) || !ReferenceEquals(current, active))
            return;
        _activeFlees.Remove(preyId);
        CancelNavigation(active);
        if (_world.TryGetEntity(preyId, out var prey) && prey is not null)
        {
            if (string.Equals(prey.TargetId, active.ThreatEntityId, StringComparison.Ordinal))
                prey.TargetId = null;
            if (prey.Activity == "Fleeing")
                prey.Activity = "Idle";
        }
    }

    private static void CancelNavigation(ActiveFlee active)
    {
        if (active.Navigator is not null && active.NavigationRequestId is not null)
            active.Navigator.Cancel(active.NavigationRequestId);
        active.NavigationRequestId = null;
        active.NavigationTarget = null;
    }

    private static bool AreFriendly(EntityRecord first, EntityRecord second) =>
        first.AllegianceFactionId is { } factionId
        && string.Equals(factionId, second.AllegianceFactionId, StringComparison.Ordinal);

    private static double DistanceSquared(Vector3 first, Vector3 second)
    {
        var x = (double)first.X - second.X;
        var y = (double)first.Y - second.Y;
        var z = (double)first.Z - second.Z;
        return x * x + y * y + z * z;
    }

    private sealed class ActiveFlee(string actionId, string threatEntityId, Vector3 lastThreatPosition)
    {
        public string ActionId { get; } = actionId;
        public string ThreatEntityId { get; set; } = threatEntityId;
        public Vector3 LastThreatPosition { get; set; } = lastThreatPosition;
        public INavigationAdapter? Navigator { get; set; }
        public string? NavigationRequestId { get; set; }
        public Vector3? NavigationTarget { get; set; }
    }
}
