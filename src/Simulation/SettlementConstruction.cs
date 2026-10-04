using System.Numerics;

namespace Simulation;

public static class SettlementConstruction
{
    public const string HutBlueprintId = "starter-hut";
    public const int HutWidth = 4;
    public const int HutDepth = 4;
    public const int HutWood = 6;
    public const int HutWork = 5;
    public const float PlacementRange = 8;
    public const float InteractionRange = 2;

    public static ConstructionBlueprint CreateHutBlueprint() => new(HutBlueprintId, HutWidth, HutDepth,
        new Dictionary<string, int> { [ItemIds.Wood] = HutWood }, HutWork);
}

public enum ConstructionActionResult
{
    Success, ActorUnavailable, ActorIncapable, ActorIncapacitated, SiteUnavailable, OutOfRange,
    InvalidTerrain, Overlap, MissingMaterial, MaterialsIncomplete, AlreadyComplete
}

public sealed record ConstructionAction(ConstructionActionResult Result, string Message, string? CreatedId = null)
{
    public bool Succeeded => Result == ConstructionActionResult.Success;
}

public sealed class ActorConstructionService(World world, SpeciesCatalog speciesCatalog, Func<Vector3, bool>? terrainBlocked = null)
{
    public ConstructionAction PlaceHut(string actorId, string siteId, Vector3 position)
    {
        var validation = ValidateHutPlacement(actorId, position);
        if (!validation.Succeeded) return validation;
        try
        {
            var site = world.AddConstructionSite(siteId, position, SettlementConstruction.CreateHutBlueprint());
            return new(ConstructionActionResult.Success, "Starter hut site placed.", site.Id);
        }
        catch (ArgumentException)
        {
            return Fail(ConstructionActionResult.Overlap, "That construction target identity is stale or already used.");
        }
    }

    public ConstructionAction ValidateHutPlacement(string actorId, Vector3 position)
    {
        if (!TryActor(actorId, out var actor, out var failure)) return failure;
        if (Vector3.Distance(actor!.Position, position) > SettlementConstruction.PlacementRange)
            return Fail(ConstructionActionResult.OutOfRange, "Place the hut within 8 m.");
        if (position.Y != 0 || Math.Abs(position.X) > 58 || Math.Abs(position.Z) > 58 || terrainBlocked?.Invoke(position) == true)
            return Fail(ConstructionActionResult.InvalidTerrain, "The hut footprint is blocked or outside valid town terrain.");
        if (Overlaps(position)) return Fail(ConstructionActionResult.Overlap, "The hut footprint overlaps an existing settlement target.");
        return Ok("Starter hut placement is valid.");
    }

    public ConstructionAction SupplyWood(string actorId, string siteId)
    {
        if (!TrySite(actorId, siteId, out var actor, out var site, out var failure)) return failure;
        if (site!.Blueprint.IsComplete) return Fail(ConstructionActionResult.AlreadyComplete, "The hut is already complete.");
        if (site.Blueprint.Remaining(ItemIds.Wood) == 0) return Fail(ConstructionActionResult.MaterialsIncomplete, "All wood is supplied; apply construction work.");
        if (actor!.Inventory.GetValueOrDefault(ItemIds.Wood) < 1) return Fail(ConstructionActionResult.MissingMaterial, "Carry wood to supply this site.");
        actor.TryRemoveCarried(ItemIds.Wood, 1);
        site.Blueprint.ApplyProgress(ItemIds.Wood, 1);
        return Ok($"Supplied wood: {site.Blueprint.GetProgress(ItemIds.Wood)}/{SettlementConstruction.HutWood}.");
    }

    public ConstructionAction ApplyWork(string actorId, string siteId)
    {
        if (!TrySite(actorId, siteId, out _, out var site, out var failure)) return failure;
        if (!site!.Blueprint.MaterialsComplete) return Fail(ConstructionActionResult.MaterialsIncomplete, "Supply all required wood before building.");
        if (site.Blueprint.IsComplete) return Fail(ConstructionActionResult.AlreadyComplete, "The hut is already complete.");
        site.Blueprint.ApplyWork(1);
        if (!site.Blueprint.IsComplete) return Ok($"Construction work: {site.Blueprint.WorkProgress}/{site.Blueprint.WorkRequired}.");
        var structureId = site.Id + "-structure";
        world.AddStructure(structureId, StructureType.Hut, site.Position);
        world.TryRemoveConstructionSite(site.Id);
        return new(ConstructionActionResult.Success, "Starter hut complete. Its door is usable.", structureId);
    }

    private bool TrySite(string actorId, string siteId, out EntityRecord? actor, out SettlementConstructionSite? site,
        out ConstructionAction failure)
    {
        site = null;
        if (!TryActor(actorId, out actor, out failure)) return false;
        site = world.ConstructionSites.FirstOrDefault(item => item.Id == siteId);
        if (site is null) { failure = Fail(ConstructionActionResult.SiteUnavailable, "The construction site is unavailable."); return false; }
        if (Vector3.Distance(actor!.Position, site.Position) > SettlementConstruction.InteractionRange)
        { failure = Fail(ConstructionActionResult.OutOfRange, "Move within 2 m of the construction site."); return false; }
        return true;
    }

    private bool TryActor(string actorId, out EntityRecord? actor, out ConstructionAction failure)
    {
        if (!world.TryGetEntity(actorId, out actor) || actor is null)
        { failure = Fail(ConstructionActionResult.ActorUnavailable, "The builder is unavailable."); return false; }
        if (!speciesCatalog.TryGet(actor.SpeciesId, out var species) || species is null || !species.CanUseTools)
        { failure = Fail(ConstructionActionResult.ActorIncapable, "This entity cannot use construction tools."); return false; }
        if (actor.Health.IsIncapacitated)
        { failure = Fail(ConstructionActionResult.ActorIncapacitated, "The builder cannot act while incapacitated."); return false; }
        failure = Ok(""); return true;
    }

    private bool Overlaps(Vector3 position)
    {
        const float radius = 3;
        return world.Structures.Any(item => Vector3.Distance(item.Position, position) < radius)
            || world.ConstructionSites.Any(item => Vector3.Distance(item.Position, position) < radius)
            || world.Workstations.Any(item => Vector3.Distance(item.Position, position) < radius)
            || world.Farms.Any(item => Vector3.Distance(item.Position, position) < radius)
            || world.Stockpiles.Any(item => Vector3.Distance(item.Position, position) < radius);
    }

    private static ConstructionAction Ok(string message) => new(ConstructionActionResult.Success, message);
    private static ConstructionAction Fail(ConstructionActionResult result, string message) => new(result, message);
}
