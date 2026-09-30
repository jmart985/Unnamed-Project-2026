namespace Simulation;

public enum ActionIntentStatus
{
    Accepted,
    Rejected,
    Deferred
}

/// <summary>Domain-only outcome shared by player and AI action callers.</summary>
public sealed record ActionIntentResult(
    string EntityId,
    ActionIntentStatus Status,
    string ReasonCode,
    string? TargetId = null)
{
    public static ActionIntentResult FromFoodSelection(EntityRecord entity, FoodSelectionResult result)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(result);
        var status = result.Status switch
        {
            FoodSelectionStatus.Selected or FoodSelectionStatus.NotHungry => ActionIntentStatus.Accepted,
            FoodSelectionStatus.Waiting or FoodSelectionStatus.RetryDeferred => ActionIntentStatus.Deferred,
            _ => ActionIntentStatus.Rejected
        };
        var reason = result.Status switch
        {
            FoodSelectionStatus.Selected => "food_selected",
            FoodSelectionStatus.NotHungry => "not_hungry",
            FoodSelectionStatus.Waiting => "waiting_for_food",
            FoodSelectionStatus.RetryDeferred => "food_retry_deferred",
            FoodSelectionStatus.InvalidEntity => "invalid_entity",
            FoodSelectionStatus.InvalidSpecies => "invalid_species",
            FoodSelectionStatus.ActionRejected => "action_rejected",
            _ => "invalid_food_selection"
        };
        return new(entity.Id, status, reason, result.Source?.Id ?? result.ActionId);
    }

    public static ActionIntentResult FromFoodConsumption(EntityRecord entity, FoodConsumptionResult result)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(result);
        var status = result.Status switch
        {
            FoodConsumptionStatus.Ate or FoodConsumptionStatus.NotHungry => ActionIntentStatus.Accepted,
            FoodConsumptionStatus.Traveling or FoodConsumptionStatus.Waiting or FoodConsumptionStatus.RetryDeferred => ActionIntentStatus.Deferred,
            _ => ActionIntentStatus.Rejected
        };
        var reason = result.Status switch
        {
            FoodConsumptionStatus.Ate => "food_consumed",
            FoodConsumptionStatus.NotHungry => "not_hungry",
            FoodConsumptionStatus.Traveling => "traveling_to_food",
            FoodConsumptionStatus.Waiting => "waiting_for_food",
            FoodConsumptionStatus.RetryDeferred => "food_retry_deferred",
            FoodConsumptionStatus.InvalidEntity => "invalid_entity",
            FoodConsumptionStatus.InvalidSpecies => "invalid_species",
            FoodConsumptionStatus.ActionRejected => "action_rejected",
            _ => "invalid_food_consumption"
        };
        return new(entity.Id, status, reason, result.Source?.Id ?? result.ActionId);
    }

    public static ActionIntentResult FromWoodHauling(EntityRecord entity, WoodHaulingResult result)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(result);
        var status = result.Status switch
        {
            WoodHaulingStatus.Delivered or WoodHaulingStatus.NoWork => ActionIntentStatus.Accepted,
            WoodHaulingStatus.TravelingToSource or WoodHaulingStatus.TravelingToStockpile or WoodHaulingStatus.NeedsFood or WoodHaulingStatus.Waiting or WoodHaulingStatus.RetryDeferred => ActionIntentStatus.Deferred,
            _ => ActionIntentStatus.Rejected
        };
        var reason = result.Status switch
        {
            WoodHaulingStatus.Delivered => "wood_delivered",
            WoodHaulingStatus.NoWork => "no_work_available",
            WoodHaulingStatus.TravelingToSource => "traveling_to_wood",
            WoodHaulingStatus.TravelingToStockpile => "traveling_to_stockpile",
            WoodHaulingStatus.NeedsFood => "waiting_for_food",
            WoodHaulingStatus.Waiting => "waiting_for_wood",
            WoodHaulingStatus.RetryDeferred => "haul_retry_deferred",
            WoodHaulingStatus.InvalidEntity => "invalid_entity",
            WoodHaulingStatus.InvalidSpecies => "invalid_species",
            WoodHaulingStatus.NotHauler => "not_hauler",
            WoodHaulingStatus.ActionRejected => "action_rejected",
            _ => "invalid_hauling"
        };
        return new(entity.Id, status, reason, result.Source?.Id ?? result.Stockpile?.Id);
    }
}
