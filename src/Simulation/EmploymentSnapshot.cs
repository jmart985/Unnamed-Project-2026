namespace Simulation;

public static partial class WorldSnapshotSerializer
{
    private static WorldSnapshot MigrateEmployment(WorldSnapshot snapshot)
    {
        // Homes are the existing explicit resident roster. Do not recruit all humans
        // or wildlife just because their species can perform the work.
        var workers = snapshot.SessionSeed is null ? [] : (snapshot.HomePositions ?? [])
            .Where(home => home is not null && home.EntityId != snapshot.PlayerEntityId)
            .Select(home => new SettlementWorkerSetting(home.EntityId)).ToArray();
        var stockpile = snapshot.Stockpiles?.FirstOrDefault();
        var positions = stockpile is null ? [] : workers.Select((_, index) => new WorkplacePosition(
            $"{stockpile.Id}-hauler-{index + 1:00}", stockpile.Id, WorkRoleIds.WoodHauler)).ToArray();
        return snapshot with
        {
            UsesWorkplaceEmployment = workers.Length > 0 && stockpile is not null,
            SettlementWorkers = stockpile is null ? [] : workers,
            WorkplacePositions = positions
        };
    }

    private static void ValidateEmployment(WorldSnapshot snapshot, SpeciesCatalog catalog)
    {
        if (snapshot.SettlementWorkers is null || snapshot.WorkplacePositions is null)
            throw new InvalidDataException("Snapshot employment lists are required.");
        if (!snapshot.UsesWorkplaceEmployment && (snapshot.SettlementWorkers.Count != 0 || snapshot.WorkplacePositions.Count != 0))
            throw new InvalidDataException("Disabled employment cannot contain workers or positions.");
        var residents = new HashSet<string>(StringComparer.Ordinal);
        foreach (var worker in snapshot.SettlementWorkers)
            if (worker is null || string.IsNullOrWhiteSpace(worker.EntityId) || worker.EntityId == snapshot.PlayerEntityId
                || !snapshot.Entities.Any(entity => entity.Id == worker.EntityId) || !residents.Add(worker.EntityId))
                throw new InvalidDataException("Settlement workers must reference distinct non-player residents.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var employees = new HashSet<string>(StringComparer.Ordinal);
        foreach (var position in snapshot.WorkplacePositions)
        {
            if (position is null || string.IsNullOrWhiteSpace(position.Id) || !ids.Add(position.Id)
                || position.RoleId != WorkRoleIds.WoodHauler
                || !snapshot.Stockpiles.Any(stockpile => stockpile.Id == position.WorkplaceId))
                throw new InvalidDataException("Workplace positions require distinct IDs, a supported role and an existing workplace.");
            if (position.WorkerId is not { } workerId) continue;
            if (!residents.Contains(workerId) || !employees.Add(workerId))
                throw new InvalidDataException("A resident may occupy only one workplace position.");
            var entity = snapshot.Entities.First(entity => entity.Id == workerId);
            var stockpile = snapshot.Stockpiles.First(stockpile => stockpile.Id == position.WorkplaceId);
            if (!catalog.TryGet(entity.SpeciesId, out var species) || species is null
                || !WorkRoleIds.Supports(position.RoleId, species)
                || (stockpile.FactionOwnerId is not null && stockpile.FactionOwnerId != entity.AllegianceFactionId))
                throw new InvalidDataException("An employee must have the role capability and access to the workplace.");
        }
    }
}
