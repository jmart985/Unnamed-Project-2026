using System.Numerics;

namespace Simulation;

public enum FoodConsumptionStatus
{
    NotHungry,
    Traveling,
    Ate,
    Waiting,
    RetryDeferred,
    ActionRejected,
    InvalidEntity,
    InvalidSpecies,
    InvalidSelection
}

public sealed record FoodConsumptionResult(
    FoodConsumptionStatus Status,
    FoodSource? Source = null,
    string? ActionId = null,
    string? WaitingReason = null,
    bool IsHungryAfterEating = false);

public sealed class FoodConsumptionService : ITransientSimulationState
{
    private const double NavigationTimeoutSeconds = 30;

    private readonly World _world;
    private readonly SpeciesCatalog? _catalog;
    private readonly Func<EntityRecord, INavigationAdapter> _navigationForEntity;
    private readonly FoodSelectionService _selection;
    private readonly Dictionary<string, ActiveTravel> _activeTravels = new(StringComparer.Ordinal);

    public FoodConsumptionService(
        World world,
        Func<EntityRecord, INavigationAdapter> navigationForEntity,
        int retryDelayTicks = FoodSelectionService.DefaultRetryDelayTicks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(navigationForEntity);

        _world = world;
        _navigationForEntity = navigationForEntity;
        _selection = new FoodSelectionService(world, retryDelayTicks);
    }

