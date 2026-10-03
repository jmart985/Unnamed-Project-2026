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
