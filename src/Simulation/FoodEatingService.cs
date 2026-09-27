using System.Numerics;

namespace Simulation;

public enum FoodEatingStatus
{
    Traveling,
    Waiting,
    RetryDeferred,
    Ate,
    NotHungry,
    ActionRejected,
    InvalidEntity,
    InvalidSpecies,
    InvalidSelection,
    ArrivalRejected
}

public sealed record FoodEatingResult(
    FoodEatingStatus Status,
    FoodSource? Source = null,
    string? WaitingReason = null,
    NavigationOutcome? NavigationOutcome = null);

public sealed class FoodEatingService
{
    public const double ArrivalTolerance = 0.1;
    public const double NavigationTimeoutSeconds = 30;

    private readonly World _world;
    private readonly FoodSelectionService _selection;
    private readonly Dictionary<string, ActiveNavigation> _activeNavigations = new(StringComparer.Ordinal);

    public FoodEatingService(World world, FoodSelectionService selection)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(selection);
        _world = world;
        _selection = selection;
    }

    public long TotalFoodConsumed { get; private set; }

    public FoodEatingResult Advance(
        EntityRecord entity,
        SpeciesDefinition species,
        INavigationAdapter navigation,
        double simulationSeconds,
        long simulationTick)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(navigation);
        if (!double.IsFinite(simulationSeconds) || simulationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(simulationSeconds),
                "Simulation time must be finite and nonnegative.");
        }

        if (simulationTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationTick), "Simulation tick cannot be negative.");
        }

        var selection = _selection.TrySelect(
            entity,
            species,
            _world.FoodSources,
            simulationTick,
            () => CancelNavigation(entity.Id));
        if (selection.Status != FoodSelectionStatus.Selected)
        {
            CancelNavigation(entity.Id);
            return ToEatingResult(selection);
        }

        var source = selection.Source
            ?? throw new InvalidOperationException("A selected food action must contain a food source.");
        var actionId = selection.ActionId
            ?? throw new InvalidOperationException("A selected food action must contain an action ID.");
        if (!_world.FoodSources.Any(candidate =>
                ReferenceEquals(candidate, source)
                && string.Equals(candidate.Id, source.Id, StringComparison.Ordinal)))
        {
            return HandleFailure(
                entity,
                source,
                FoodTargetFailure.Removed,
                navigation,
                simulationTick);
        }

        if (!_activeNavigations.TryGetValue(entity.Id, out var active)
            || !string.Equals(active.ActionId, actionId, StringComparison.Ordinal)
            || !ReferenceEquals(active.Source, source)
            || !ReferenceEquals(active.Navigation, navigation))
        {
            CancelNavigation(entity.Id);
            active = BeginNavigation(entity, actionId, source, navigation);
        }

        var outcome = navigation.Poll(active.RequestId, simulationSeconds);
        if (outcome == NavigationOutcome.InProgress)
        {
            entity.Activity = "SeekingFood";
            return new FoodEatingResult(
                FoodEatingStatus.Traveling,
                source,
                NavigationOutcome: outcome);
        }

        if (outcome == NavigationOutcome.Arrived)
        {
            if (DistanceSquared(entity.Position, source.Position) > ArrivalTolerance * ArrivalTolerance)
            {
                return HandleFailure(
                    entity,
                    source,
                    FoodTargetFailure.Unreachable,
                    navigation,
                    simulationTick,
                    outcome,
                    arrivalRejected: true);
            }

            return ConsumeAtArrival(entity, source, navigation, simulationTick);
        }

        return HandleFailure(
            entity,
            source,
            outcome == NavigationOutcome.Removed
                ? FoodTargetFailure.Removed
                : FoodTargetFailure.Unreachable,
            navigation,
            simulationTick,
            outcome);
    }

    public bool Cancel(EntityRecord entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CancelNavigation(entity.Id);
        var cancelled = entity.Actions.Current?.Kind == ActionKind.Eat
            && entity.Actions.Cancel();
        if (cancelled && entity.Activity == "SeekingFood")
        {
            entity.Activity = "Idle";
        }

        return cancelled;
    }

    private FoodEatingResult ConsumeAtArrival(
        EntityRecord entity,
        FoodSource source,
        INavigationAdapter navigation,
        long simulationTick)
    {
        if (!source.Contents.TryGetCount(ItemIds.Food, out var available) || available < 1)
        {
            return HandleFailure(
                entity,
                source,
                FoodTargetFailure.Depleted,
                navigation,
                simulationTick);
        }

        if (TotalFoodConsumed == long.MaxValue)
        {
            throw new InvalidOperationException("The food consumption counter cannot be incremented further.");
        }

        var transfer = source.Contents.TryRemove(ItemIds.Food, 1);
        if (transfer != ResourceMutationResult.Success)
        {
            return HandleFailure(
                entity,
                source,
                FoodTargetFailure.Depleted,
                navigation,
                simulationTick);
        }

        entity.Needs.AddNutrition(entity.Needs.NutritionPerUnit);
        TotalFoodConsumed++;
        entity.Actions.Complete();
        entity.Activity = entity.Needs.IsHungry ? "Hungry" : "Idle";
        entity.WaitingReason = null;
        return new FoodEatingResult(FoodEatingStatus.Ate, source);
    }

    private FoodEatingResult HandleFailure(
        EntityRecord entity,
        FoodSource source,
        FoodTargetFailure failure,
        INavigationAdapter navigation,
        long simulationTick,
        NavigationOutcome? outcome = null,
        bool arrivalRejected = false)
    {
        CancelNavigation(entity.Id);
        if (failure == FoodTargetFailure.Removed)
        {
            _world.TryRemoveFoodSource(source.Id);
        }

        var result = _selection.ReportCandidateFailure(
            entity,
            source.Id,
            failure,
            _world.FoodSources,
            simulationTick);
        if (result.Status == FoodSelectionStatus.Selected)
        {
            var alternate = result.Source
                ?? throw new InvalidOperationException("A selected food action must contain a food source.");
            var actionId = result.ActionId
                ?? throw new InvalidOperationException("A selected food action must contain an action ID.");
            BeginNavigation(entity, actionId, alternate, navigation);
            entity.Activity = "SeekingFood";
            return new FoodEatingResult(
                FoodEatingStatus.Traveling,
                alternate,
                NavigationOutcome: outcome);
        }

        return new FoodEatingResult(
            arrivalRejected ? FoodEatingStatus.ArrivalRejected : ToEatingStatus(result.Status),
            WaitingReason: result.WaitingReason,
            NavigationOutcome: outcome);
    }

    private ActiveNavigation BeginNavigation(
        EntityRecord entity,
        string actionId,
        FoodSource source,
        INavigationAdapter navigation)
    {
        var requestId = navigation.Request(
            new NavigationRequest(
                source.Position,
                ArrivalTolerance,
                NavigationTimeoutSeconds));
        var active = new ActiveNavigation(actionId, source, requestId, navigation);
        _activeNavigations.Add(entity.Id, active);
        return active;
    }

    private void CancelNavigation(string entityId)
    {
        if (_activeNavigations.Remove(entityId, out var active))
        {
            active.Navigation.Cancel(active.RequestId);
        }
    }

    private static FoodEatingResult ToEatingResult(FoodSelectionResult result)
    {
        return new FoodEatingResult(
            ToEatingStatus(result.Status),
            result.Source,
            result.WaitingReason);
    }

    private static FoodEatingStatus ToEatingStatus(FoodSelectionStatus status)
    {
        return status switch
        {
            FoodSelectionStatus.Waiting => FoodEatingStatus.Waiting,
            FoodSelectionStatus.RetryDeferred => FoodEatingStatus.RetryDeferred,
            FoodSelectionStatus.NotHungry => FoodEatingStatus.NotHungry,
            FoodSelectionStatus.ActionRejected => FoodEatingStatus.ActionRejected,
            FoodSelectionStatus.InvalidEntity => FoodEatingStatus.InvalidEntity,
            FoodSelectionStatus.InvalidSpecies => FoodEatingStatus.InvalidSpecies,
            FoodSelectionStatus.InvalidSelection => FoodEatingStatus.InvalidSelection,
            FoodSelectionStatus.Selected => FoodEatingStatus.Traveling,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown food selection status.")
        };
    }

    private static double DistanceSquared(Vector3 first, Vector3 second)
    {
        var x = (double)first.X - second.X;
        var y = (double)first.Y - second.Y;
        var z = (double)first.Z - second.Z;
        return x * x + y * y + z * z;
    }

    private sealed record ActiveNavigation(
        string ActionId,
        FoodSource Source,
        string RequestId,
        INavigationAdapter Navigation);
}
