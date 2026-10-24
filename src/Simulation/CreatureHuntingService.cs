using System.Numerics;

namespace Simulation;

public enum CreatureHuntingStatus
{
    NotEligible,
    NotHungry,
    NoPrey,
    Waiting,
    Traveling,
    Attacked,
    Harvested,
    ActionRejected,
    TargetInvalid
}

public sealed record CreatureHuntingResult(
    CreatureHuntingStatus Status,
    string? TargetEntityId = null,
    FoodSource? Carcass = null,
    double Damage = 0,
    string? Reason = null);

/// <summary>Runs a data-configured wild predator hunt through existing perception, navigation, combat, and harvest systems.</summary>
public sealed class CreatureHuntingService : ITransientSimulationState
{
    private const double AttackReach = 1.6;
    private const double NavigationArrivalTolerance = 1.2;
    private const double NavigationTimeoutSeconds = 30;
    private const double RepathDistance = 1.0;
    private const int AttackIntervalTicks = 5;
    private const double DamagePerStrength = 10;

    private readonly World _world;
    private readonly SpeciesCatalog _catalog;
    private readonly PerceptionQueryService _perception;
    private readonly Func<EntityRecord, INavigationAdapter> _navigationForEntity;
    private readonly IMeleeDamageResponder _damageResponder;
    private readonly Dictionary<string, ActiveHunt> _activeHunts = new(StringComparer.Ordinal);
    private readonly RetryScheduler _retries = new();
    private long _nextHuntId;

    public CreatureHuntingService(
        World world,
        SpeciesCatalog catalog,
        Func<EntityRecord, INavigationAdapter> navigationForEntity,
        IMeleeDamageResponder? damageResponder = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _navigationForEntity = navigationForEntity ?? throw new ArgumentNullException(nameof(navigationForEntity));
        _perception = new PerceptionQueryService(world, catalog);
        _damageResponder = damageResponder ?? new BehavioralTrigger(world);
    }

    public bool IsHunting(string? entityId) =>
        !string.IsNullOrWhiteSpace(entityId) && _activeHunts.ContainsKey(entityId);

    public CreatureHuntingResult Advance(EntityRecord hunter, long simulationTick)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        if (simulationTick < 0)
            throw new ArgumentOutOfRangeException(nameof(simulationTick), "Simulation tick cannot be negative.");
        if (!_world.TryGetEntity(hunter.Id, out var liveHunter) || !ReferenceEquals(hunter, liveHunter))
            return new(CreatureHuntingStatus.NotEligible, Reason: "The hunter is not part of this world.");
        if (!_catalog.TryGet(hunter.SpeciesId, out var species) || species is null)
            return new(CreatureHuntingStatus.NotEligible, Reason: "The hunter species is not configured.");

        if (!species.IsPredator || species.PreySpeciesIds.Count == 0
            || hunter.CaptivityState != CreatureCaptivityState.Wild
            || hunter.Health.IsDead || hunter.Health.IsIncapacitated)
        {
            CancelHunt(hunter);
            return new(CreatureHuntingStatus.NotEligible);
        }

        if (!hunter.Needs.IsHungry)
        {
            CancelHunt(hunter);
            return new(CreatureHuntingStatus.NotHungry);
        }

        if (_activeHunts.TryGetValue(hunter.Id, out var active))
        {
            if (hunter.Actions.Current?.Id != active.ActionId)
            {
                CancelHunt(hunter);
            }
            else
            {
                return AdvanceActiveHunt(hunter, species, active, simulationTick);
            }
        }

        if (_retries.IsWaiting(hunter.Id, simulationTick))
            return new(CreatureHuntingStatus.Waiting, Reason: "Waiting before reconsidering nearby prey.");

        var prey = SelectPrey(hunter, species);
        if (prey is null)
        {
            _retries.ScheduleRetry(hunter.Id, simulationTick);
            return new(CreatureHuntingStatus.NoPrey);
        }

        var target = new WildCreatureDecisionTarget(prey.Id, prey.Position);
        var decision = WildCreatureDecisionPolicy.Decide(
            hunter,
            new WildCreatureDecisionContext(pursuitTarget: target));
        if (decision.Decision != WildCreatureDecision.Pursue || decision.Target is null)
            return new(CreatureHuntingStatus.Waiting, prey.Id, Reason: "Another wild-creature need has priority.");

