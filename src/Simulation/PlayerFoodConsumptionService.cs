using System.Numerics;

namespace Simulation;

public enum PlayerFoodConsumptionStatus
{
    Ate,
    PlayerUnavailable,
    InvalidSpecies,
    OutOfRange,
    FoodUnavailable,
    IncompatibleFood,
    NoFoodAvailable,
    ActionRejected
}

public sealed record PlayerFoodConsumptionResult(
    PlayerFoodConsumptionStatus Status,
    FoodSource? Source = null,
    string? Reason = null);

public sealed class PlayerFoodConsumptionService : ITransientSimulationState
{
    public const double DefaultMaximumReach = 2.0;

    private readonly World _world;
    private readonly SpeciesCatalog _speciesCatalog;
    private readonly double _maximumReach;
    private long _nextActionId;

    public PlayerFoodConsumptionService(
        World world,
        SpeciesCatalog speciesCatalog,
        double maximumReach = DefaultMaximumReach)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _speciesCatalog = speciesCatalog ?? throw new ArgumentNullException(nameof(speciesCatalog));
        if (!double.IsFinite(maximumReach) || maximumReach < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumReach));
        }

        _maximumReach = maximumReach;
    }

    public long FoodUnitsConsumed { get; private set; }

    public long FoodUnitsRemaining => _world.FoodSources.Sum(source =>
        source.Contents.Counts.Where(pair => ItemIds.IsEdibleFood(pair.Key)).Sum(pair => (long)pair.Value));

    public PlayerFoodConsumptionResult EatNearby()
    {
        if (_world.PlayerEntityId is not { } playerId
            || !_world.TryGetEntity(playerId, out var player)
            || player is null)
        {
            return new(PlayerFoodConsumptionStatus.PlayerUnavailable, Reason: "The session player is unavailable.");
        }

        if (!_speciesCatalog.TryGet(player.SpeciesId, out var species) || species is null)
        {
            return new(PlayerFoodConsumptionStatus.InvalidSpecies, Reason: "The player's species is unavailable.");
        }

        var candidates = _world.FoodSources
            .SelectMany(source => species.Diet.Select((itemId, index) => new
            {
                Source = source,
                ItemId = itemId,
                DietOrder = index,
                DistanceSquared = DistanceSquared(player.Position, source.Position),
                Available = _world.Reservations.GetAvailableResourceQuantity(source.Contents, itemId)
            }))
            .Where(candidate => candidate.Available > 0)
            .OrderBy(candidate => candidate.DistanceSquared)
            .ThenBy(candidate => candidate.Source.Id, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.DietOrder)
            .ToArray();

        if (candidates.Length == 0)
        {
            if (HasCompatibleFood(species))
            {
                return new(
                    PlayerFoodConsumptionStatus.FoodUnavailable,
                    Reason: "Compatible food is reserved for another action.");
            }

            if (_world.FoodSources.Any(source => source.Contents.Counts.Keys.Any(ItemIds.IsEdibleFood)))
            {
                return new(
                    PlayerFoodConsumptionStatus.IncompatibleFood,
                    Reason: "No compatible food is available.");
            }

            return new(PlayerFoodConsumptionStatus.NoFoodAvailable, Reason: "No food is available.");
        }

        var selected = candidates[0];
        if (selected.DistanceSquared > _maximumReach * _maximumReach)
        {
            return new(
                PlayerFoodConsumptionStatus.OutOfRange,
                selected.Source,
                "Move closer to a compatible food source.");
        }

        var nextConsumedCount = checked(FoodUnitsConsumed + 1);
        var actionId = $"player-eat-{player.Id}-{checked(++_nextActionId):D6}";
        if (player.Actions.TryStart(new ActionCandidate(actionId, ActionKind.Eat)) == ActionStartResult.Rejected)
        {
            return new(PlayerFoodConsumptionStatus.ActionRejected, Reason: "The player cannot eat during the current action.");
        }

        var reservation = _world.Reservations.TryReserveResource(
            player.Id,
            actionId,
            selected.Source.Id,
            selected.Source.Contents,
            selected.ItemId,
            1);
        if (reservation != ReservationResult.Reserved)
        {
            player.Actions.Cancel();
            return new(
                PlayerFoodConsumptionStatus.FoodUnavailable,
                selected.Source,
                "Compatible food is no longer available.");
        }

        var removalResult = selected.Source.Contents.TryRemove(selected.ItemId, 1);
        if (removalResult != ResourceMutationResult.Success)
        {
            player.Actions.Cancel();
            return new(
                PlayerFoodConsumptionStatus.FoodUnavailable,
                selected.Source,
                "Compatible food is no longer available.");
        }

        player.Needs.AddNutrition(player.Needs.NutritionPerUnit);
        FoodUnitsConsumed = nextConsumedCount;
        player.WaitingReason = null;
        player.Activity = "Eating";
        player.Actions.Complete();
        player.Activity = player.Needs.IsHungry ? "Hungry" : "Idle";
        return new(PlayerFoodConsumptionStatus.Ate, selected.Source, $"Ate one unit of food from {selected.Source.Id}.");
    }

    public void Reset()
    {
        FoodUnitsConsumed = 0;
        _nextActionId = 0;
    }

    private bool HasCompatibleFood(SpeciesDefinition species)
    {
        return _world.FoodSources.Any(source => species.Diet.Any(itemId =>
            source.Contents.TryGetCount(itemId, out var count)
            && count > 0));
    }

    private static double DistanceSquared(Vector3 first, Vector3 second)
    {
        var x = (double)first.X - second.X;
        var y = (double)first.Y - second.Y;
        var z = (double)first.Z - second.Z;
        return x * x + y * y + z * z;
    }
}
