using System.Numerics;

namespace Simulation;

public enum FarmActionResult
{
    Success,
    ActorUnavailable,
    ActorIncapacitated,
    ActorIncapable,
    FarmUnavailable,
    PlotUnavailable,
    OutOfRange,
    PlotOccupied,
    MissingSeed,
    PlotEmpty,
    CropMature,
    CropImmature,
    CarryCapacityExceeded
}

public sealed record FarmAction(FarmActionResult Result, string Message)
{
    public bool Succeeded => Result == FarmActionResult.Success;
}

public sealed class ActorFarmService(World world, SpeciesCatalog speciesCatalog)
{
    public const float InteractionRange = 2f;

    public FarmAction Plant(string actorId, string farmId, string plotId)
    {
        if (!TryResolve(actorId, farmId, plotId, out var actor, out _, out var plot, out var failure)) return failure;
        if (plot!.IsPlanted) return Fail(FarmActionResult.PlotOccupied, "The plot is already planted.");
        if (!actor!.Inventory.TryGetValue(ItemIds.Plant, out var carried) || carried < 1)
            return Fail(FarmActionResult.MissingSeed, "Carry 1 plant to seed this plot.");

        actor.TryRemoveCarried(ItemIds.Plant, 1);
        if (!plot.TryPlant(SettlementCrops.CreateFieldCrop(), 1))
            throw new InvalidOperationException("Validated farm planting failed.");
        return Ok("Planted field crop: apply 5 growth steps before harvest.");
    }

    public FarmAction AdvanceGrowth(string actorId, string farmId, string plotId)
    {
        if (!TryResolve(actorId, farmId, plotId, out _, out _, out var plot, out var failure)) return failure;
        if (!plot!.IsPlanted) return Fail(FarmActionResult.PlotEmpty, "Plant the empty plot first.");
        if (plot.IsMature) return Fail(FarmActionResult.CropMature, "The crop is mature and ready to harvest.");
        plot.AdvanceGrowth(1);
        return Ok(plot.IsMature ? "Crop mature: harvest is ready." : $"Growth advanced: {plot.Growth}/{plot.Crop!.GrowthRequired}.");
    }

    public FarmAction Harvest(string actorId, string farmId, string plotId)
    {
        if (!TryResolve(actorId, farmId, plotId, out var actor, out _, out var plot, out var failure)) return failure;
        if (!plot!.IsPlanted) return Fail(FarmActionResult.PlotEmpty, "The plot is empty.");
        if (!plot.IsMature) return Fail(FarmActionResult.CropImmature, "The crop is not mature yet.");
        var yield = plot.Crop!.HarvestYield;
        var carried = actor!.Inventory.Values.Sum(value => (long)value);
        if (carried + yield > actor.InventoryCapacity
            || actor.Inventory.GetValueOrDefault(ItemIds.Food) > int.MaxValue - yield)
            return Fail(FarmActionResult.CarryCapacityExceeded, "You do not have enough carrying capacity for the harvest.");

        if (plot.Harvest() != yield) throw new InvalidOperationException("Validated farm harvest failed.");
        actor.TryAddCarried(ItemIds.Food, yield);
        return Ok($"Harvested {yield} food.");
    }

    private bool TryResolve(string actorId, string farmId, string plotId, out EntityRecord? actor, out Farm? farm,
        out FarmPlot? plot, out FarmAction failure)
    {
        actor = null; farm = null; plot = null;
        if (!world.TryGetEntity(actorId, out actor) || actor is null)
        { failure = Fail(FarmActionResult.ActorUnavailable, "The farmer is unavailable."); return false; }
        if (!speciesCatalog.TryGet(actor.SpeciesId, out var species) || species is null || !species.CanUseTools)
        { failure = Fail(FarmActionResult.ActorIncapable, "This entity cannot use farming tools."); return false; }
        if (actor.Health.IsIncapacitated)
        { failure = Fail(FarmActionResult.ActorIncapacitated, "The farmer cannot act while incapacitated."); return false; }
        farm = world.Farms.FirstOrDefault(item => item.Id == farmId);
        if (farm is null)
        { failure = Fail(FarmActionResult.FarmUnavailable, "The farm is unavailable."); return false; }
        plot = farm.Plots.FirstOrDefault(item => item.Id == plotId);
        if (plot is null)
        { failure = Fail(FarmActionResult.PlotUnavailable, "The farm plot is unavailable."); return false; }
        if (Vector3.Distance(actor.Position, farm.Position) > InteractionRange)
        { failure = Fail(FarmActionResult.OutOfRange, "Move within 2 m of the farm plot."); return false; }
        failure = Ok(""); return true;
    }

    private static FarmAction Ok(string message) => new(FarmActionResult.Success, message);
    private static FarmAction Fail(FarmActionResult result, string message) => new(result, message);
}

public sealed class PlayerFarmService(World world, SpeciesCatalog speciesCatalog)
{
    private readonly ActorFarmService _actors = new(world, speciesCatalog);

    public const float InteractionRange = ActorFarmService.InteractionRange;

    public FarmAction Plant(string farmId, string plotId) => WithPlayer(id => _actors.Plant(id, farmId, plotId));
    public FarmAction AdvanceGrowth(string farmId, string plotId) => WithPlayer(id => _actors.AdvanceGrowth(id, farmId, plotId));
    public FarmAction Harvest(string farmId, string plotId) => WithPlayer(id => _actors.Harvest(id, farmId, plotId));

    private FarmAction WithPlayer(Func<string, FarmAction> action) => world.PlayerEntityId is { } id
        ? action(id)
        : new(FarmActionResult.ActorUnavailable, "The player is unavailable.");
}