    public FoodConsumptionService(
        World world,
        SpeciesCatalog catalog,
        Func<EntityRecord, INavigationAdapter> navigationForEntity,
        int retryDelayTicks = FoodSelectionService.DefaultRetryDelayTicks)
        : this(world, navigationForEntity, retryDelayTicks)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _selection = new FoodSelectionService(world, catalog, retryDelayTicks);
    }

    public FoodConsumptionResult Advance(EntityRecord entity, long simulationTick)
    {
        ArgumentNullException.ThrowIfNull(_catalog);
        ArgumentNullException.ThrowIfNull(entity);
        if (!_catalog.TryGet(entity.SpeciesId, out var species) || species is null)
        {
            return new FoodConsumptionResult(FoodConsumptionStatus.InvalidSpecies);
        }

        return Advance(entity, species, simulationTick);
    }

    public long FoodUnitsConsumed { get; private set; }

    public long FoodUnitsRemaining => _world.FoodSources.Sum(source =>
        (long)source.Contents.Counts.GetValueOrDefault(ItemIds.Food));

    public FoodConsumptionResult Advance(
        EntityRecord entity,
        SpeciesDefinition species,
        long simulationTick)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(species);
        if (simulationTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationTick), "Simulation tick cannot be negative.");
        }

        if (!OwnsEntity(entity))
        {
            return new FoodConsumptionResult(FoodConsumptionStatus.InvalidEntity);
        }

        if (!string.Equals(entity.SpeciesId, species.Id, StringComparison.Ordinal))
        {
            return new FoodConsumptionResult(FoodConsumptionStatus.InvalidSpecies);
        }

        if (_activeTravels.TryGetValue(entity.Id, out var activeTravel))
        {
            if (!IsCurrentAction(entity, activeTravel))
            {
                CancelTravel(entity.Id);
            }
            else if (!entity.Needs.IsHungry)
            {
                return MapSelection(_selection.TrySelect(
                    entity,
                    species,
                    _world.FoodSources,
                    simulationTick,
                    () => CancelTravel(entity.Id)));
            }
            else if (!_world.TryGetFoodSource(activeTravel.Source.Id, out var currentSource)
                     || !ReferenceEquals(activeTravel.Source, currentSource))
            {
                return FailCandidate(entity, species, activeTravel, FoodTargetFailure.Removed, simulationTick);
            }
            else
            {
                var outcome = activeTravel.Navigator.Poll(activeTravel.NavigationRequestId, 0);
                if (outcome == NavigationOutcome.InProgress)
                {
                    return new FoodConsumptionResult(
                        FoodConsumptionStatus.Traveling,
                        activeTravel.Source,
                        activeTravel.ActionId);
                }

                if (outcome == NavigationOutcome.Arrived)
                {
                    if (!IsWithinArrivalTolerance(
                            entity.Position,
                            activeTravel.Source.Position,
                            activeTravel.ArrivalTolerance))
                    {
                        return FailCandidate(
                            entity,
                            species,
                            activeTravel,
                            FoodTargetFailure.Unreachable,
                            simulationTick);
                    }

                    return ConsumeFood(entity, species, activeTravel, simulationTick);
                }

                var failure = outcome == NavigationOutcome.Removed
                    ? FoodTargetFailure.Removed
                    : FoodTargetFailure.Unreachable;
                return FailCandidate(entity, species, activeTravel, failure, simulationTick);
            }
        }

        return BeginSelection(entity, species, simulationTick);
    }

    public void Reset()
    {
        foreach (var entityId in _activeTravels.Keys.ToArray())
        {
            CancelTravel(entityId);
        }

        _selection.Reset();
        FoodUnitsConsumed = 0;
    }

    private FoodConsumptionResult BeginSelection(
        EntityRecord entity,
        SpeciesDefinition species,
        long simulationTick)
    {
        var selection = _selection.TrySelect(
            entity,
            species,
            _world.FoodSources,
            simulationTick,
            () => CancelTravel(entity.Id));
        return MapSelection(selection, entity);
    }

    private FoodConsumptionResult FailCandidate(
        EntityRecord entity,
        SpeciesDefinition species,
        ActiveTravel travel,
        FoodTargetFailure failure,
        long simulationTick)
    {
        CancelTravel(entity.Id);
        var selection = _selection.ReportCandidateFailure(
            entity,
            travel.Source.Id,
            failure,
            _world.FoodSources,
            simulationTick);
        return MapSelection(selection, entity);
    }

    private FoodConsumptionResult ConsumeFood(
        EntityRecord entity,
        SpeciesDefinition species,
        ActiveTravel travel,
        long simulationTick)
    {
        var nextConsumedCount = string.Equals(travel.ItemId, ItemIds.Food, StringComparison.Ordinal)
            ? checked(FoodUnitsConsumed + 1)
            : FoodUnitsConsumed;
        var removalResult = travel.Source.Contents.TryRemove(travel.ItemId, 1);
        if (removalResult == ResourceMutationResult.InsufficientQuantity)
        {
            return FailCandidate(
                entity,
                species,
                travel,
                FoodTargetFailure.Depleted,
                simulationTick);
        }

        if (removalResult != ResourceMutationResult.Success)
        {
            throw new InvalidOperationException(
                $"Reserved food could not be consumed: {removalResult}.");
        }

        entity.Activity = "Eating";
        entity.Needs.AddNutrition(entity.Needs.NutritionPerUnit);
        FoodUnitsConsumed = nextConsumedCount;
        entity.Actions.Complete();
        entity.WaitingReason = null;
        entity.Activity = entity.Needs.IsHungry ? "Hungry" : "Idle";

        return new FoodConsumptionResult(
            FoodConsumptionStatus.Ate,
            travel.Source,
            travel.ActionId,
            IsHungryAfterEating: entity.Needs.IsHungry);
    }

    private FoodConsumptionResult MapSelection(FoodSelectionResult selection, EntityRecord? entity = null)
    {
        if (selection.Status == FoodSelectionStatus.Selected)
        {
            if (entity is null || selection.Source is null || selection.ActionId is null || selection.ItemId is null)
            {
                return new FoodConsumptionResult(FoodConsumptionStatus.InvalidSelection);
            }

            return StartTravel(entity, selection.Source, selection.ActionId, selection.ItemId);
        }

        var status = selection.Status switch
        {
            FoodSelectionStatus.NotHungry => FoodConsumptionStatus.NotHungry,
            FoodSelectionStatus.Waiting => FoodConsumptionStatus.Waiting,
            FoodSelectionStatus.RetryDeferred => FoodConsumptionStatus.RetryDeferred,
            FoodSelectionStatus.ActionRejected => FoodConsumptionStatus.ActionRejected,
            FoodSelectionStatus.InvalidEntity => FoodConsumptionStatus.InvalidEntity,
            FoodSelectionStatus.InvalidSpecies => FoodConsumptionStatus.InvalidSpecies,
            _ => FoodConsumptionStatus.InvalidSelection
        };
        return new FoodConsumptionResult(status, WaitingReason: selection.WaitingReason);
    }

    private FoodConsumptionResult StartTravel(EntityRecord entity, FoodSource source, string actionId, string itemId)
    {
        var request = new NavigationRequest(
            source.Position,
            timeoutSeconds: NavigationTimeoutSeconds);
        INavigationAdapter? navigator = null;
        string? requestId = null;
        var travelStarted = false;

        try
        {
            navigator = _navigationForEntity(entity)
                ?? throw new InvalidOperationException("The entity has no navigation adapter.");
            requestId = navigator.Request(request);
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new InvalidOperationException("The navigation adapter returned an empty request ID.");
            }

            _activeTravels.Add(
                entity.Id,
                new ActiveTravel(source, actionId, itemId, navigator, requestId, request.ArrivalTolerance));
            travelStarted = true;
        }
        finally
        {
            if (!travelStarted)
            {
                if (requestId is not null && navigator is not null)
                {
                    navigator.Cancel(requestId);
                }

                entity.Actions.Cancel();
            }
        }

        entity.Activity = "SeekingFood";
        return new FoodConsumptionResult(
            FoodConsumptionStatus.Traveling,
            source,
            actionId);
    }

    private void CancelTravel(string entityId)
    {
        if (_activeTravels.Remove(entityId, out var travel))
        {
            travel.Navigator.Cancel(travel.NavigationRequestId);
        }
    }

    private bool OwnsEntity(EntityRecord entity)
    {
        return _world.TryGetEntity(entity.Id, out var worldEntity)
            && ReferenceEquals(entity, worldEntity);
    }

    private static bool IsCurrentAction(EntityRecord entity, ActiveTravel travel)
    {
        return string.Equals(entity.Actions.Current?.Id, travel.ActionId, StringComparison.Ordinal);
    }

    private static bool IsWithinArrivalTolerance(Vector3 position, Vector3 target, double tolerance)
    {
        var x = (double)position.X - target.X;
        var y = (double)position.Y - target.Y;
        var z = (double)position.Z - target.Z;
        return x * x + y * y + z * z <= tolerance * tolerance;
    }

    private sealed record ActiveTravel(
        FoodSource Source,
        string ActionId,
        string ItemId,
        INavigationAdapter Navigator,
        string NavigationRequestId,
        double ArrivalTolerance);
}
