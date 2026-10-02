using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class EntityView : CharacterBody3D
{
    private GodotNavigationAdapter? _navigationAdapter;
    private float _movementSpeed;
    private bool _isCreature;
    private string _speciesId = string.Empty;

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
        // Deterministic per-entity variation so individuals are distinguishable without extra state.
        var hash = (uint)StringComparer.Ordinal.GetHashCode(EntityId);
        var variation = (hash % 100) / 100f;
        var (bodyColor, accentColor) = _speciesId switch
        {
            "cow" => (new Color(0.92f, 0.9f, 0.85f), new Color(0.15f, 0.12f, 0.1f)),
            "wolf" => (new Color(0.45f, 0.45f, 0.5f), new Color(0.2f, 0.2f, 0.24f)),
            "ape" => (new Color(0.3f, 0.2f, 0.14f), new Color(0.55f, 0.4f, 0.3f)),
            _ when _isCreature => (new Color(0.6f, 0.45f + variation * 0.15f, 0.3f), new Color(0.35f, 0.25f, 0.15f)),
            _ => (Color.FromHsv(variation, 0.45f, 0.75f), new Color(0.85f, 0.68f + variation * 0.12f, 0.55f))
        };
        var bodySize = _speciesId switch
        {
            "cow" => new Vector3(0.9f, 0.8f, 1.45f),
            "wolf" => new Vector3(0.65f, 0.55f, 1.35f),
            "ape" => new Vector3(0.75f, 0.9f, 0.65f),
            _ when _isCreature => new Vector3(0.75f, 0.65f, 1.25f),
            _ => new Vector3(0.65f, 0.9f, 0.45f)
        };
        var bodyHeight = bodySize.Y;
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = Mathf.Max(bodySize.X, bodySize.Z) * 0.5f, Height = bodyHeight + 0.2f },
            Position = new Vector3(0, bodyHeight * 0.5f + 0.1f, 0)
        });
        AddPart(new BoxMesh { Size = bodySize }, new Vector3(0, bodyHeight * 0.5f + 0.1f, 0), bodyColor);

        if (_isCreature && _speciesId != "ape")
        {
            BuildQuadruped(bodySize, bodyColor, accentColor);
        }
        else
        {
            BuildUpright(bodyColor, accentColor, bodySize);
        }
    }

    private void BuildQuadruped(Vector3 bodySize, Color bodyColor, Color accentColor)
    {
        var headY = bodySize.Y + 0.35f;
        var headZ = -bodySize.Z * 0.5f - 0.15f;
        AddPart(new BoxMesh { Size = new Vector3(0.4f, 0.4f, 0.45f) }, new Vector3(0, headY, headZ), bodyColor);
        AddPart(new BoxMesh { Size = new Vector3(0.22f, 0.18f, 0.22f) }, new Vector3(0, headY - 0.08f, headZ - 0.3f), accentColor);
        AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.18f, 0.06f) }, new Vector3(-0.15f, headY + 0.28f, headZ + 0.05f), accentColor);
        AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.18f, 0.06f) }, new Vector3(0.15f, headY + 0.28f, headZ + 0.05f), accentColor);
        AddPart(new SphereMesh { Radius = 0.04f, Height = 0.08f }, new Vector3(-0.1f, headY + 0.07f, headZ - 0.2f), Colors.Black);
        AddPart(new SphereMesh { Radius = 0.04f, Height = 0.08f }, new Vector3(0.1f, headY + 0.07f, headZ - 0.2f), Colors.Black);
        if (_speciesId == "cow")
        {
            AddPart(new CapsuleMesh { Height = 0.3f, Radius = 0.03f }, new Vector3(-0.22f, headY + 0.25f, headZ), new Color(0.9f, 0.85f, 0.7f));
            AddPart(new CapsuleMesh { Height = 0.3f, Radius = 0.03f }, new Vector3(0.22f, headY + 0.25f, headZ), new Color(0.9f, 0.85f, 0.7f));
            AddPart(new BoxMesh { Size = new Vector3(0.5f, 0.3f, 0.5f) }, new Vector3(0.2f, bodySize.Y * 0.7f, 0.1f), accentColor);
        }
        else if (_speciesId == "wolf")
        {
            AddPart(new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.6f) }, new Vector3(0, bodySize.Y * 0.6f, bodySize.Z * 0.5f + 0.25f), accentColor, new Vector3(-30, 0, 0));
        }

        var legColor = accentColor;
        foreach (var x in new[] { -0.28f, 0.28f })
        {
            foreach (var z in new[] { -bodySize.Z * 0.32f, bodySize.Z * 0.32f })
            {
                AddPart(new BoxMesh { Size = new Vector3(0.14f, 0.4f, 0.14f) }, new Vector3(x, 0.2f, z), legColor);
            }
        }

        if (_speciesId != "wolf")
        {
            AddPart(new CapsuleMesh { Height = 0.7f, Radius = 0.06f }, new Vector3(0, 0.6f, bodySize.Z * 0.5f + 0.2f), accentColor, new Vector3(75, 0, 0));
        }
    }

    private void BuildUpright(Color bodyColor, Color accentColor, Vector3 bodySize)
    {
        var skin = _speciesId == "ape" ? accentColor : accentColor;
        var shoulderY = bodySize.Y * 0.85f + 0.1f;
        var headY = _speciesId == "ape" ? 1.25f : 1.5f;
        AddPart(new SphereMesh { Height = 0.45f, Radius = 0.22f }, new Vector3(0, headY, 0), skin);
        AddPart(new SphereMesh { Radius = 0.03f, Height = 0.06f }, new Vector3(-0.08f, headY + 0.03f, -0.19f), Colors.Black);
        AddPart(new SphereMesh { Radius = 0.03f, Height = 0.06f }, new Vector3(0.08f, headY + 0.03f, -0.19f), Colors.Black);
        var armColor = _speciesId == "ape" ? bodyColor : bodyColor.Darkened(0.15f);
        var armLength = _speciesId == "ape" ? 0.85f : 0.6f;
        foreach (var side in new[] { -1f, 1f })
        {
            AddPart(new CapsuleMesh { Height = armLength, Radius = 0.07f },
                new Vector3(side * (bodySize.X * 0.5f + 0.08f), shoulderY - armLength * 0.4f, 0), armColor);
            AddPart(new CapsuleMesh { Height = 0.65f, Radius = 0.1f },
                new Vector3(side * 0.18f, 0.33f, 0), _speciesId == "ape" ? bodyColor : new Color(0.25f, 0.28f, 0.4f));
        }

        if (_speciesId != "ape")
        {
            AddPart(new SphereMesh { Radius = 0.24f, Height = 0.2f }, new Vector3(0, headY + 0.16f, 0.02f),
                new Color(0.2f + (EntityId.Length % 4) * 0.12f, 0.14f, 0.1f));
        }
    }

    private void AddPart(Mesh mesh, Vector3 position, Color color, Vector3? rotationDegrees = null)
    {
        var part = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color }
        };
        if (rotationDegrees is { } rotation)
        {
            part.RotationDegrees = rotation;
        }

        AddChild(part);
    }
    public override void _PhysicsProcess(double delta) => _navigationAdapter?.RefreshPath();

    public void Bind(string entityId, float movementSpeed, bool isCreature = false, string? speciesId = null)
    {
        if (!float.IsFinite(movementSpeed) || movementSpeed <= 0) throw new ArgumentOutOfRangeException(nameof(movementSpeed));
        EntityId = entityId;
        _movementSpeed = movementSpeed;
        _isCreature = isCreature;
        _speciesId = speciesId ?? (isCreature ? "creature" : "human");
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