        return StartHunt(hunter, species, prey, simulationTick);
    }

    public void Reset()
    {
        foreach (var entityId in _activeHunts.Keys.ToArray())
            if (_world.TryGetEntity(entityId, out var entity) && entity is not null)
                CancelHunt(entity);
        _activeHunts.Clear();
        _retries.Reset();
        _nextHuntId = 0;
    }

    private CreatureHuntingResult StartHunt(
        EntityRecord hunter,
        SpeciesDefinition species,
        EntityRecord prey,
        long simulationTick)
    {
        var actionId = $"hunt-{hunter.Id}-{checked(++_nextHuntId):D6}";
        ActiveHunt? active = null;
        var result = hunter.Actions.TryStart(
            new ActionCandidate(actionId, ActionKind.Hunt),
            () => OnActionFinished(hunter.Id, actionId));
        if (result is not (ActionStartResult.Started or ActionStartResult.Preempted))
            return new(CreatureHuntingStatus.ActionRejected, prey.Id);

        active = new ActiveHunt(actionId, prey.Id, simulationTick);
        _activeHunts[hunter.Id] = active;
        hunter.TargetId = prey.Id;
        hunter.Activity = "Hunting";
        hunter.WaitingReason = null;
        _retries.Clear(hunter.Id);

        if (Distance(hunter.Position, prey.Position) <= AttackReach)
            return AttackOrHarvest(hunter, species, prey, active, simulationTick);

        if (!RequestNavigation(hunter, active))
            return FailHunt(hunter, active, simulationTick, "The prey route could not be started.");

        return new(CreatureHuntingStatus.Traveling, prey.Id);
    }

    private CreatureHuntingResult AdvanceActiveHunt(
        EntityRecord hunter,
        SpeciesDefinition species,
        ActiveHunt active,
        long simulationTick)
    {
        if (!_world.TryGetEntity(active.PreyId, out var prey) || prey is null)
            return FailHunt(hunter, active, simulationTick, "The prey is no longer available.", CreatureHuntingStatus.TargetInvalid);
        if (prey.Health.IsDead)
            return Harvest(hunter, active, prey, species, simulationTick);
        if (!IsEligiblePrey(hunter, species, prey)
            || Distance(hunter.Position, prey.Position) > species.SmellRange)
        {
            return FailHunt(hunter, active, simulationTick, "The prey is no longer a valid nearby target.", CreatureHuntingStatus.TargetInvalid);
        }

        if (Distance(hunter.Position, prey.Position) <= AttackReach)
        {
            CancelNavigation(active);
            active.NavigationTarget = null;
            return AttackOrHarvest(hunter, species, prey, active, simulationTick);
        }

        if (active.NavigationRequestId is null || active.NavigationTarget is null
            || Distance(active.NavigationTarget.Value, prey.Position) > RepathDistance)
        {
            CancelNavigation(active);
            if (!RequestNavigation(hunter, active, prey.Position))
                return FailHunt(hunter, active, simulationTick, "The prey route could not be updated.");
            return new(CreatureHuntingStatus.Traveling, prey.Id);
        }

        var outcome = active.Navigator!.Poll(active.NavigationRequestId, 0);
        if (outcome == NavigationOutcome.InProgress)
            return new(CreatureHuntingStatus.Traveling, prey.Id);
        if (outcome == NavigationOutcome.Arrived)
        {
            CancelNavigation(active);
            return Distance(hunter.Position, prey.Position) <= AttackReach
                ? AttackOrHarvest(hunter, species, prey, active, simulationTick)
                : new(CreatureHuntingStatus.Traveling, prey.Id, Reason: "Closing the remaining distance to prey.");
        }

        return FailHunt(hunter, active, simulationTick, $"The prey route failed: {outcome}.");
    }

    private CreatureHuntingResult AttackOrHarvest(
        EntityRecord hunter,
        SpeciesDefinition hunterSpecies,
        EntityRecord prey,
        ActiveHunt active,
        long simulationTick)
    {
        if (prey.Health.IsDead)
            return Harvest(hunter, active, prey, hunterSpecies, simulationTick);
        if (simulationTick < active.NextAttackTick)
            return new(CreatureHuntingStatus.Attacked, prey.Id, Damage: 0, Reason: "Waiting for the next bite.");

        var attack = MeleeCombatService.Resolve(
            _world,
            new MeleeAttackRequest(hunter.Id, prey.Id, "Torso", hunter.BaseStats.Strength * DamagePerStrength),
            maximumReach: AttackReach,
            damageResponder: _damageResponder);
        if (attack.Result == MeleeAttackResult.Success)
        {
            active.NextAttackTick = simulationTick > long.MaxValue - AttackIntervalTicks
                ? long.MaxValue
                : simulationTick + AttackIntervalTicks;
            if (attack.DefenderDead)
                return Harvest(hunter, active, prey, hunterSpecies, simulationTick, attack.AppliedDamage);
            return new(CreatureHuntingStatus.Attacked, prey.Id, Damage: attack.AppliedDamage);
        }

        if (attack.Result == MeleeAttackResult.OutOfRange)
        {
            CancelNavigation(active);
            if (!RequestNavigation(hunter, active, prey.Position))
                return FailHunt(hunter, active, simulationTick, "The prey moved out of attack range and could not be followed.");
            return new(CreatureHuntingStatus.Traveling, prey.Id);
        }

        return FailHunt(hunter, active, simulationTick, $"The attack could not continue: {attack.Result}.");
    }

    private CreatureHuntingResult Harvest(
        EntityRecord hunter,
        ActiveHunt active,
        EntityRecord prey,
        SpeciesDefinition hunterSpecies,
        long simulationTick,
        double damage = 0)
    {
        if (!_catalog.TryGet(prey.SpeciesId, out var preySpecies) || preySpecies is null)
            return FailHunt(hunter, active, simulationTick, "The prey species definition is missing.", CreatureHuntingStatus.TargetInvalid);

        var harvest = CreatureHarvestService.Harvest(
            _world, prey, preySpecies.CarcassMeatUnits, preySpecies.CarcassFreshnessSeconds);
        if (harvest.Status != CreatureHarvestStatus.Harvested || harvest.Carcass is null)
            return FailHunt(hunter, active, simulationTick, $"Carcass creation failed: {harvest.Status}.", CreatureHuntingStatus.TargetInvalid);

        CompleteHunt(hunter, active);
        hunter.Activity = "Hunting";
        hunter.WaitingReason = null;
        return new(CreatureHuntingStatus.Harvested, prey.Id, harvest.Carcass, damage);
    }

    private CreatureHuntingResult FailHunt(
        EntityRecord hunter,
        ActiveHunt active,
        long simulationTick,
        string reason,
        CreatureHuntingStatus status = CreatureHuntingStatus.Waiting)
    {
        CompleteHunt(hunter, active);
        hunter.Activity = "Waiting";
        hunter.WaitingReason = reason;
        _retries.ScheduleRetry(hunter.Id, simulationTick);
        return new(status, active.PreyId, Reason: reason);
    }

    private EntityRecord? SelectPrey(EntityRecord hunter, SpeciesDefinition species)
    {
        var nearby = _perception.FindNearbyEntities(hunter.Id);
        if (nearby.Status != PerceptionQueryStatus.Success)
            return null;
        return nearby.Results.FirstOrDefault(candidate => IsEligiblePrey(hunter, species, candidate));
    }

    private static bool IsEligiblePrey(
        EntityRecord hunter,
        SpeciesDefinition hunterSpecies,
        EntityRecord candidate)
    {
        if (ReferenceEquals(hunter, candidate)
            || candidate.Health.IsDead
            || candidate.CaptivityState != CreatureCaptivityState.Wild
            || !hunterSpecies.PreySpeciesIds.Contains(candidate.SpeciesId, StringComparer.Ordinal))
            return false;

        if (hunter.AllegianceFactionId is { } factionId
            && string.Equals(factionId, candidate.AllegianceFactionId, StringComparison.Ordinal))
            return false;

        return true;
    }

    private bool RequestNavigation(EntityRecord hunter, ActiveHunt active, Vector3? target = null)
    {
        try
        {
            active.Navigator = _navigationForEntity(hunter)
                ?? throw new InvalidOperationException("The hunter has no navigation adapter.");
            active.NavigationTarget = target
                ?? (_world.TryGetEntity(active.PreyId, out var prey) && prey is not null ? prey.Position : null);
            if (active.NavigationTarget is null)
                return false;
            active.NavigationRequestId = active.Navigator.Request(new NavigationRequest(
                active.NavigationTarget.Value,
                NavigationArrivalTolerance,
                NavigationTimeoutSeconds));
            return !string.IsNullOrWhiteSpace(active.NavigationRequestId);
        }
        catch
        {
            CancelNavigation(active);
            return false;
        }
    }

    private void CancelHunt(EntityRecord hunter)
    {
        if (!_activeHunts.Remove(hunter.Id, out var active))
            return;
        CancelNavigation(active);
        if (hunter.Actions.Current?.Id == active.ActionId)
            hunter.Actions.Cancel();
        ClearTarget(hunter, active);
    }

    private void CompleteHunt(EntityRecord hunter, ActiveHunt active)
    {
        _activeHunts.Remove(hunter.Id);
        CancelNavigation(active);
        if (hunter.Actions.Current?.Id == active.ActionId)
            hunter.Actions.Complete();
        ClearTarget(hunter, active);
    }

    private void OnActionFinished(string hunterId, string actionId)
    {
        if (!_activeHunts.TryGetValue(hunterId, out var active)
            || !string.Equals(active.ActionId, actionId, StringComparison.Ordinal))
            return;
        _activeHunts.Remove(hunterId);
        CancelNavigation(active);
    }

    private static void ClearTarget(EntityRecord hunter, ActiveHunt active)
    {
        if (string.Equals(hunter.TargetId, active.PreyId, StringComparison.Ordinal))
            hunter.TargetId = null;
    }

    private static void CancelNavigation(ActiveHunt active)
    {
        if (active.Navigator is not null && active.NavigationRequestId is not null)
            active.Navigator.Cancel(active.NavigationRequestId);
        active.NavigationRequestId = null;
        active.NavigationTarget = null;
    }

    private static double Distance(Vector3 first, Vector3 second)
    {
        var x = (double)first.X - second.X;
        var y = (double)first.Y - second.Y;
        var z = (double)first.Z - second.Z;
        return Math.Sqrt(x * x + y * y + z * z);
    }

    private sealed class ActiveHunt(string actionId, string preyId, long startTick)
    {
        public string ActionId { get; } = actionId;
        public string PreyId { get; } = preyId;
        public Vector3? NavigationTarget { get; set; }
        public INavigationAdapter? Navigator { get; set; }
        public string? NavigationRequestId { get; set; }
        public long NextAttackTick { get; set; } = startTick;
    }
}
