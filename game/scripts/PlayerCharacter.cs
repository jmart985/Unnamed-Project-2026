using Godot;
using Simulation;

namespace UnnamedProject2026;

/// A reusable CharacterBody3D player for the playable prototype.
/// Keyboard bindings are installed here as defaults and remain configurable in project settings.
public partial class PlayerCharacter : CharacterBody3D
{
    [Export] public float MovementSpeed { get; set; } = 5.0f;
    [Export] public float JumpVelocity { get; set; } = 5.5f;
    [Export] public float Gravity { get; set; } = 9.8f;
    [Export] public float Acceleration { get; set; } = 20.0f;
    [Export] public float Deceleration { get; set; } = 24.0f;
    [Export] public float FallRespawnHeight { get; set; } = -5.0f;
    [Export] public float RespawnLift { get; set; } = 0.2f;
    [Export] public float AttackDuration { get; set; } = 0.22f;
    [Export] public float AttackCooldown { get; set; } = 0.35f;
    [Export] public float MouseSensitivity { get; set; } = 0.01f;
    [Export] public bool LeftShieldEquipped { get; set; }
    [Export] public bool RightShieldEquipped { get; set; }

    private Vector3 _lastGroundedPosition;
    private MeshInstance3D _leftArm = null!;
    private MeshInstance3D _rightArm = null!;
    private double _attackCooldownRemaining;
    private float _restingArmRotation;
    private float _restingLeftArmRotation;
    private readonly List<MeshInstance3D> _chestPlaceholders = [];
    private readonly ProceduralGait _gait = new() { ReferenceSpeed = 5.0f, BobHeight = 0.0f };

    public bool IsAttacking { get; private set; }
    public string EntityId { get; private set; } = string.Empty;

    public override void _Ready()
    {
        PrototypeInputMap.EnsureDefaults();
        _lastGroundedPosition = GlobalPosition;
        _leftArm = GetNode<MeshInstance3D>("LeftShoulder/LeftArm");
        _rightArm = GetNode<MeshInstance3D>("RightShoulder/RightArm");
        _restingLeftArmRotation = _leftArm.Rotation.X;
        _restingArmRotation = _rightArm.Rotation.X;
        AddChestPlaceholders();
        BuildGait();
    }

