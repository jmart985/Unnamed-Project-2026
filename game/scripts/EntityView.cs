using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class EntityView : Node3D
{
    private GodotNavigationAdapter? _navigationAdapter;
    private float _movementSpeed;

    public string EntityId { get; private set; } = string.Empty;
    public NavigationOutcome? NavigationOutcome => _navigationAdapter?.LastOutcome;
    public NumericsVector3? NavigationTarget => _navigationAdapter?.LastTarget;
    public INavigationAdapter NavigationAdapter => _navigationAdapter
        ?? throw new InvalidOperationException("The entity view is not ready for navigation.");

    public override void _Ready()
    {
        var navigationAgent = new NavigationAgent3D
        {
            PathDesiredDistance = 0.1f,
            TargetDesiredDistance = 0.1f
        };
        AddChild(navigationAgent);
        _navigationAdapter = new GodotNavigationAdapter(this, navigationAgent);

        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Height = 0.6f, Radius = 0.3f }
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        _navigationAdapter?.RefreshPath();
    }

    public void Bind(string entityId, float movementSpeed)
    {
        if (!float.IsFinite(movementSpeed) || movementSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(movementSpeed));
        }

        EntityId = entityId;
        _movementSpeed = movementSpeed;
    }

    public bool Sync(World world)
    {
        if (!world.TryGetEntity(EntityId, out var entity) || entity is null)
        {
            return false;
        }

        Position = new Vector3(entity.Position.X, entity.Position.Y, entity.Position.Z);
        return true;
    }

    public string RequestNavigation(NumericsVector3 target)
    {
        var adapter = _navigationAdapter
            ?? throw new InvalidOperationException("The entity view is not ready for navigation.");
        return adapter.Request(new NavigationRequest(target, timeoutSeconds: 30));
    }

    public bool CancelNavigation()
    {
        return _navigationAdapter?.ActiveRequestId is { } requestId
            && _navigationAdapter.Cancel(requestId);
    }

    public bool RemoveNavigationTarget()
    {
        return _navigationAdapter?.ActiveRequestId is { } requestId
            && _navigationAdapter.RemoveTarget(requestId);
    }

    public void AdvanceNavigation(double simulationSeconds, World world)
    {
        if (_navigationAdapter is not { HasActiveRequest: true } adapter)
        {
            return;
        }

        adapter.AdvanceMovement(simulationSeconds, _movementSpeed);
        var position = GlobalPosition;
        if (!world.TrySetEntityPosition(
                EntityId,
                new NumericsVector3(position.X, position.Y, position.Z))
            && adapter.ActiveRequestId is { } requestId)
        {
            adapter.RemoveTarget(requestId);
        }
    }

    public NavigationOutcome? PollNavigation(double simulationSeconds)
    {
        return _navigationAdapter?.ActiveRequestId is { } requestId
            ? _navigationAdapter.Poll(requestId, simulationSeconds)
            : _navigationAdapter?.LastOutcome;
    }
}
