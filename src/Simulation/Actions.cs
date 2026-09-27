namespace Simulation;

public enum ActionKind
{
    Idle,
    Wander,
    Haul,
    Eat
}

public enum ActionStatus
{
    Active,
    Completed,
    Cancelled
}

public enum ActionStartResult
{
    Started,
    Preempted,
    Rejected
}

public sealed class ActionCandidate
{
    public ActionCandidate(string id, ActionKind kind)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("An action ID is required.", nameof(id));
        }

        Id = id;
        Kind = kind;
    }

    public string Id { get; }
    public ActionKind Kind { get; }
    public int Priority => Kind switch
    {
        ActionKind.Eat => 300,
        ActionKind.Haul => 200,
        ActionKind.Wander => 100,
        ActionKind.Idle => 0,
        _ => throw new ArgumentOutOfRangeException()
    };
}

public sealed class ActionState
{
    internal ActionState(ActionCandidate candidate, Action? cleanup)
    {
        Id = candidate.Id;
        Kind = candidate.Kind;
        Priority = candidate.Priority;
        Cleanup = cleanup;
    }

    public string Id { get; }
    public ActionKind Kind { get; }
    public int Priority { get; }
    public double Progress { get; internal set; }
    public ActionStatus Status { get; internal set; } = ActionStatus.Active;
    internal Action? Cleanup { get; }
}

public sealed class ActionLifecycle
{
    public ActionState? Current { get; private set; }

    public ActionStartResult TryStart(ActionCandidate candidate, Action? cleanup = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (Current is null)
        {
            Current = new ActionState(candidate, cleanup);
            return ActionStartResult.Started;
        }

        if (candidate.Priority <= Current.Priority)
        {
            return ActionStartResult.Rejected;
        }

        FinishCurrent(ActionStatus.Cancelled);
        Current = new ActionState(candidate, cleanup);
        return ActionStartResult.Preempted;
    }

    public bool Complete()
    {
        return FinishCurrent(ActionStatus.Completed);
    }

    public bool AdvanceProgress(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Progress must be finite and nonnegative.");
        }

        if (Current is null)
        {
            return false;
        }

        Current.Progress = Math.Clamp(Current.Progress + amount, 0, 1);
        if (Current.Progress >= 1)
        {
            Complete();
        }

        return true;
    }

    public bool Cancel()
    {
        return FinishCurrent(ActionStatus.Cancelled);
    }

    public static ActionCandidate? Select(IEnumerable<ActionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .OrderByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private bool FinishCurrent(ActionStatus status)
    {
        if (Current is null)
        {
            return false;
        }

        var finished = Current;
        finished.Status = status;
        Current = null;
        finished.Cleanup?.Invoke();
        return true;
    }
}
