using System.Globalization;
using System.Numerics;

namespace Simulation;

public enum FoodTargetFailure
{
    Depleted,
    Unreachable,
    Removed
}

public enum FoodSelectionStatus
{
    Selected,
    Waiting,
    RetryDeferred,
    NotHungry,
    ActionRejected,
    InvalidEntity,
    InvalidSpecies,
    InvalidSelection
}

public sealed class FoodSource
{
    public FoodSource(string id, Vector3 position, ResourceContainer contents)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A food source ID is required.", nameof(id));
        }

        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position coordinates must be finite.");
        }

        ArgumentNullException.ThrowIfNull(contents);
        Id = id;
        Position = position;
        Contents = contents;
    }

    public string Id { get; }
    public Vector3 Position { get; }
    public ResourceContainer Contents { get; }
}

public sealed record FoodSelectionResult(
    FoodSelectionStatus Status,
    FoodSource? Source = null,
    string? ActionId = null,
    string? WaitingReason = null);

public sealed class FoodSelectionService
{
    public const int DefaultRetryDelayTicks = 10;

    private readonly World _world;
    private readonly int _retryDelayTicks;
    private readonly Dictionary<string, SelectionAttempt> _attempts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _retryAtTicks = new(StringComparer.Ordinal);
    private long _nextActionId;

