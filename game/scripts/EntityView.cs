using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class EntityView : CharacterBody3D
{
    private GodotNavigationAdapter? _navigationAdapter;
    private float _movementSpeed;
    private bool _isCreature;

    public string EntityId { get; private set; } = string.Empty;
    public NavigationOutcome? NavigationOutcome => _navigationAdapter?.LastOutcome;
    public NumericsVector3? NavigationTarget => _navigationAdapter?.LastTarget;
    public INavigationAdapter NavigationAdapter
        => _navigationAdapter ?? throw new InvalidOperationException("The entity view is not ready for navigation.");

    public override void _Ready()
    {
        var navigationAgent = new NavigationAgent3D { PathDesiredDistance = 0.1f, TargetDesiredDistance = 0.1f };
        AddChild(navigationAgent);
        _navigationAdapter = new GodotNavigationAdapter(this, navigationAgent);
        BuildBodyVisuals();
    }

    private void BuildBodyVisuals()
    {
        var bodySize = _isCreature ? new Vector3(0.75f, 0.65f, 1.25f) : new Vector3(0.65f, 0.9f, 0.45f);
        var bodyHeight = bodySize.Y;
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = Mathf.Max(bodySize.X, bodySize.Z) * 0.5f, Height = bodyHeight + 0.2f },
            Position = new Vector3(0, bodyHeight * 0.5f + 0.1f, 0)
        });
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = bodySize }, Position = new Vector3(0, bodyHeight * 0.5f + 0.1f, 0) });
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Height = 0.45f, Radius = 0.25f },
            Position = _isCreature ? new Vector3(0, 1.15f, -0.78f) : new Vector3(0, 1.75f, 0)
        });
        if (_isCreature)
        {
            AddLimb(new Vector3(-0.3f, 0.25f, -0.42f)); AddLimb(new Vector3(0.3f, 0.25f, -0.42f));
            AddLimb(new Vector3(-0.3f, 0.25f, 0.42f)); AddLimb(new Vector3(0.3f, 0.25f, 0.42f));
        }
        else
        {
            AddLimb(new Vector3(-0.2f, -0.25f, 0)); AddLimb(new Vector3(0.2f, -0.25f, 0));
        }
    }

    private void AddLimb(Vector3 position)
    {
        AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Height = 0.65f, Radius = 0.11f }, Position = position });
    }

    public override void _PhysicsProcess(double delta) => _navigationAdapter?.RefreshPath();

    public void Bind(string entityId, float movementSpeed, bool isCreature = false)
    {
        if (!float.IsFinite(movementSpeed) || movementSpeed <= 0) throw new ArgumentOutOfRangeException(nameof(movementSpeed));
        EntityId = entityId;
        _movementSpeed = movementSpeed;
        _isCreature = isCreature;
    }

    public bool Sync(World world)
    {
        if (!world.TryGetEntity(EntityId, out var entity) || entity is null) return false;
        GlobalPosition = new Vector3(entity.Position.X, entity.Position.Y, entity.Position.Z);
        return true;
    }

    public string RequestNavigation(NumericsVector3 target) => NavigationAdapter.Request(new NavigationRequest(target, timeoutSeconds: 30));
    public bool CancelNavigation() => _navigationAdapter?.ActiveRequestId is { } requestId && _navigationAdapter.Cancel(requestId);
    public bool RemoveNavigationTarget() => _navigationAdapter?.ActiveRequestId is { } requestId && _navigationAdapter.RemoveTarget(requestId);

    public void AdvanceNavigation(double simulationSeconds, World world)
    {
        if (_navigationAdapter is not { HasActiveRequest: true } adapter) return;
        adapter.AdvanceMovement(simulationSeconds, _movementSpeed);
        var position = GlobalPosition;
        if (!world.TrySetEntityPosition(EntityId, new NumericsVector3(position.X, position.Y, position.Z)) && adapter.ActiveRequestId is { } requestId)
            adapter.RemoveTarget(requestId);
    }
}
