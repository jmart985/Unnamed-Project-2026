namespace Simulation;

public enum FactionDefinitionResult
{
    Defined,
    InvalidId,
    DuplicateId
}

public enum FactionAllegianceResult
{
    Assigned,
    Removed,
    EntityNotFound,
    UnknownFaction
}

public enum FactionReputationResult
{
    Applied,
    EntityNotFound,
    UnknownFaction,
    InvalidDelta,
    OutOfRange
}

public enum FactionHostilityResult
{
    Updated,
    UnknownFaction,
    SameFaction
}

public enum FactionOwnershipResult
{
    Assigned,
    Removed,
    ContainerNotFound,
    UnknownFaction
}

public sealed record FactionReputationState(
    string EntityId,
    string FactionId,
    double Value);

public sealed record FactionHostilityState(
    string SourceFactionId,
    string TargetFactionId,
    bool IsHostile);
