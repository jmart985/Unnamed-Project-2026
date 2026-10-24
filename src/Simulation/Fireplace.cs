using System.Numerics;

namespace Simulation;

public sealed class Fireplace
{
    public Fireplace(string id, Vector3 position)
    { Id = id; Position = position; }
    public string Id { get; }
    public Vector3 Position { get; }
    public int Fuel { get; internal set; }
    public bool IsLit => Fuel > 0;
}

public enum FireplaceActionCode { Success, Missing, OutOfRange, MissingFuel, MissingIngredients, ItemStackFull, Invalid }
public sealed record FireplaceAction(FireplaceActionCode Code, string Message);

public sealed class FireplaceService(World world)
{
    public const double WarmthRadius = 6;

    public bool IsWarm(string actorId)
    {
        if (!world.TryGetEntity(actorId, out var actor) || actor is null) return false;
        return world.Fireplaces.Any(fire => fire.IsLit && Vector3.Distance(actor.Position, fire.Position) <= WarmthRadius);
    }

    public FireplaceAction AddFuel(string actorId, string fireplaceId)
    {
        if (!world.TryGetEntity(actorId, out var actor) || actor is null) return new(FireplaceActionCode.Invalid, "The actor is unavailable.");
        var fire = world.Fireplaces.FirstOrDefault(item => item.Id == fireplaceId);
        if (fire is null) return new(FireplaceActionCode.Missing, "The fireplace is unavailable.");
        if (Vector3.Distance(actor.Position, fire.Position) > 2) return new(FireplaceActionCode.OutOfRange, "Move within 2 m of the fire.");
        if (actor.Inventory.GetValueOrDefault(ItemIds.Wood) < 1) return new(FireplaceActionCode.MissingFuel, "Carry wood to fuel the fire.");
        actor.TryRemoveCarried(ItemIds.Wood, 1); fire.Fuel++;
        return new(FireplaceActionCode.Success, "Added wood to the fire.");
    }

    public FireplaceAction CookMeal(string actorId, string fireplaceId)
    {
        if (!world.TryGetEntity(actorId, out var actor) || actor is null) return new(FireplaceActionCode.Invalid, "The actor is unavailable.");
        var fire = world.Fireplaces.FirstOrDefault(item => item.Id == fireplaceId);
        if (fire is null) return new(FireplaceActionCode.Missing, "The fireplace is unavailable.");
        if (Vector3.Distance(actor.Position, fire.Position) > 2 || !fire.IsLit) return new(FireplaceActionCode.MissingFuel, "A lit nearby fire is required.");
        if (actor.Inventory.GetValueOrDefault(ItemIds.Plant) < 1) return new(FireplaceActionCode.MissingIngredients, "Carry a plant ingredient to cook.");
        if (!actor.CanAddCarried(ItemIds.Food, 1)) return new(FireplaceActionCode.ItemStackFull, "The carried food stack is full.");
        actor.TryRemoveCarried(ItemIds.Plant, 1); actor.TryAddCarried(ItemIds.Food, 1); fire.Fuel--;
        return new(FireplaceActionCode.Success, "Cooked one meal over the fire.");
    }
}
