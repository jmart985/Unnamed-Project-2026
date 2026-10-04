using System.Numerics;

namespace Simulation;

public enum PlayerResourceKind { Food, ResourcePile, Stockpile }
public sealed record PlayerResourceTarget(PlayerResourceKind Kind, string Id, Vector3 Position, ResourceContainer Contents);
public enum PlayerResourceResultCode { Collected, Used, Unavailable, Missing, OutOfRange, Empty, Reserved, Full, NotUsable }
public sealed record PlayerResourceResult(PlayerResourceResultCode Code, string Message);

/// <summary>Immediate player actions reuse World containers and atomic carried-inventory transfers.</summary>
public sealed class PlayerResourceService(World world, SpeciesCatalog species)
{
    public const double Reach = 2;
    public IEnumerable<PlayerResourceTarget> Targets =>
        world.FoodSources.Select(source => new PlayerResourceTarget(PlayerResourceKind.Food, source.Id, source.Position, source.Contents))
            .Concat(world.ResourcePiles.Select(source => new PlayerResourceTarget(PlayerResourceKind.ResourcePile, source.Id, source.Position, source.Contents)))
            .Concat(world.Stockpiles.Select(source => new PlayerResourceTarget(PlayerResourceKind.Stockpile, source.Id, source.Position, source.Contents)));

    public bool HasAvailableItems(PlayerResourceTarget target) => target.Contents.Counts
        .Any(item => world.Reservations.GetAvailableResourceQuantity(target.Contents, item.Key) > 0);

    public PlayerResourceResult Collect(PlayerResourceKind kind, string id)
    {
        if (!TryPlayer(out var player)) return new(PlayerResourceResultCode.Unavailable, "The player cannot collect while incapacitated.");
        var target = Targets.FirstOrDefault(target => target.Kind == kind && target.Id == id);
        if (target is null) return new(PlayerResourceResultCode.Missing, "The resource no longer exists.");
        if (MeleeReachValidator.Validate(player!.Position, target.Position, Reach) != MeleeReachResult.InRange)
            return new(PlayerResourceResultCode.OutOfRange, "Move within 2 m to collect.");
        if (player.Inventory.Sum(entry => (long)entry.Value) >= player.InventoryCapacity)
            return new(PlayerResourceResultCode.Full, $"Inventory full ({player.InventoryCapacity} units).");
        var stocked = target.Contents.Counts.Where(entry => entry.Value > 0).OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
        if (stocked.Length == 0) return new(PlayerResourceResultCode.Empty, "This resource is empty.");
        var item = stocked.FirstOrDefault(entry => world.Reservations.GetAvailableResourceQuantity(target.Contents, entry.Key) > 0).Key;
        if (item is null) return new(PlayerResourceResultCode.Reserved, "These items are reserved by another worker.");
        var result = ResourceTransferService.TryPickUp(target.Contents, player, item, 1);
        return result == ResourceMutationResult.Success
            ? new(PlayerResourceResultCode.Collected, $"Collected 1 {item}. Carried {player.Inventory.GetValueOrDefault(item)}.")
            : new(PlayerResourceResultCode.Full, $"Collection rejected: {result}.");
    }

    public PlayerResourceResult UseCarriedFood()
    {
        if (!TryPlayer(out var player) || !species.TryGet(player!.SpeciesId, out var definition) || definition is null)
            return new(PlayerResourceResultCode.Unavailable, "The player cannot use items while incapacitated.");
        var item = definition.Diet.FirstOrDefault(item => ItemIds.IsEdibleFood(item) && player.Inventory.GetValueOrDefault(item) > 0);
        if (item is null) return new(PlayerResourceResultCode.NotUsable, "No usable carried food. Tap collect near food first; materials cannot be used here.");
        if (player.Satiety >= 100) return new(PlayerResourceResultCode.NotUsable, "Already full; carried food was kept.");
        if (player.TryRemoveCarried(item, 1) != ResourceMutationResult.Success)
            return new(PlayerResourceResultCode.NotUsable, "The carried item is unavailable.");
        player.Needs.AddNutrition(player.Needs.NutritionPerUnit);
        return new(PlayerResourceResultCode.Used, $"Used 1 {item}. Satiety {player.Satiety:0.#}.");
    }
    private bool TryPlayer(out EntityRecord? player)
    {
        player = null;
        return world.PlayerEntityId is { } id && world.TryGetEntity(id, out player) && player is not null && !player.Health.IsIncapacitated;
    }
}