    public FoodSelectionService(World world, int retryDelayTicks = DefaultRetryDelayTicks)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (retryDelayTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelayTicks), "Retry delay must be positive.");
        }

        _world = world;
        _retryDelayTicks = retryDelayTicks;
    }

    public FoodSelectionResult TrySelect(
        EntityRecord entity,
        SpeciesDefinition species,
        IEnumerable<FoodSource> sources,
        long simulationTick,
        Action? actionCleanup = null)
    {
        ValidateInputs(entity, species, sources, simulationTick);
        var candidates = ValidateSources(sources);
        if (!OwnsEntity(entity))
        {
            return new FoodSelectionResult(FoodSelectionStatus.InvalidEntity);
        }

        if (!string.Equals(entity.SpeciesId, species.Id, StringComparison.Ordinal))
        {
            return new FoodSelectionResult(FoodSelectionStatus.InvalidSpecies);
        }

        if (!entity.Needs.IsHungry)
        {
            var shouldReturnToIdle = entity.Activity is "SeekingFood" or "Waiting";
            CancelAttempt(entity);
            _retryAtTicks.Remove(entity.Id);
            if (shouldReturnToIdle && entity.Actions.Current is null)
            {
                entity.Activity = "Idle";
            }

            entity.WaitingReason = null;
            return new FoodSelectionResult(FoodSelectionStatus.NotHungry);
        }

        if (_attempts.TryGetValue(entity.Id, out var currentAttempt))
        {
            if (!IsCurrentAttempt(entity, currentAttempt))
            {
                _attempts.Remove(entity.Id);
            }
            else if (currentAttempt.SelectedSource is not null)
            {
                return new FoodSelectionResult(
                    FoodSelectionStatus.Selected,
                    currentAttempt.SelectedSource,
                    currentAttempt.ActionId);
            }
            else
            {
                return TrySelectCandidate(entity, currentAttempt, candidates, simulationTick);
            }
        }

        if (_retryAtTicks.TryGetValue(entity.Id, out var retryAtTick))
        {
            if (simulationTick < retryAtTick)
            {
                return new FoodSelectionResult(
                    FoodSelectionStatus.RetryDeferred,
                    WaitingReason: entity.WaitingReason);
            }

            _retryAtTicks.Remove(entity.Id);
        }

        var actionId = $"eat-{entity.Id}-{(++_nextActionId).ToString("D6", CultureInfo.InvariantCulture)}";
        var attempt = new SelectionAttempt(actionId, species, actionCleanup);
        var startResult = entity.Actions.TryStart(
            new ActionCandidate(actionId, ActionKind.Eat),
            () => OnActionFinished(entity.Id, actionId));
        if (startResult == ActionStartResult.Rejected)
        {
            return new FoodSelectionResult(FoodSelectionStatus.ActionRejected);
        }

        _attempts.Add(entity.Id, attempt);
        return TrySelectCandidate(entity, attempt, candidates, simulationTick);
    }

    public FoodSelectionResult ReportCandidateFailure(
        EntityRecord entity,
        string? targetId,
        FoodTargetFailure failure,
        IEnumerable<FoodSource> sources,
        long simulationTick)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(sources);
        if (simulationTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationTick), "Simulation tick cannot be negative.");
        }

        var candidates = ValidateSources(sources);
        if (!OwnsEntity(entity))
        {
            return new FoodSelectionResult(FoodSelectionStatus.InvalidEntity);
        }

        if (!Enum.IsDefined(failure)
            || string.IsNullOrWhiteSpace(targetId)
            || !_attempts.TryGetValue(entity.Id, out var attempt)
            || !IsCurrentAttempt(entity, attempt)
            || !string.Equals(attempt.SelectedSource?.Id, targetId, StringComparison.Ordinal))
        {
            return new FoodSelectionResult(FoodSelectionStatus.InvalidSelection);
        }

        if (failure == FoodTargetFailure.Removed)
        {
            _world.Reservations.RemoveTarget(targetId);
        }

        _world.Reservations.ReleaseForAction(entity.Id, attempt.ActionId);
        attempt.ExcludedTargetIds.Add(targetId);
        attempt.LastFailure = failure;
        attempt.SelectedSource = null;
        return TrySelectCandidate(entity, attempt, candidates, simulationTick);
    }

    public void Reset()
    {
        foreach (var pair in _attempts.ToArray())
        {
            if (_world.TryGetEntity(pair.Key, out var entity)
                && entity is not null
                && string.Equals(entity.Actions.Current?.Id, pair.Value.ActionId, StringComparison.Ordinal))
            {
                entity.Actions.Cancel();
            }
        }

        _attempts.Clear();
        _retryAtTicks.Clear();
    }

    private FoodSelectionResult TrySelectCandidate(
        EntityRecord entity,
        SelectionAttempt attempt,
        IReadOnlyCollection<FoodSource> sources,
        long simulationTick)
    {
        var compatibleSources = sources
            .Where(source => attempt.Species.Eats(ItemIds.Food))
            .Where(source => !attempt.ExcludedTargetIds.Contains(source.Id))
            .OrderBy(source => DistanceSquared(entity.Position, source.Position))
            .ThenBy(source => source.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (var source in compatibleSources)
        {
            if (!source.Contents.TryGetCount(ItemIds.Food, out var count) || count <= 0)
            {
                continue;
            }

            var reservation = _world.Reservations.TryReserveResource(
                entity.Id,
                attempt.ActionId,
                source.Id,
                source.Contents,
                ItemIds.Food,
                1);
            if (reservation != ReservationResult.Reserved)
            {
                continue;
            }

            attempt.SelectedSource = source;
            entity.Activity = "SeekingFood";
            entity.WaitingReason = null;
            _retryAtTicks.Remove(entity.Id);
            return new FoodSelectionResult(FoodSelectionStatus.Selected, source, attempt.ActionId);
        }

        entity.Actions.Cancel();
        entity.Activity = "Waiting";
        entity.WaitingReason = GetWaitingReason(attempt, sources);
        _retryAtTicks[entity.Id] = simulationTick > long.MaxValue - _retryDelayTicks
            ? long.MaxValue
            : simulationTick + _retryDelayTicks;
        return new FoodSelectionResult(
            FoodSelectionStatus.Waiting,
            WaitingReason: entity.WaitingReason);
    }

    private string GetWaitingReason(SelectionAttempt attempt, IReadOnlyCollection<FoodSource> sources)
    {
        if (!sources.Any(source => attempt.Species.Eats(ItemIds.Food)))
        {
            return "No compatible food available.";
        }

        return attempt.LastFailure == FoodTargetFailure.Unreachable
            ? "No reachable food available."
            : "No available food.";
    }

    private bool OwnsEntity(EntityRecord entity)
    {
        return _world.TryGetEntity(entity.Id, out var worldEntity)
            && ReferenceEquals(entity, worldEntity);
    }

    private bool IsCurrentAttempt(EntityRecord entity, SelectionAttempt attempt)
    {
        return string.Equals(entity.Actions.Current?.Id, attempt.ActionId, StringComparison.Ordinal);
    }

    private void CancelAttempt(EntityRecord entity)
    {
        if (_attempts.TryGetValue(entity.Id, out var attempt)
            && IsCurrentAttempt(entity, attempt))
        {
            entity.Actions.Cancel();
        }
        else
        {
            _attempts.Remove(entity.Id);
        }
    }

    private void OnActionFinished(string entityId, string actionId)
    {
        if (_attempts.TryGetValue(entityId, out var attempt)
            && string.Equals(attempt.ActionId, actionId, StringComparison.Ordinal))
        {
            _attempts.Remove(entityId);
            attempt.ActionCleanup?.Invoke();
        }
    }

    private static double DistanceSquared(Vector3 first, Vector3 second)
    {
        var x = (double)first.X - second.X;
        var y = (double)first.Y - second.Y;
        var z = (double)first.Z - second.Z;
        return x * x + y * y + z * z;
    }

    private static void ValidateInputs(
        EntityRecord entity,
        SpeciesDefinition species,
        IEnumerable<FoodSource> sources,
        long simulationTick)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(sources);
        if (simulationTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationTick), "Simulation tick cannot be negative.");
        }
    }

    private static FoodSource[] ValidateSources(IEnumerable<FoodSource> sources)
    {
        var candidates = sources.ToArray();
        if (candidates.Any(source => source is null))
        {
            throw new ArgumentException("Food sources cannot contain null entries.", nameof(sources));
        }

        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        if (candidates.Any(source => !sourceIds.Add(source.Id)))
        {
            throw new ArgumentException("Food source IDs must be unique.", nameof(sources));
        }

        return candidates;
    }

    private sealed class SelectionAttempt(
        string actionId,
        SpeciesDefinition species,
        Action? actionCleanup)
    {
        public string ActionId { get; } = actionId;
        public SpeciesDefinition Species { get; } = species;
        public Action? ActionCleanup { get; } = actionCleanup;
        public HashSet<string> ExcludedTargetIds { get; } = new(StringComparer.Ordinal);
        public FoodSource? SelectedSource { get; set; }
        public FoodTargetFailure? LastFailure { get; set; }
    }
}
