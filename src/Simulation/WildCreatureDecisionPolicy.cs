using System.Numerics;

namespace Simulation;

public enum WildCreatureDecision
{
    Idle,
    SeekFood,
    Flee,
    Pursue,
    ReturnToArea
}

/// <summary>Identifies a caller-supplied stimulus target, not a navigation command.</summary>
public sealed record WildCreatureDecisionTarget
{
    public WildCreatureDecisionTarget(string id, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A decision target ID is required.", nameof(id));
        }

        if (!IsFinite(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Target position coordinates must be finite.");
        }

        Id = id;
        Position = position;
    }

    public string Id { get; }
    public Vector3 Position { get; }

    private static bool IsFinite(Vector3 position)
    {
        return float.IsFinite(position.X)
            && float.IsFinite(position.Y)
            && float.IsFinite(position.Z);
    }
}

/// <summary>
/// Contains already-resolved stimuli. Food must be available and compatible; threat and
/// pursuit targets, plus any return-area destination, are supplied by the caller.
/// </summary>
public sealed record WildCreatureDecisionContext
{
    public WildCreatureDecisionContext(
        WildCreatureDecisionTarget? compatibleFood = null,
        WildCreatureDecisionTarget? threat = null,
        WildCreatureDecisionTarget? pursuitTarget = null,
        bool isOutsideArea = false,
        WildCreatureDecisionTarget? returnAreaTarget = null)
    {
        if (isOutsideArea && returnAreaTarget is null)
        {
            throw new ArgumentException(
                "A return-area target is required when the creature is outside its area.",
                nameof(returnAreaTarget));
        }

        CompatibleFood = compatibleFood;
        Threat = threat;
        PursuitTarget = pursuitTarget;
        IsOutsideArea = isOutsideArea;
        ReturnAreaTarget = returnAreaTarget;
    }

    public WildCreatureDecisionTarget? CompatibleFood { get; }
    public WildCreatureDecisionTarget? Threat { get; }
    public WildCreatureDecisionTarget? PursuitTarget { get; }
    public bool IsOutsideArea { get; }
    public WildCreatureDecisionTarget? ReturnAreaTarget { get; }
}

/// <summary>
/// A decision and the stimulus it refers to. For Flee, the target identifies the threat,
/// not the direction or destination for movement.
/// </summary>
public sealed record WildCreatureDecisionResult
{
    public WildCreatureDecisionResult(WildCreatureDecision decision, WildCreatureDecisionTarget? target)
    {
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        if ((decision == WildCreatureDecision.Idle) != (target is null))
        {
            throw new ArgumentException(
                "Idle decisions cannot have a target, and active decisions require one.",
                nameof(target));
        }

        Decision = decision;
        Target = target;
    }

    public WildCreatureDecision Decision { get; }
    public WildCreatureDecisionTarget? Target { get; }
}

/// <summary>Chooses one deterministic priority outcome without mutating simulation state.</summary>
public static class WildCreatureDecisionPolicy
{
    public static WildCreatureDecisionResult Decide(
        EntityRecord entity,
        WildCreatureDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Threat is not null)
        {
            return new WildCreatureDecisionResult(WildCreatureDecision.Flee, context.Threat);
        }

        if (entity.Needs.IsHungry && context.CompatibleFood is not null)
        {
            return new WildCreatureDecisionResult(WildCreatureDecision.SeekFood, context.CompatibleFood);
        }

        if (context.IsOutsideArea)
        {
            return new WildCreatureDecisionResult(WildCreatureDecision.ReturnToArea, context.ReturnAreaTarget);
        }

        if (context.PursuitTarget is not null)
        {
            return new WildCreatureDecisionResult(WildCreatureDecision.Pursue, context.PursuitTarget);
        }

        return new WildCreatureDecisionResult(WildCreatureDecision.Idle, null);
    }
}
