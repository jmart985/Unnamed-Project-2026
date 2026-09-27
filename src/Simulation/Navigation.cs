using System.Numerics;

namespace Simulation;

public enum NavigationOutcome
{
    InProgress,
    Arrived,
    Unreachable,
    Removed,
    Cancelled,
    TimedOut
}

public sealed record NavigationRequest
{
    public NavigationRequest(
        Vector3 target,
        double arrivalTolerance = 0.1,
        double timeoutSeconds = 5)
    {
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y) || !float.IsFinite(target.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(target), "Target coordinates must be finite.");
        }

        if (!double.IsFinite(arrivalTolerance) || arrivalTolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrivalTolerance), "Arrival tolerance must be finite and positive.");
        }

        if (!double.IsFinite(timeoutSeconds) || timeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Timeout must be finite and positive.");
        }

        Target = target;
        ArrivalTolerance = arrivalTolerance;
        TimeoutSeconds = timeoutSeconds;
    }

    public Vector3 Target { get; }
    public double ArrivalTolerance { get; }
    public double TimeoutSeconds { get; }
}

public interface INavigationAdapter
{
    string Request(NavigationRequest request);
    NavigationOutcome Poll(string requestId, double elapsedSeconds);
    bool Cancel(string requestId);
}

public sealed class FakeNavigationAdapter : INavigationAdapter
{
    private sealed class RequestState(NavigationRequest request)
    {
        public NavigationRequest Request { get; } = request;
        public double ElapsedSeconds { get; set; }
        public NavigationOutcome Outcome { get; set; } = NavigationOutcome.InProgress;
    }

    private readonly Dictionary<string, RequestState> _requests = new(StringComparer.Ordinal);
    private int _nextRequestId;

    public IReadOnlyDictionary<string, NavigationOutcome> Requests
        => _requests.ToDictionary(pair => pair.Key, pair => pair.Value.Outcome, StringComparer.Ordinal);

    public string Request(NavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestId = $"navigation-{++_nextRequestId:D6}";
        _requests.Add(requestId, new RequestState(request));
        return requestId;
    }

    public NavigationOutcome Poll(string requestId, double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        if (!_requests.TryGetValue(requestId, out var state))
        {
            return NavigationOutcome.Removed;
        }

        if (state.Outcome != NavigationOutcome.InProgress)
        {
            return state.Outcome;
        }

        state.ElapsedSeconds += elapsedSeconds;
        if (state.ElapsedSeconds >= state.Request.TimeoutSeconds)
        {
            state.Outcome = NavigationOutcome.TimedOut;
        }

        return state.Outcome;
    }

    public bool Cancel(string requestId)
    {
        if (!_requests.TryGetValue(requestId, out var state)
            || state.Outcome != NavigationOutcome.InProgress)
        {
            return false;
        }

        state.Outcome = NavigationOutcome.Cancelled;
        return true;
    }

    public bool CompleteAsArrived(string requestId)
    {
        return SetTerminalOutcome(requestId, NavigationOutcome.Arrived);
    }

    public bool MarkUnreachable(string requestId)
    {
        return SetTerminalOutcome(requestId, NavigationOutcome.Unreachable);
    }

    public bool RemoveTarget(string requestId)
    {
        return SetTerminalOutcome(requestId, NavigationOutcome.Removed);
    }

    private bool SetTerminalOutcome(string requestId, NavigationOutcome outcome)
    {
        if (!_requests.TryGetValue(requestId, out var state)
            || state.Outcome != NavigationOutcome.InProgress)
        {
            return false;
        }

        state.Outcome = outcome;
        return true;
    }
}
