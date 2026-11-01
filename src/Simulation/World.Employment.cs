namespace Simulation;

public static class WorkRoleIds
{
    public const string WoodHauler = "wood_hauler";
    public static bool Supports(string roleId, SpeciesDefinition species) =>
        roleId == WoodHauler && species.CanHaul;
}

public sealed record SettlementWorkerSetting(string EntityId, bool AutoAssign = true);
public sealed record WorkplacePosition(string Id, string WorkplaceId, string RoleId, string? WorkerId = null);
public enum EmploymentResult
{
    Accepted, Unchanged, InvalidWorker, NotResident, Incapable, Unavailable,
    InvalidPosition, Occupied, AlreadyEmployed
}

/// <summary>Durable employment is independent of transient actions and navigation.</summary>
public sealed partial class World
{
    private readonly Dictionary<string, SettlementWorkerSetting> _settlementWorkers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkplacePosition> _workplacePositions = new(StringComparer.Ordinal);
    public bool UsesWorkplaceEmployment { get; private set; }
    public IReadOnlyList<SettlementWorkerSetting> SettlementWorkers => _settlementWorkers.Values
        .OrderBy(worker => worker.EntityId, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<WorkplacePosition> WorkplacePositions => _workplacePositions.Values
        .OrderBy(position => position.Id, StringComparer.Ordinal).ToArray();

    public WorkplacePosition? GetEmployment(string? entityId) => entityId is null ? null
        : _workplacePositions.Values.FirstOrDefault(position => position.WorkerId == entityId);

    public EmploymentResult RegisterSettlementWorker(string? entityId, bool autoAssign = true)
    {
        if (!TryGetEntity(entityId, out var worker) || worker is null || worker.Id == PlayerEntityId
            || worker.Health.IsDead) return EmploymentResult.InvalidWorker;
        if (_settlementWorkers.ContainsKey(worker.Id)) return EmploymentResult.Unchanged;
        UsesWorkplaceEmployment = true;
        _settlementWorkers.Add(worker.Id, new(worker.Id, autoAssign));
        return EmploymentResult.Accepted;
    }

    public EmploymentResult SetWorkerAutoAssignment(string? entityId, bool enabled)
    {
        if (entityId is null || !_settlementWorkers.TryGetValue(entityId, out var worker))
            return EmploymentResult.NotResident;
        if (worker.AutoAssign == enabled) return EmploymentResult.Unchanged;
        _settlementWorkers[entityId] = worker with { AutoAssign = enabled };
        return EmploymentResult.Accepted;
    }

    public EmploymentResult AddWorkplacePosition(string id, string workplaceId, string roleId = WorkRoleIds.WoodHauler)
    {
        if (string.IsNullOrWhiteSpace(id) || roleId != WorkRoleIds.WoodHauler
            || !_stockpiles.Any(stockpile => stockpile.Id == workplaceId) || _workplacePositions.ContainsKey(id))
            return EmploymentResult.InvalidPosition;
        UsesWorkplaceEmployment = true;
        _workplacePositions.Add(id, new(id, workplaceId, roleId));
        return EmploymentResult.Accepted;
    }

    public EmploymentResult AssignWorkerToPosition(string? workerId, string? positionId)
    {
        if (!TryGetEntity(workerId, out var worker) || worker is null || worker.Id == PlayerEntityId)
            return EmploymentResult.InvalidWorker;
        if (!_settlementWorkers.ContainsKey(worker.Id)) return EmploymentResult.NotResident;
        if (worker.Health.IsIncapacitated) return EmploymentResult.Unavailable;
        if (positionId is null || !_workplacePositions.TryGetValue(positionId, out var position))
            return EmploymentResult.InvalidPosition;
        if (position.WorkerId == worker.Id) return EmploymentResult.Unchanged;
        if (position.WorkerId is not null) return EmploymentResult.Occupied;
        if (GetEmployment(worker.Id) is not null) return EmploymentResult.AlreadyEmployed;
        if (SpeciesCatalog is null || !SpeciesCatalog.TryGet(worker.SpeciesId, out var species) || species is null
            || !WorkRoleIds.Supports(position.RoleId, species)) return EmploymentResult.Incapable;
        if (!CanWorkAtStockpile(worker, position.WorkplaceId)) return EmploymentResult.Unavailable;
        // A legacy free-hauling task may still target a different stockpile.
        // Rebuild the task from the new employment, preserving any carried wood.
        if (worker.Actions.Current?.Kind == ActionKind.Haul) worker.Actions.Cancel();
        _workplacePositions[position.Id] = position with { WorkerId = worker.Id };
        return EmploymentResult.Accepted;
    }

    public EmploymentResult UnassignWorker(string? workerId, bool pauseAutoAssignment = true)
    {
        if (workerId is null || !_settlementWorkers.ContainsKey(workerId)) return EmploymentResult.NotResident;
        var preferenceChanged = pauseAutoAssignment
            && SetWorkerAutoAssignment(workerId, false) == EmploymentResult.Accepted;
        if (GetEmployment(workerId) is not { } position)
            return preferenceChanged ? EmploymentResult.Accepted : EmploymentResult.Unchanged;
        if (TryGetEntity(workerId, out var worker) && worker?.Actions.Current?.Kind == ActionKind.Haul)
            worker.Actions.Cancel();
        _workplacePositions[position.Id] = position with { WorkerId = null };
        return EmploymentResult.Accepted;
    }

    public bool RemoveWorkplacePosition(string? positionId)
    {
        if (positionId is null || !_workplacePositions.TryGetValue(positionId, out var position)) return false;
        if (position.WorkerId is { } workerId) UnassignWorker(workerId, pauseAutoAssignment: false);
        return _workplacePositions.Remove(positionId);
    }

    public int AutoAssignWorkplacePositions()
    {
        if (!UsesWorkplaceEmployment) return 0;
        ReconcileEmployment();
        var vacancies = WorkplacePositions.Where(position => position.WorkerId is null).ToArray();
        if (vacancies.Length == 0) return 0;
        var assigned = 0;
        foreach (var worker in SettlementWorkers.Where(worker => worker.AutoAssign))
        {
            if (GetEmployment(worker.EntityId) is not null) continue;
            foreach (var position in vacancies)
                if (AssignWorkerToPosition(worker.EntityId, position.Id) == EmploymentResult.Accepted)
                { assigned++; break; }
        }
        return assigned;
    }

    private bool CanWorkAtStockpile(EntityRecord worker, string stockpileId) =>
        _stockpiles.Any(stockpile => stockpile.Id == stockpileId
            && (stockpile.FactionOwnerId is null || stockpile.FactionOwnerId == worker.AllegianceFactionId));

    private void ReconcileEmployment()
    {
        foreach (var position in WorkplacePositions)
        {
            if (!_stockpiles.Any(stockpile => stockpile.Id == position.WorkplaceId))
            { RemoveWorkplacePosition(position.Id); continue; }
            if (position.WorkerId is not { } id) continue;
            if (!TryGetEntity(id, out var worker) || worker is null || worker.Health.IsDead
                || id == PlayerEntityId || !CanWorkAtStockpile(worker, position.WorkplaceId)
                || SpeciesCatalog is null || !SpeciesCatalog.TryGet(worker.SpeciesId, out var species)
                || species is null || !WorkRoleIds.Supports(position.RoleId, species))
                UnassignWorker(id, pauseAutoAssignment: false);
        }
    }

    private void ResetEmployment()
    {
        _settlementWorkers.Clear();
        _workplacePositions.Clear();
        UsesWorkplaceEmployment = false;
    }

    private void RestoreEmployment(WorldSnapshot snapshot)
    {
        UsesWorkplaceEmployment = snapshot.UsesWorkplaceEmployment;
        foreach (var worker in snapshot.SettlementWorkers!) _settlementWorkers.Add(worker.EntityId, worker);
        foreach (var position in snapshot.WorkplacePositions!) _workplacePositions.Add(position.Id, position);
    }

    internal void InitializeStarterEmployment(IEnumerable<string> residents, string stockpileId)
    {
        var ids = residents.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        foreach (var id in ids) RegisterSettlementWorker(id);
        for (var index = 1; index <= ids.Length; index++)
            AddWorkplacePosition($"{stockpileId}-hauler-{index:00}", stockpileId);
    }
}
