using System.Numerics;

namespace Simulation;

public enum ConstructionActionResult
{
    Success,
    ActorUnavailable,
    ActorIncapacitated,
    ActorIncapable,
    SiteUnavailable,
    OutOfRange,
    MissingMaterial,
    SiteIncomplete
}

public sealed record ConstructionAction(ConstructionActionResult Result, string Message)
{
    public bool Succeeded => Result == ConstructionActionResult.Success;
}

public static class SettlementConstruction
{
    public const float InteractionRange = 2f;
    public const string StarterHutBlueprintId = "starter-hut";

    public static ConstructionBlueprint CreateStarterHutBlueprint() => new(
        StarterHutBlueprintId, 4, 4, new Dictionary<string, int> { [ItemIds.Wood] = 8 });
}

public sealed class ActorConstructionService(World world, SpeciesCatalog speciesCatalog)
{
    public ConstructionAction LoadWood(string actorId, string siteId)
    {
        if (!TryResolve(actorId, siteId, out var actor, out var site, out var failure)) return failure;
        var needed = site!.Blueprint.Remaining(ItemIds.Wood);
        if (needed == 0) return Fail(ConstructionActionResult.SiteIncomplete, "The hut already has all required wood.");
        var carried = actor!.Inventory.GetValueOrDefault(ItemIds.Wood);
        var amount = Math.Min(needed, carried);
        if (amount == 0) return Fail(ConstructionActionResult.MissingMaterial, "Carry wood or collect it from a settlement stockpile.");
        actor.TryRemoveCarried(ItemIds.Wood, amount);
        if (site.Blueprint.ApplyProgress(ItemIds.Wood, amount) != ResourceMutationResult.Success)
            throw new InvalidOperationException("Validated construction material transfer failed.");
        return Ok($"Supplied {amount} wood ({site.Blueprint.GetProgress(ItemIds.Wood)}/{site.Blueprint.RequiredMaterials[ItemIds.Wood]}).");
    }

    public ConstructionAction ApplyWork(string actorId, string siteId)
    {
        if (!TryResolve(actorId, siteId, out _, out var site, out var failure)) return failure;
        if (site!.Blueprint.Remaining(ItemIds.Wood) > 0)
            return Fail(ConstructionActionResult.MissingMaterial, "Supply all required wood before building.");
        if (!world.TryCompleteConstructionSite(site.Id, StructureType.Hut, out _))
            return Fail(ConstructionActionResult.SiteIncomplete, "The hut site cannot be completed.");
        return Ok("Hut complete. Its doorway is clear; approach and press F to use the door.");
    }

    private bool TryResolve(string actorId, string siteId, out EntityRecord? actor,
        out SettlementConstructionSite? site, out ConstructionAction failure)
    {
        actor = null;
        site = null;
        if (!world.TryGetEntity(actorId, out actor) || actor is null)
        { failure = Fail(ConstructionActionResult.ActorUnavailable, "The builder is unavailable."); return false; }
        if (actor.Health.IsIncapacitated)
        { failure = Fail(ConstructionActionResult.ActorIncapacitated, "You cannot build while incapacitated."); return false; }
        if (!speciesCatalog.TryGet(actor.SpeciesId, out var species) || species is null || !species.CanUseTools)
        { failure = Fail(ConstructionActionResult.ActorIncapable, "This entity cannot use construction tools."); return false; }
        site = world.ConstructionSites.FirstOrDefault(item => item.Id == siteId);
        if (site is null)
        { failure = Fail(ConstructionActionResult.SiteUnavailable, "The construction site is unavailable."); return false; }
        if (Vector3.Distance(actor.Position, site.Position) > SettlementConstruction.InteractionRange)
        { failure = Fail(ConstructionActionResult.OutOfRange, "Move within 2 m of the construction site."); return false; }
        failure = Ok("");
        return true;
    }

    private static ConstructionAction Ok(string message) => new(ConstructionActionResult.Success, message);
    private static ConstructionAction Fail(ConstructionActionResult result, string message) => new(result, message);
}

public sealed class PlayerConstructionService(World world, SpeciesCatalog speciesCatalog)
{
    private readonly ActorConstructionService _actors = new(world, speciesCatalog);
    public const float PlacementRange = 12f;
    public ConstructionAction LoadWood(string siteId) => WithPlayer(id => _actors.LoadWood(id, siteId));
    public ConstructionAction ApplyWork(string siteId) => WithPlayer(id => _actors.ApplyWork(id, siteId));
    public ConstructionAction PlaceStarterHut(Vector3 position, bool navigationClear = true)
    {
        if (world.PlayerEntityId is not { } playerId || !world.TryGetEntity(playerId, out var player) || player is null)
            return new(ConstructionActionResult.ActorUnavailable, "The player is unavailable.");
        if (player.Health.IsIncapacitated)
            return new(ConstructionActionResult.ActorIncapacitated, "You cannot place a hut while incapacitated.");
        if (!navigationClear || !float.IsFinite(position.X) || !float.IsFinite(position.Y)
            || !float.IsFinite(position.Z) || position.Y != 0
            || Math.Abs(position.X) > 198 || Math.Abs(position.Z) > 198
            || Vector3.Distance(player.Position, position) > PlacementRange
            || world.FoodSources.Any(item => Vector3.Distance(item.Position, position) < 4)
            || world.ResourcePiles.Any(item => Vector3.Distance(item.Position, position) < 4)
            || world.Stockpiles.Any(item => Vector3.Distance(item.Position, position) < 4)
            || world.Workstations.Any(item => Vector3.Distance(item.Position, position) < 4)
            || world.Farms.Any(item => Vector3.Distance(item.Position, position) < 4)
            || world.Structures.Any(item => Vector3.Distance(item.Position, position) < 4)
            || world.ConstructionSites.Any(item => Vector3.Distance(item.Position, position) < 4))
            return new(ConstructionActionResult.SiteUnavailable, "That location is blocked or outside valid terrain.");
        if (world.ConstructionSites.Count > 0 || world.Structures.Any(item => item.Type == StructureType.Hut))
            return new(ConstructionActionResult.SiteUnavailable, "The starter hut has already been placed.");
        world.AddConstructionSite("starter-hut-1", position, SettlementConstruction.CreateStarterHutBlueprint());
        return new(ConstructionActionResult.Success, "Hut site placed. Load 8 wood, then apply work to build.");
    }

    private ConstructionAction WithPlayer(Func<string, ConstructionAction> action) => world.PlayerEntityId is { } id
        ? action(id)
        : new(ConstructionActionResult.ActorUnavailable, "The player is unavailable.");
}