    // Legs are re-parented under hip pivots and arms swing from their shoulder pivots so the walk cycle
    // does not disturb the attack swing, which rotates the arm meshes directly.
    private void BuildGait()
    {
        _gait.ReferenceSpeed = MovementSpeed;
        foreach (var (name, phase) in new[] { ("LeftLeg", 0f), ("RightLeg", Mathf.Pi) })
        {
            var leg = GetNode<MeshInstance3D>(name);
            var hip = new Node3D { Name = name + "Hip", Position = leg.Position + new Vector3(0, 0.4f, 0) };
            AddChild(hip);
            RemoveChild(leg);
            hip.AddChild(leg);
            leg.Position = new Vector3(0, -0.4f, 0);
            _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = hip, Phase = phase, Amplitude = 0.6f });
        }

        _gait.Limbs.Add(new ProceduralGait.Limb
        {
            Pivot = GetNode<Node3D>("LeftShoulder"), Phase = Mathf.Pi, Amplitude = 0.5f, IdleAmplitude = 0.03f, IdleFrequency = 1.1f
        });
        _gait.Limbs.Add(new ProceduralGait.Limb
        {
            Pivot = GetNode<Node3D>("RightShoulder"), Phase = 0, Amplitude = 0.5f, IdleAmplitude = 0.03f, IdleFrequency = 1.1f
        });
        _gait.Limbs.Add(new ProceduralGait.Limb
        {
            Pivot = GetNode<Node3D>("Head"), Axis = Vector3.Up, Amplitude = 0.04f, IdleAmplitude = 0.15f, IdleFrequency = 0.6f
        });
    }

    public void BindPlayer(string entityId, PlayerCharacterProfile profile, float movementSpeed)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new ArgumentException("A player entity ID is required.", nameof(entityId));
        }

        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.IsValid)
        {
            throw new ArgumentException("The player profile is invalid.", nameof(profile));
        }

        if (!float.IsFinite(movementSpeed) || movementSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(movementSpeed));
        }

        EntityId = entityId;
        MovementSpeed = movementSpeed;
        _lastGroundedPosition = GlobalPosition;
        _gait.ReferenceSpeed = movementSpeed;
        var heightScale = profile.Height switch
        {
            PlayerHeight.Small => 0.88f,
            PlayerHeight.Medium => 1.0f,
            PlayerHeight.Tall => 1.12f,
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        var (widthScale, depthScale) = profile.BodyType switch
        {
            PlayerBodyType.Lean => (0.84f, 0.86f),
            PlayerBodyType.Athletic => (0.96f, 0.94f),
            PlayerBodyType.Average => (1.0f, 1.0f),
            PlayerBodyType.Stocky => (1.12f, 1.08f),
            PlayerBodyType.Thick => (1.24f, 1.18f),
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        Scale = new Vector3(widthScale, heightScale, depthScale);
        foreach (var placeholder in _chestPlaceholders)
        {
            placeholder.Visible = profile.Sex == PlayerSex.Female;
        }
    }

    private void AddChestPlaceholders()
    {
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.82f, 0.58f, 0.42f) };
        foreach (var x in new[] { -0.17f, 0.17f })
        {
            var placeholder = new MeshInstance3D
            {
                Name = x < 0 ? "ChestLeft" : "ChestRight",
                Mesh = new SphereMesh { Radius = 0.13f, Height = 0.22f },
                Position = new Vector3(x, 1.12f, -0.26f),
                MaterialOverride = material,
                Visible = false
            };
            AddChild(placeholder);
            _chestPlaceholders.Add(placeholder);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            RotateY(-motion.Relative.X * MouseSensitivity);
            return;
        }

        if (@event is InputEventKey key && (!key.Pressed || key.Echo))
        {
            return;
        }

        if (@event is InputEventMouseButton mouse && !mouse.Pressed)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                StartAttackSwing(_leftArm, _restingLeftArmRotation, LeftShieldEquipped);
            }
            else if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                StartAttackSwing(_rightArm, _restingArmRotation, RightShieldEquipped);
            }

            return;
        }

        if (Input.IsActionPressed(ControllerActions.AttackLeft))
        {
            StartAttackSwing(_leftArm, _restingLeftArmRotation, LeftShieldEquipped);
        }
        else if (Input.IsActionPressed(ControllerActions.AttackRight))
        {
            StartAttackSwing(_rightArm, _restingArmRotation, RightShieldEquipped);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var physicsDelta = (float)delta;
        _attackCooldownRemaining = Mathf.Max(0.0, _attackCooldownRemaining - delta);
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * physicsDelta, Velocity.Z);
        }

        if (Input.IsActionJustPressed(ControllerActions.Jump) && IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
        }

        var forward = Input.GetAxis(ControllerActions.MoveForward, ControllerActions.MoveBackward);
        var strafe = Input.GetAxis(ControllerActions.MoveLeft, ControllerActions.MoveRight);
        var localDirection = new Vector3(strafe, 0.0f, forward);
        if (localDirection.LengthSquared() > 1.0f)
        {
            localDirection = localDirection.Normalized();
        }

        // Convert local input into world space so movement follows the player's facing.
        var direction = GlobalTransform.Basis * localDirection;
        direction.Y = 0.0f;
        var target = direction * MovementSpeed;
        var rate = direction == Vector3.Zero ? Deceleration : Acceleration;
        Velocity = new Vector3(
            Mathf.MoveToward(Velocity.X, target.X, rate * physicsDelta),
            Velocity.Y,
            Mathf.MoveToward(Velocity.Z, target.Z, rate * physicsDelta));

        MoveAndSlide();
        _gait.Advance(physicsDelta, IsOnFloor() ? new Vector2(Velocity.X, Velocity.Z).Length() : 0f);

        if (IsOnFloor())
        {
            _lastGroundedPosition = GlobalPosition;
        }
        else if (GlobalPosition.Y < FallRespawnHeight)
        {
            GlobalPosition = _lastGroundedPosition + Vector3.Up * RespawnLift;
            Velocity = Vector3.Zero;
        }
    }

    private void StartAttackSwing(MeshInstance3D arm, float restingRotation, bool shieldEquipped)
    {
        if (shieldEquipped || IsAttacking || _attackCooldownRemaining > 0.0)
        {
            return;
        }

        IsAttacking = true;
        _attackCooldownRemaining = AttackCooldown;
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(arm, "rotation:x", restingRotation - Mathf.Pi * 0.75f, AttackDuration * 0.45f);
        tween.TweenProperty(arm, "rotation:x", restingRotation, AttackDuration * 0.55f);
        tween.Finished += () => IsAttacking = false;
    }
}
