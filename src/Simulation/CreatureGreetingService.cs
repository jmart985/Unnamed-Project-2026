namespace Simulation;

public enum CreatureGreetingResultCode
{
    Greeted,
    PlayerUnavailable,
    TargetNotFound,
    SelfTarget,
    UnsupportedSpecies,
    OutOfRange,
    RelationshipEventUnavailable,
    RelationshipRejected
}

public sealed record CreatureGreetingResult(
    CreatureGreetingResultCode Code,
    string? TargetEntityId = null,
    string? Reason = null);

public sealed class CreatureGreetingService
{
    public const string GreetedEventId = "greeted";
    public const double DefaultMaximumReach = 2.0;

    private readonly World _world;
    private readonly SpeciesCatalog _speciesCatalog;
    private readonly RelationshipEventService _relationshipEvents;
    private readonly double _maximumReach;

    public CreatureGreetingService(
        World world,
        SpeciesCatalog speciesCatalog,
        RelationshipEventCatalog relationshipEventCatalog,
        double maximumReach = DefaultMaximumReach)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _speciesCatalog = speciesCatalog ?? throw new ArgumentNullException(nameof(speciesCatalog));
        ArgumentNullException.ThrowIfNull(relationshipEventCatalog);
        if (!double.IsFinite(maximumReach) || maximumReach < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumReach));
        }

        _relationshipEvents = new RelationshipEventService(world, relationshipEventCatalog);
        _maximumReach = maximumReach;
    }

    public CreatureGreetingResult Greet(string? targetEntityId)
    {
        if (_world.PlayerEntityId is not { } playerEntityId
            || !_world.TryGetEntity(playerEntityId, out var player)
            || player is null)
        {
            return new(CreatureGreetingResultCode.PlayerUnavailable, Reason: "The session player is unavailable.");
        }

        if (string.IsNullOrWhiteSpace(targetEntityId))
        {
            return new(CreatureGreetingResultCode.TargetNotFound, Reason: "Select a creature before greeting.");
        }

        if (!_world.TryGetEntity(targetEntityId, out var target) || target is null)
        {
            return new(CreatureGreetingResultCode.TargetNotFound, targetEntityId, "The selected entity no longer exists.");
        }

        if (string.Equals(player.Id, target.Id, StringComparison.Ordinal))
        {
            return new(CreatureGreetingResultCode.SelfTarget, target.Id, "The player cannot greet themself.");
        }

        if (!_speciesCatalog.TryGet(target.SpeciesId, out var species)
            || species is null
            || !species.CanAcceptFriendlyInteraction)
        {
            return new(
                CreatureGreetingResultCode.UnsupportedSpecies,
                target.Id,
                "This species does not accept friendly interaction.");
        }

        if (MeleeReachValidator.Validate(player.Position, target.Position, _maximumReach) != MeleeReachResult.InRange)
        {
            return new(CreatureGreetingResultCode.OutOfRange, target.Id, "Move closer before greeting this creature.");
        }

        return _relationshipEvents.Apply(player.Id, target.Id, GreetedEventId) switch
        {
            RelationshipEventApplyResult.Applied =>
                new(CreatureGreetingResultCode.Greeted, target.Id, $"Greeted {species.DisplayName}."),
            RelationshipEventApplyResult.UnknownEvent =>
                new(CreatureGreetingResultCode.RelationshipEventUnavailable, target.Id, "The greeted relationship profile is unavailable."),
            RelationshipEventApplyResult.InvalidParticipant =>
                new(CreatureGreetingResultCode.TargetNotFound, target.Id, "The player or selected entity no longer exists."),
            RelationshipEventApplyResult.InvalidDelta =>
                new(CreatureGreetingResultCode.RelationshipRejected, target.Id, "The greeted relationship profile was rejected."),
            _ => throw new ArgumentOutOfRangeException(nameof(targetEntityId))
        };
    }
}
