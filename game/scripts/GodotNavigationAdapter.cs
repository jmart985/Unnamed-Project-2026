using Godot;
using Simulation;
using GodotVector3 = Godot.Vector3;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public sealed class GodotNavigationAdapter : INavigationAdapter
{
    private sealed class RequestState(string id, NavigationRequest request)
    {
        public string Id { get; } = id;
        public NavigationRequest Request { get; } = request;
        public double ElapsedSeconds { get; set; }
        public GodotVector3 NextPathPosition { get; set; }
        public bool HasPath { get; set; }
        public NavigationOutcome Outcome { get; set; } = NavigationOutcome.InProgress;
    }

    private readonly Node3D _owner;
    private readonly NavigationAgent3D _agent;
    private readonly Dictionary<string, RequestState> _requests = new(StringComparer.Ordinal);
    private RequestState? _activeRequest;
    private RequestState? _lastRequest;
    private int _nextRequestId;

    public GodotNavigationAdapter(Node3D owner, NavigationAgent3D agent)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(agent);
        _owner = owner;
        _agent = agent;
    }

    public bool HasActiveRequest => _activeRequest is not null;
    public string? ActiveRequestId => _activeRequest?.Id;
    public NavigationOutcome? LastOutcome => _lastRequest?.Outcome;
    public NumericsVector3? LastTarget => _lastRequest?.Request.Target;

    public string Request(NavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_activeRequest is { } current)
        {
            Cancel(current.Id);
        }

        var id = $"godot-navigation-{++_nextRequestId:D6}";
        var state = new RequestState(id, request);
        _requests.Add(id, state);
        _activeRequest = state;
        _lastRequest = state;

        _agent.TargetDesiredDistance = (float)request.ArrivalTolerance;
        _agent.PathDesiredDistance = (float)request.ArrivalTolerance;
        _agent.TargetPosition = ToGodot(request.Target);
        return id;
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
            Finish(state, NavigationOutcome.TimedOut);
            return state.Outcome;
        }

        if (ReferenceEquals(state, _activeRequest)
            && state.HasPath
            && _agent.IsNavigationFinished())
        {
            if (_agent.IsTargetReached())
            {
                Finish(state, NavigationOutcome.Arrived);
            }
            else if (!_agent.IsTargetReachable())
            {
                Finish(state, NavigationOutcome.Unreachable);
            }
        }

        return state.Outcome;
    }

    public bool Cancel(string requestId)
    {
        return TryFinish(requestId, NavigationOutcome.Cancelled);
    }

    public bool RemoveTarget(string requestId)
    {
        return TryFinish(requestId, NavigationOutcome.Removed);
    }

    public void RefreshPath()
    {
        if (_activeRequest is not { } state)
        {
            return;
        }

        state.NextPathPosition = _agent.GetNextPathPosition();
        state.HasPath = _agent.GetCurrentNavigationPath().Length > 0;
    }

    public void AdvanceMovement(double simulationSeconds, float movementSpeed)
    {
        if (!double.IsFinite(simulationSeconds) || simulationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationSeconds));
        }

        if (!float.IsFinite(movementSpeed) || movementSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(movementSpeed));
        }

        if (_activeRequest is not { } state
            || Poll(state.Id, simulationSeconds) != NavigationOutcome.InProgress
            || !state.HasPath)
        {
            return;
        }

        _owner.GlobalPosition = _owner.GlobalPosition.MoveToward(
            state.NextPathPosition,
            movementSpeed * (float)simulationSeconds);
    }

    private bool TryFinish(string requestId, NavigationOutcome outcome)
    {
        if (!_requests.TryGetValue(requestId, out var state)
            || state.Outcome != NavigationOutcome.InProgress)
        {
            return false;
        }

        Finish(state, outcome);
        return true;
    }

    private void Finish(RequestState state, NavigationOutcome outcome)
    {
        state.Outcome = outcome;
        if (ReferenceEquals(state, _activeRequest))
        {
            _activeRequest = null;
        }
    }

    private static GodotVector3 ToGodot(NumericsVector3 position)
    {
        return new GodotVector3(position.X, position.Y, position.Z);
    }
}
