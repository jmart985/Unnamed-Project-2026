namespace Simulation;

public sealed record SocialMeleeAttackResolution(
    MeleeAttackResolution Combat,
    RelationshipEventApplyResult? RelationshipEvent);

public sealed class SocialMeleeCombatService
{
    private const string HarmedEventId = "harmed";

    private readonly World _world;
    private readonly RelationshipEventService _relationshipEvents;

    public SocialMeleeCombatService(World world, RelationshipEventCatalog eventCatalog)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        ArgumentNullException.ThrowIfNull(eventCatalog);
        if (!eventCatalog.TryGet(HarmedEventId, out _))
        {
            throw new ArgumentException(
                $"Relationship event catalog must define '{HarmedEventId}'.",
                nameof(eventCatalog));
        }

        _relationshipEvents = new RelationshipEventService(world, eventCatalog);
    }

    public SocialMeleeAttackResolution Resolve(
        MeleeAttackRequest? request,
        double maximumReach = double.MaxValue)
    {
        var combat = MeleeCombatService.Resolve(_world, request, maximumReach);
        if (combat.Result != MeleeAttackResult.Success || request is null)
        {
            return new SocialMeleeAttackResolution(combat, null);
        }

        var relationshipEvent = _relationshipEvents.Apply(
            request.DefenderId,
            request.AttackerId,
            HarmedEventId);
        if (relationshipEvent != RelationshipEventApplyResult.Applied)
        {
            throw new InvalidOperationException(
                $"A successful melee attack could not apply its required '{HarmedEventId}' relationship event: {relationshipEvent}.");
        }

        return new SocialMeleeAttackResolution(combat, relationshipEvent);
    }
}
