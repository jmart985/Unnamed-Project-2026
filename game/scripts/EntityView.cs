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
    private Node3D? _rig;
    private ProceduralGait? _gait;
    private Vector3 _lastPosition;
    private float _smoothedSpeed;

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
        _rig = new Node3D { Name = "Rig" };
        AddChild(_rig);
        _gait = new ProceduralGait { Body = _rig, ReferenceSpeed = Mathf.Max(_movementSpeed, 0.5f) };

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

        if (_isCreature && _speciesId != "ape")
        {
            BuildQuadruped(bodyColor, accentColor);
        }
        else
        {
            BuildUpright(bodyColor, accentColor, variation);
        }
    }

    private void AddCollision(float totalHeight, float radius)
    {
        radius = Mathf.Min(radius, totalHeight * 0.5f);
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = radius, Height = totalHeight },
            Position = new Vector3(0, totalHeight * 0.5f, 0)
        });
    }

    private static Node3D AddJoint(Node3D parent, Vector3 position)
    {
        var joint = new Node3D { Position = position };
        parent.AddChild(joint);
        return joint;
    }

    private void AddLeg(Node3D parent, Vector3 hip, float length, float thickness, Color color, Color footColor,
        float phase, float swing, float knee)
    {
        var upper = AddJoint(parent, hip);
        AddPart(new BoxMesh { Size = new Vector3(thickness, length * 0.5f, thickness) }, new Vector3(0, -length * 0.25f, 0), color, parent: upper);
        var lower = AddJoint(upper, new Vector3(0, -length * 0.5f, 0));
        AddPart(new BoxMesh { Size = new Vector3(thickness * 0.8f, length * 0.5f, thickness * 0.8f) }, new Vector3(0, -length * 0.25f, 0), color, parent: lower);
        AddPart(new BoxMesh { Size = new Vector3(thickness * 1.1f, 0.06f, thickness * 1.7f) }, new Vector3(0, -length * 0.5f + 0.03f, -thickness * 0.35f), footColor, parent: lower);
        _gait!.Limbs.Add(new ProceduralGait.Limb { Pivot = upper, Phase = phase, Amplitude = swing });
        _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = lower, Phase = phase + Mathf.Pi * 0.5f, Amplitude = -knee, Rectify = true });
    }

    private void AddArm(Node3D parent, Vector3 shoulder, float length, float thickness, Color sleeve, Color hand,
        float phase, float swing)
    {
        var upper = AddJoint(parent, shoulder);
        AddPart(new SphereMesh { Radius = thickness * 0.75f, Height = thickness * 1.5f }, Vector3.Zero, sleeve, parent: upper);
        AddPart(new BoxMesh { Size = new Vector3(thickness, length * 0.5f, thickness) }, new Vector3(0, -length * 0.25f, 0), sleeve, parent: upper);
        var lower = AddJoint(upper, new Vector3(0, -length * 0.5f, 0));
        AddPart(new BoxMesh { Size = new Vector3(thickness * 0.85f, length * 0.5f, thickness * 0.85f) }, new Vector3(0, -length * 0.25f, 0), hand, parent: lower);
        AddPart(new SphereMesh { Radius = thickness * 0.5f, Height = thickness }, new Vector3(0, -length * 0.5f, 0), hand, parent: lower);
        _gait!.Limbs.Add(new ProceduralGait.Limb { Pivot = upper, Phase = phase, Amplitude = swing, IdleAmplitude = 0.03f, IdleFrequency = 1.1f });
        _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = lower, Phase = phase + Mathf.Pi * 0.5f, Amplitude = 0.25f, Bias = 0.3f });
    }

    private void BuildQuadruped(Color bodyColor, Color accentColor)
    {
        var bodySize = _speciesId switch
        {
            "cow" => new Vector3(0.9f, 0.8f, 1.45f),
            "wolf" => new Vector3(0.6f, 0.5f, 1.2f),
            _ => new Vector3(0.75f, 0.65f, 1.25f)
        };
        var legLength = bodySize.Y * (_speciesId == "wolf" ? 0.85f : 0.75f);
        var bodyY = legLength + bodySize.Y * 0.5f - 0.05f;
        AddCollision(legLength + bodySize.Y, Mathf.Max(bodySize.X, bodySize.Z) * 0.4f);
        _gait!.BobHeight = 0.03f;
        _gait.RadiansPerMeter = 3.4f;

        // Barrel chest and slimmer hindquarters read as one animal rather than a box.
        AddPart(new BoxMesh { Size = bodySize }, new Vector3(0, bodyY, 0), bodyColor);
        AddPart(new BoxMesh { Size = new Vector3(bodySize.X * 0.9f, bodySize.Y * 0.85f, bodySize.Z * 0.4f) },
            new Vector3(0, bodyY - 0.02f, bodySize.Z * 0.38f), bodyColor.Darkened(0.08f));
        if (_speciesId == "cow")
        {
            AddPart(new BoxMesh { Size = new Vector3(0.5f, 0.3f, 0.5f) }, new Vector3(0.2f, bodyY + bodySize.Y * 0.25f, 0.1f), accentColor);
            AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.2f, 0.25f) }, new Vector3(0, legLength - 0.02f, bodySize.Z * 0.3f), new Color(0.95f, 0.7f, 0.7f));
        }

        var head = AddJoint(_rig, new Vector3(0, bodyY + bodySize.Y * 0.3f, -bodySize.Z * 0.5f));
        AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.34f, 0.4f) }, new Vector3(0, 0.02f, -0.12f), bodyColor, new Vector3(-25, 0, 0), head);
        AddPart(new BoxMesh { Size = new Vector3(0.38f, 0.38f, 0.42f) }, new Vector3(0, 0.1f, -0.38f), bodyColor, parent: head);
        AddPart(new BoxMesh { Size = new Vector3(0.22f, 0.18f, 0.24f) }, new Vector3(0, 0.02f, -0.66f), accentColor, parent: head);
        AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.18f, 0.06f) }, new Vector3(-0.17f, 0.37f, -0.3f), accentColor, new Vector3(0, 0, 20), head);
        AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.18f, 0.06f) }, new Vector3(0.17f, 0.37f, -0.3f), accentColor, new Vector3(0, 0, -20), head);
        foreach (var x in new[] { -0.1f, 0.1f })
        {
            AddPart(new SphereMesh { Radius = 0.04f, Height = 0.08f }, new Vector3(x, 0.17f, -0.58f), Colors.Black, parent: head);
        }

        if (_speciesId == "cow")
        {
            foreach (var x in new[] { -0.2f, 0.2f })
            {
                AddPart(new CapsuleMesh { Height = 0.3f, Radius = 0.03f }, new Vector3(x, 0.4f, -0.36f), new Color(0.9f, 0.85f, 0.7f), new Vector3(0, 0, x < 0 ? 35 : -35), head);
            }
        }

        _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = head, Phase = 0.5f, Amplitude = 0.07f, IdleAmplitude = 0.12f, IdleFrequency = 0.7f });

        var tail = AddJoint(_rig, new Vector3(0, bodyY + bodySize.Y * 0.2f, bodySize.Z * 0.5f));
        var tailLength = _speciesId == "wolf" ? 0.6f : 0.5f;
        AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.1f, tailLength) }, new Vector3(0, -0.07f, tailLength * 0.5f), accentColor, new Vector3(25, 0, 0), tail);
        _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = tail, Axis = Vector3.Up, Phase = 0, Amplitude = 0.35f, IdleAmplitude = 0.3f, IdleFrequency = 1.3f });

        var frontZ = -bodySize.Z * 0.32f;
        var rearZ = bodySize.Z * 0.32f;
        var legX = bodySize.X * 0.32f;
        var thickness = _speciesId == "wolf" ? 0.12f : 0.16f;
        AddLeg(_rig, new Vector3(-legX, legLength, frontZ), legLength, thickness, bodyColor, accentColor, 0, 0.55f, 0.6f);
        AddLeg(_rig, new Vector3(legX, legLength, rearZ), legLength, thickness, bodyColor, accentColor, 0, 0.55f, 0.6f);
        AddLeg(_rig, new Vector3(legX, legLength, frontZ), legLength, thickness, bodyColor, accentColor, Mathf.Pi, 0.55f, 0.6f);
        AddLeg(_rig, new Vector3(-legX, legLength, rearZ), legLength, thickness, bodyColor, accentColor, Mathf.Pi, 0.55f, 0.6f);
    }

    private void BuildUpright(Color shirt, Color skin, float variation)
    {
        var ape = _speciesId == "ape";
        var legLength = ape ? 0.55f : 0.9f;
        var shoulderWidth = ape ? 0.85f : 0.5f;
        var chest = ape ? new Vector3(0.75f, 0.6f, 0.45f) : new Vector3(0.5f, 0.55f, 0.28f);
        var armLength = ape ? 0.95f : 0.68f;
        var lean = ape ? -0.5f : 0f;
        var pants = ape ? shirt : new Color(0.22f, 0.25f, 0.36f);
        var armColor = ape ? shirt : shirt.Darkened(0.1f);
        var handColor = ape ? skin : skin;
        var totalHeight = legLength + chest.Y + 0.35f;
        AddCollision(totalHeight, 0.32f);
        _gait!.BobHeight = ape ? 0.05f : 0.035f;

        AddPart(new BoxMesh { Size = new Vector3(chest.X * 0.85f, 0.2f, chest.Z * 0.9f) }, new Vector3(0, legLength, 0), pants);
        var torso = AddJoint(_rig, new Vector3(0, legLength, 0));
        torso.Rotation = new Vector3(lean, 0, 0);
        AddPart(new BoxMesh { Size = chest }, new Vector3(0, chest.Y * 0.5f + 0.05f, 0), shirt, parent: torso);
        AddPart(new BoxMesh { Size = new Vector3(chest.X * 0.7f, 0.1f, chest.Z * 0.8f) }, new Vector3(0, 0.02f, 0), shirt.Darkened(0.25f), parent: torso);
        AddPart(new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.08f, Height = 0.12f }, new Vector3(0, chest.Y + 0.1f, 0), skin, parent: torso);

        var headPivot = AddJoint(torso, new Vector3(0, chest.Y + 0.15f, ape ? -0.08f : 0));
        AddPart(new SphereMesh { Height = 0.46f, Radius = 0.22f }, new Vector3(0, 0.15f, 0), skin, parent: headPivot);
        AddPart(new SphereMesh { Radius = 0.03f, Height = 0.06f }, new Vector3(-0.08f, 0.2f, -0.19f), Colors.Black, parent: headPivot);
        AddPart(new SphereMesh { Radius = 0.03f, Height = 0.06f }, new Vector3(0.08f, 0.2f, -0.19f), Colors.Black, parent: headPivot);
        AddPart(new BoxMesh { Size = new Vector3(0.06f, 0.08f, 0.08f) }, new Vector3(0, 0.12f, -0.22f), skin.Darkened(0.08f), parent: headPivot);
        foreach (var x in new[] { -0.22f, 0.22f })
        {
            AddPart(new SphereMesh { Radius = 0.05f, Height = 0.1f }, new Vector3(x, 0.15f, 0), skin, parent: headPivot);
        }

        if (ape)
        {
            AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.14f, 0.12f) }, new Vector3(0, 0.05f, -0.17f), skin.Lightened(0.1f), parent: headPivot);
            AddPart(new BoxMesh { Size = new Vector3(0.42f, 0.08f, 0.1f) }, new Vector3(0, 0.3f, -0.15f), shirt.Darkened(0.1f), parent: headPivot);
        }
        else
        {
            AddPart(new SphereMesh { Radius = 0.235f, Height = 0.24f }, new Vector3(0, 0.27f, 0.02f),
                new Color(0.12f + variation * 0.4f, 0.1f + variation * 0.15f, 0.08f), parent: headPivot);
        }

        _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = headPivot, Axis = Vector3.Up, Amplitude = 0.05f, IdleAmplitude = 0.2f, IdleFrequency = 0.6f });

        var swing = ape ? 0.55f : 0.6f;
        var shoulderY = chest.Y + 0.02f;
        AddArm(torso, new Vector3(-(shoulderWidth * 0.5f + 0.06f), shoulderY, 0), armLength, ape ? 0.17f : 0.11f, armColor, handColor, Mathf.Pi, swing);
        AddArm(torso, new Vector3(shoulderWidth * 0.5f + 0.06f, shoulderY, 0), armLength, ape ? 0.17f : 0.11f, armColor, handColor, 0, swing);

        var hipX = chest.X * 0.28f;
        var thickness = ape ? 0.17f : 0.15f;
        AddLeg(_rig, new Vector3(-hipX, legLength, 0), legLength, thickness, pants, new Color(0.15f, 0.12f, 0.1f), 0, 0.6f, 0.9f);
        AddLeg(_rig, new Vector3(hipX, legLength, 0), legLength, thickness, pants, new Color(0.15f, 0.12f, 0.1f), Mathf.Pi, 0.6f, 0.9f);
    }

    private void AddPart(Mesh mesh, Vector3 position, Color color, Vector3? rotationDegrees = null, Node3D? parent = null)
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

        (parent ?? _rig!).AddChild(part);
    }

    public override void _Process(double delta)
    {
        if (_gait is null || _rig is null || delta <= 0) return;
        var position = GlobalPosition;
        var step = new Vector3(position.X - _lastPosition.X, 0, position.Z - _lastPosition.Z);
        _lastPosition = position;
        // Large jumps are loads, resets, or respawns, not walking.
        var speed = step.Length() > 3f ? 0f : step.Length() / (float)delta;
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, 1 - Mathf.Exp(-12 * (float)delta));
        if (_smoothedSpeed > 0.15f && step.LengthSquared() > 1e-8f)
        {
            var targetYaw = Mathf.Atan2(-step.X, -step.Z);
            _rig.Rotation = new Vector3(0, Mathf.LerpAngle(_rig.Rotation.Y, targetYaw, 1 - Mathf.Exp(-10 * (float)delta)), 0);
        }

        _gait.Advance((float)delta, _smoothedSpeed);
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
