namespace Simulation;

public enum CreatureCaptivityState
{
    Wild,
    Captured,
    Captive,
    Domesticated
}

public enum CreatureLifecycleTransitionResult
{
    Transitioned,
    TargetNotFound,
    NotCreature,
    TargetDead,
    InvalidTransition,
    AlreadyInState
}

public enum CreatureCaptureResult
{
    Captured,
    AlreadyCaptured,
    TargetNotFound,
    NotCreature,
    TargetDead,
    AlreadyCaptive,
    InvalidTransition
}
