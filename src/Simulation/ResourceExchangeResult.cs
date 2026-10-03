namespace Simulation;

public enum ResourceExchangeResult
{
    Success,
    UnknownItem,
    InvalidAmount,
    InvalidDestination,
    InsufficientFirst,
    InsufficientSecond,
    CapacityExceededFirst,
    CapacityExceededSecond
}
