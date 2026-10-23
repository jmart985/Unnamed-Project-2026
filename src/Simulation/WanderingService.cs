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

    private readonly World _world;
    private readonly Func<EntityRecord, INavigationAdapter> _navigationForEntity;
    private readonly IReadOnlyList<Vector3> _goals;
    private readonly int _decisionSeed;
    private readonly Dictionary<string, ActiveTrip> _activeTrips = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _tripCounts = new(StringComparer.Ordinal);

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

        if (entity.Needs.IsHungry)
        {
            CancelTrip(entity);
            return new WanderingResult(WanderingStatus.NeedsFood);
        }

        if (_activeTrips.TryGetValue(entity.Id, out var activeTrip))
        {
            if (entity.Actions.Current?.Id != activeTrip.ActionId)
            {
                CancelTrip(entity);
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

        return BeginTrip(entity, species, simulationTick);
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
    }

    private WanderingResult BeginTrip(
        EntityRecord entity,
        SpeciesDefinition species,
        long simulationTick)
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
        var firstGoalIndex = GetStableStartIndex(entity.Id, simulationTick, tripCount, _goals.Count);
        var target = SelectGoal(entity, behavior, firstGoalIndex);
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

            _activeTrips[entity.Id] = new ActiveTrip(actionId, target, behavior, navigator, requestId);
            entity.Activity = "Wandering";
            entity.WaitingReason = null;
            return new WanderingResult(WanderingStatus.Traveling, target, Behavior: behavior);
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
                "Wandering navigation could not be started.",
                behavior);
        }
    }

    private WanderingResult AdvanceTrip(EntityRecord entity, ActiveTrip trip)
    {
        var outcome = trip.Navigator.Poll(trip.NavigationRequestId, 0);
        if (outcome == NavigationOutcome.InProgress)
        {
            return new WanderingResult(WanderingStatus.Traveling, trip.Target, Behavior: trip.Behavior);
        }

        if (outcome == NavigationOutcome.Arrived
            && IsWithinArrivalTolerance(entity.Position, trip.Target))
        {
            _activeTrips.Remove(entity.Id);
            entity.Actions.Complete();
            entity.Activity = "Idle";
            entity.WaitingReason = null;
            return new WanderingResult(WanderingStatus.Arrived, trip.Target, Behavior: trip.Behavior);
        }

        CancelTrip(entity);
        entity.Activity = "Waiting";
        entity.WaitingReason = $"Wandering target failed: {outcome}.";
        return new WanderingResult(WanderingStatus.Waiting, trip.Target, entity.WaitingReason, trip.Behavior);
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
        string NavigationRequestId);
}
