using System.Numerics;

namespace Simulation;

public enum WanderingStatus
{
    NotWanderer,
    Traveling,
    Arrived,
    ReturningToTerritory,
    ReturnedToTerritory,
    NeedsFood,
    Waiting,
    ActionRejected,
    InvalidEntity,
    InvalidSpecies
}

public enum WildBehavior
{
    CautiousRoaming,
    CuriousExploration,
    SocialRoaming
}

public sealed record WanderingResult(
    WanderingStatus Status,
    Vector3? Target = null,
    string? WaitingReason = null,
    WildBehavior? Behavior = null);

public sealed class WanderingService : ITransientSimulationState
{
    private const double NavigationTimeoutSeconds = 30;
    private const double ArrivalTolerance = 0.1;
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;
    private const int TerritoryGoalCount = 16;
    private const double GoldenAngleRadians = 2.399963229728653;

    private readonly World _world;
    private readonly Func<EntityRecord, INavigationAdapter> _navigationForEntity;
    private readonly IReadOnlyList<Vector3> _goals;
    private readonly int _decisionSeed;
    private readonly Dictionary<string, ActiveTrip> _activeTrips = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _tripCounts = new(StringComparer.Ordinal);
    private readonly RetryScheduler _returnRetries = new();

    public WanderingService(
        World world,
        Func<EntityRecord, INavigationAdapter> navigationForEntity,
        IEnumerable<Vector3> goals,
        int decisionSeed = 0)
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
        _decisionSeed = decisionSeed;
    }

    public WanderingResult Advance(
        EntityRecord entity,
        SpeciesDefinition species,
        long simulationTick = 0)
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

        var territory = entity.SpeciesId != "human"
            && entity.CaptivityState == CreatureCaptivityState.Wild
            && _world.TryGetWildTerritory(entity.Id, out var assignedTerritory)
                ? assignedTerritory
                : null;
        var isOutsideTerritory = territory is not null && !territory.Contains(entity.Position);

        // A displaced wild creature must be able to return home before its food
        // search can resume. Hungry creatures inside their territory still
        // interrupt ordinary roaming to seek food.
        if (entity.Needs.IsHungry && !isOutsideTerritory)
        {
            CancelTrip(entity);
            return new WanderingResult(WanderingStatus.NeedsFood);
        }

        if (territory is null || territory.Contains(entity.Position))
            _returnRetries.Clear(entity.Id);

        if (_activeTrips.TryGetValue(entity.Id, out var activeTrip))
        {
            if (activeTrip.IsReturnTrip && !isOutsideTerritory && activeTrip.Territory == territory)
            {
                _activeTrips.Remove(entity.Id);
                activeTrip.Navigator.Cancel(activeTrip.NavigationRequestId);
                if (entity.Actions.Current?.Id == activeTrip.ActionId)
                    entity.Actions.Complete();
                entity.Activity = "Idle";
                entity.WaitingReason = null;
                _returnRetries.Clear(entity.Id);
                return new WanderingResult(
                    WanderingStatus.ReturnedToTerritory,
                    entity.Position,
                    Behavior: activeTrip.Behavior);
            }

            if (entity.Actions.Current?.Id != activeTrip.ActionId
                || activeTrip.Territory != territory
                || (!activeTrip.IsReturnTrip && isOutsideTerritory))
            {
                CancelTrip(entity);
            }
            else
            {
                return AdvanceTrip(entity, activeTrip, simulationTick);
            }
        }

        if (entity.Actions.Current is not null)
        {
            return new WanderingResult(WanderingStatus.ActionRejected);
        }

        if (isOutsideTerritory)
        {
            if (_returnRetries.IsWaiting(entity.Id, simulationTick))
            {
                entity.Activity = "Waiting";
                entity.WaitingReason = "Waiting before retrying the territory return route.";
                return new WanderingResult(
                    WanderingStatus.Waiting,
                    territory!.Center,
                    entity.WaitingReason);
            }

            var returnTarget = new WildCreatureDecisionTarget($"territory-{entity.Id}", territory!.Center);
            var decision = WildCreatureDecisionPolicy.Decide(
                entity,
                new WildCreatureDecisionContext(isOutsideArea: true, returnAreaTarget: returnTarget));
            if (decision.Decision == WildCreatureDecision.ReturnToArea)
                return BeginTrip(entity, species, simulationTick, territory, decision.Target!.Position, isReturnTrip: true);
        }

        return BeginTrip(entity, species, simulationTick, territory, targetOverride: null, isReturnTrip: false);
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
        _tripCounts.Clear();
        _returnRetries.Reset();
    }

    private WanderingResult BeginTrip(
        EntityRecord entity,
        SpeciesDefinition species,
        long simulationTick,
        WildTerritory? territory,
        Vector3? targetOverride,
        bool isReturnTrip)
    {
        var temperament = IndividualTemperament.ForEntity(entity.Id, species.Temperament);
        var hasSameSpeciesCompanions = _world.Entities.Any(other =>
            !string.Equals(other.Id, entity.Id, StringComparison.Ordinal)
            && string.Equals(other.SpeciesId, species.Id, StringComparison.Ordinal));
        var behavior = hasSameSpeciesCompanions && temperament.Sociability >= 0.7
            ? WildBehavior.SocialRoaming
            : temperament.Boldness + temperament.Curiosity >= 0.68
                ? WildBehavior.CuriousExploration
                : WildBehavior.CautiousRoaming;
        var tripCount = _tripCounts.GetValueOrDefault(entity.Id);
        var firstGoalIndex = GetStableStartIndex(
            entity.Id,
            simulationTick,
            tripCount,
            territory is null ? _goals.Count : TerritoryGoalCount);
        var target = targetOverride ?? (territory is null
            ? SelectGoal(entity, behavior, firstGoalIndex)
            : SelectTerritoryGoal(entity, behavior, territory, firstGoalIndex));
        _tripCounts[entity.Id] = checked(tripCount + 1);

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

            var actionId = $"wander-{entity.Id}-{tripCount}";
            var startResult = entity.Actions.TryStart(
                new ActionCandidate(actionId, ActionKind.Wander),
                () => navigator.Cancel(requestId));
            if (startResult is not (ActionStartResult.Started or ActionStartResult.Preempted))
            {
                navigator.Cancel(requestId);
                return new WanderingResult(WanderingStatus.ActionRejected, target, Behavior: behavior);
            }

            _activeTrips[entity.Id] = new ActiveTrip(actionId, target, behavior, navigator, requestId, territory, isReturnTrip);
            entity.Activity = isReturnTrip ? "ReturningToTerritory" : "Wandering";
            entity.WaitingReason = null;
            return new WanderingResult(
                isReturnTrip ? WanderingStatus.ReturningToTerritory : WanderingStatus.Traveling,
                target,
                Behavior: behavior);
        }
        catch
        {
            if (requestId is not null && navigator is not null)
            {
                navigator.Cancel(requestId);
            }

            if (isReturnTrip)
                _returnRetries.ScheduleRetry(entity.Id, simulationTick);

            return new WanderingResult(
                WanderingStatus.Waiting,
                target,
                "Wandering navigation could not be started.",
                behavior);
        }
    }

    private WanderingResult AdvanceTrip(EntityRecord entity, ActiveTrip trip, long simulationTick)
    {
        var outcome = trip.Navigator.Poll(trip.NavigationRequestId, 0);
        if (outcome == NavigationOutcome.InProgress)
        {
            return new WanderingResult(
                trip.IsReturnTrip ? WanderingStatus.ReturningToTerritory : WanderingStatus.Traveling,
                trip.Target,
                Behavior: trip.Behavior);
        }

        if (outcome == NavigationOutcome.Arrived
            && IsWithinArrivalTolerance(entity.Position, trip.Target))
        {
            _activeTrips.Remove(entity.Id);
            entity.Actions.Complete();
            entity.Activity = "Idle";
            entity.WaitingReason = null;
            if (trip.IsReturnTrip)
            {
                _returnRetries.Clear(entity.Id);
                return new WanderingResult(WanderingStatus.ReturnedToTerritory, trip.Target, Behavior: trip.Behavior);
            }
            return new WanderingResult(WanderingStatus.Arrived, trip.Target, Behavior: trip.Behavior);
        }

        CancelTrip(entity);
        entity.Activity = "Waiting";
        entity.WaitingReason = trip.IsReturnTrip
            ? $"Territory return route failed: {outcome}; retry deferred."
            : $"Wandering target failed: {outcome}.";
        if (trip.IsReturnTrip)
            _returnRetries.ScheduleRetry(entity.Id, simulationTick);
        return new WanderingResult(WanderingStatus.Waiting, trip.Target, entity.WaitingReason, trip.Behavior);
    }

    private static Vector3 SelectTerritoryGoal(
        EntityRecord entity,
        WildBehavior behavior,
        WildTerritory territory,
        int firstGoalIndex)
    {
        var bestGoal = territory.Center;
        var bestDistance = 0f;
        var foundGoal = false;
        for (var offset = 0; offset < TerritoryGoalCount; offset++)
        {
            var candidateIndex = (firstGoalIndex + offset) % TerritoryGoalCount;
            var radialFraction = Math.Sqrt((candidateIndex + 0.5) / TerritoryGoalCount) * 0.9;
            var angle = candidateIndex * GoldenAngleRadians;
            var candidate = territory.Center + new Vector3(
                (float)(Math.Cos(angle) * territory.Radius * radialFraction),
                0,
                (float)(Math.Sin(angle) * territory.Radius * radialFraction));
            var candidateDistance = Vector3.DistanceSquared(entity.Position, candidate);
            if (!territory.Contains(candidate) || candidateDistance <= ArrivalTolerance * ArrivalTolerance)
                continue;

            if (!foundGoal
                || (behavior == WildBehavior.CuriousExploration
                    ? candidateDistance > bestDistance
                    : candidateDistance < bestDistance))
            {
                bestGoal = candidate;
                bestDistance = candidateDistance;
                foundGoal = true;
            }
        }

        return bestGoal;
    }

    private Vector3 SelectGoal(EntityRecord entity, WildBehavior behavior, int firstGoalIndex)
    {
        var origin = entity.Position;
        var groupCenter = behavior == WildBehavior.SocialRoaming
            ? GetCompanionCenter(entity)
            : origin;
        var bestGoal = _goals[firstGoalIndex];
        var bestDistance = 0f;
        var foundGoal = false;
        for (var offset = 0; offset < _goals.Count; offset++)
        {
            var candidate = _goals[(firstGoalIndex + offset) % _goals.Count];
            var candidateDistance = Vector3.DistanceSquared(groupCenter, candidate);
            if (candidateDistance <= ArrivalTolerance * ArrivalTolerance)
            {
                continue;
            }

            if (!foundGoal
                || (behavior == WildBehavior.CuriousExploration
                    ? candidateDistance > bestDistance
                    : candidateDistance < bestDistance))
            {
                bestGoal = candidate;
                bestDistance = candidateDistance;
                foundGoal = true;
            }
        }

        return bestGoal;
    }

    private Vector3 GetCompanionCenter(EntityRecord entity)
    {
        var companions = _world.Entities
            .Where(other => !string.Equals(other.Id, entity.Id, StringComparison.Ordinal)
                            && string.Equals(other.SpeciesId, entity.SpeciesId, StringComparison.Ordinal))
            .ToArray();
        if (companions.Length == 0)
        {
            return entity.Position;
        }

        var center = Vector3.Zero;
        foreach (var companion in companions)
        {
            center += companion.Position;
        }

        return center / companions.Length;
    }

    private int GetStableStartIndex(string entityId, long simulationTick, int tripCount, int goalCount)
    {
        var hash = FnvOffsetBasis;
        AddToHash(unchecked((uint)_decisionSeed), ref hash);
        AddToHash(unchecked((ulong)simulationTick), ref hash);
        AddToHash(unchecked((uint)tripCount), ref hash);
        foreach (var character in entityId)
        {
            AddToHash(character, ref hash);
        }

        return (int)(hash % (uint)goalCount);
    }

    private static void AddToHash(uint value, ref uint hash)
    {
        AddToHash((ulong)value, ref hash);
    }

    private static void AddToHash(ulong value, ref uint hash)
    {
        for (var byteIndex = 0; byteIndex < sizeof(ulong); byteIndex++)
        {
            hash = unchecked((hash ^ (byte)value) * FnvPrime);
            value >>= 8;
        }
    }

    private static void AddToHash(char value, ref uint hash)
    {
        hash = unchecked((hash ^ (byte)value) * FnvPrime);
        hash = unchecked((hash ^ (byte)(value >> 8)) * FnvPrime);
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
        WildBehavior Behavior,
        INavigationAdapter Navigator,
        string NavigationRequestId,
        WildTerritory? Territory,
        bool IsReturnTrip);
}
