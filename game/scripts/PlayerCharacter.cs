using Godot;
using Simulation;

namespace UnnamedProject2026;

/// A reusable CharacterBody3D player for the playable prototype.
/// Keyboard bindings are installed here as defaults and remain configurable in project settings.
public partial class PlayerCharacter : CharacterBody3D
{
	public bool GameplayInputEnabled { get; private set; } = true;

	public void SetGameplayInputEnabled(bool enabled)
	{
		GameplayInputEnabled = enabled;
		if (!enabled)
		{
			Velocity = new Vector3(0, Velocity.Y, 0);
			_swing = AttackSwing.ForTotalDuration(AttackDuration);
		}
	}
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
	[Export] public float CameraZoomDistance { get; set; } = 5.5f;
	[Export] public float CameraFirstPersonDistance { get; set; } = 0.12f;
	[Export] public float CameraZoomStep { get; set; } = 0.75f;
	[Export] public float CameraZoomSmoothing { get; set; } = 12.0f;
	[Export] public float CameraCollisionMargin { get; set; } = 0.2f;
	[Export] public float CameraPitchOffset { get; set; } = -0.2617994f;
	[Export] public bool LeftShieldEquipped { get; set; }
	[Export] public bool RightShieldEquipped { get; set; }

	private Vector3 _lastGroundedPosition;
	private double _attackCooldownRemaining;
	private PlayerBodyRig _body = null!;
	private Node3D _axeView = null!;
	private PlayerCharacterProfile _profile = PlayerCharacterProfile.Create("Player");
	private readonly ProceduralGait _gait = new() { ReferenceSpeed = 5.0f, BobHeight = 0.03f };
	private AttackSwing _swing = new();
	private bool _swingLeft;
	private float _headPitch;
	private float _headPitchTarget;
	private float _cameraZoomDistance;

	public bool IsAttacking => _swing.IsActive;
	public string EntityId { get; private set; } = string.Empty;
	public string AimedBodyPart => CombatAim.PartForPitch(_headPitch, _swingLeft);

	/// Raised once per swing at the moment of impact: the swinging side and the aimed body part.
	/// Main resolves this event through the World-owned melee/social combat boundary.
	public event Action<bool, string>? StrikeReleased;
	public event Action? AttackStarted;

	public override void _Ready()
	{
		PrototypeInputMap.EnsureDefaults();
		// Layer 1 is the player; mask layer 2 for soft physical resistance against
		// entities. Entity views intentionally do not collide with each other.
		CollisionLayer = 1;
		CollisionMask = 3;
		_lastGroundedPosition = GlobalPosition;
		RebuildBody(_profile);
	}

	// The rig is rebuilt from the profile so character creation changes proportions, not a whole-node scale
	// (which would also distort the collision capsule and camera).
	private void RebuildBody(PlayerCharacterProfile profile)
	{
		_profile = profile;
		if (_body is not null)
		{
			_body.Root.QueueFree();
		}

		var proportions = PlayerBodyProportions.From(profile);
		_body = new PlayerBodyRig(proportions);
		AddChild(_body.Root);
		BuildToolVisual();

		var collision = GetNode<CollisionShape3D>("CollisionShape3D");
		var radius = Mathf.Min(0.4f * proportions.TorsoWidth, proportions.Height * 0.25f);
		collision.Shape = new CapsuleShape3D { Radius = radius, Height = proportions.Height };
		collision.Position = new Vector3(0, proportions.Height * 0.5f, 0);
		var camera = GetNode<Camera3D>("Camera3D");
		_cameraZoomDistance = CameraZoomDistance;
		camera.Position = new Vector3(0, proportions.Height * 1.45f, _cameraZoomDistance);
		camera.Rotation = new Vector3(CameraPitchOffset, 0, 0);

		BuildGait();
	}

	private void BuildToolVisual()
	{
		_axeView = new Node3D { Name = "EquippedAxe", Visible = false, Position = new Vector3(0, -0.62f, 0) };
		_body.RightElbow.AddChild(_axeView);
		var handleMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.16f, 0.06f) };
		var bladeMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.65f, 0.68f, 0.72f), Metallic = 0.35f, Roughness = 0.35f };
		_axeView.AddChild(new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 0.72f },
			RotationDegrees = new Vector3(0, 0, 90), MaterialOverride = handleMaterial
		});
		_axeView.AddChild(new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.28f, 0.07f) },
			Position = new Vector3(0.34f, 0, 0), MaterialOverride = bladeMaterial
		});
	}

	public void SetAxeEquipped(bool equipped)
	{
		if (_axeView is not null) _axeView.Visible = equipped;
	}

	// Walk drives hips, knees, shoulders, elbows, head and torso; an attack layer overrides the swinging arm
	// and adds a torso twist after the gait step, so locomotion and combat share joints without fighting.
	private void BuildGait()
	{
		_gait.Limbs.Clear();
		_gait.ReferenceSpeed = MovementSpeed;
		_gait.Body = _body.Root;
		var leg = new[] { (_body.LeftHip, _body.LeftKnee, 0f), (_body.RightHip, _body.RightKnee, Mathf.Pi) };
		foreach (var (hip, knee, phase) in leg)
		{
			_gait.Limbs.Add(new ProceduralGait.Limb { Pivot = hip, Phase = phase, Amplitude = 0.6f });
			_gait.Limbs.Add(new ProceduralGait.Limb { Pivot = knee, Phase = phase + Mathf.Pi * 0.5f, Amplitude = -0.9f, Rectify = true });
		}

		var arm = new[] { (_body.LeftShoulder, _body.LeftElbow, Mathf.Pi), (_body.RightShoulder, _body.RightElbow, 0f) };
		foreach (var (shoulder, elbow, phase) in arm)
		{
			_gait.Limbs.Add(new ProceduralGait.Limb { Pivot = shoulder, Phase = phase, Amplitude = 0.5f, IdleAmplitude = 0.03f, IdleFrequency = 1.1f });
			_gait.Limbs.Add(new ProceduralGait.Limb { Pivot = elbow, Phase = phase, Amplitude = 0.25f, Bias = 0.2f, Rectify = true });
		}

		_gait.Limbs.Add(new ProceduralGait.Limb { Pivot = _body.Torso, Axis = Vector3.Up, Phase = 0, Amplitude = 0.08f, IdleAmplitude = 0.02f, IdleFrequency = 0.5f });
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
		RebuildBody(profile);
	}
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!GameplayInputEnabled) return;
		if (@event is InputEventMouseMotion motion)
		{
			RotateY(-motion.Relative.X * MouseSensitivity);
			// Allow a wider upward look for close overhead targets while keeping a
			// comfortable downward limit and preventing a full camera inversion.
			_headPitchTarget = Mathf.Clamp(_headPitchTarget - motion.Relative.Y * MouseSensitivity, -0.6f, 1.0f);
			return;
		}
		if (@event is InputEventMouseButton wheel && wheel.Pressed
			&& wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
		{
			_cameraZoomDistance = Mathf.Clamp(_cameraZoomDistance +
				(wheel.ButtonIndex == MouseButton.WheelUp ? -CameraZoomStep : CameraZoomStep),
				CameraFirstPersonDistance, CameraZoomDistance);
			GetViewport().SetInputAsHandled();
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

		if (@event.IsActionPressed(ControllerActions.AttackLeft))
		{
			StartAttackSwing(true, LeftShieldEquipped);
		}
		else if (@event.IsActionPressed(ControllerActions.AttackRight))
		{
			StartAttackSwing(false, RightShieldEquipped);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		var physicsDelta = (float)delta;
		if (GameplayInputEnabled)
		{
			var stickLook = new Vector2(Input.GetJoyAxis(0, JoyAxis.RightX), Input.GetJoyAxis(0, JoyAxis.RightY));
			if (stickLook.LengthSquared() > 0.01f)
			{
				RotateY(-stickLook.X * MouseSensitivity * 60f * physicsDelta);
				_headPitchTarget = Mathf.Clamp(_headPitchTarget - stickLook.Y * MouseSensitivity * 60f * physicsDelta, -0.6f, 1.0f);
			}
		}
		_attackCooldownRemaining = Mathf.Max(0.0, _attackCooldownRemaining - delta);
		if (!IsOnFloor())
		{
			Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * physicsDelta, Velocity.Z);
		}

		if (GameplayInputEnabled && Input.IsActionJustPressed(ControllerActions.Jump) && IsOnFloor())
		{
			Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
		}

		var forward = GameplayInputEnabled ? Input.GetAxis(ControllerActions.MoveForward, ControllerActions.MoveBackward) : 0;
		var strafe = GameplayInputEnabled ? Input.GetAxis(ControllerActions.MoveLeft, ControllerActions.MoveRight) : 0;
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
        if (_swing.Advance(physicsDelta))
        {
            StrikeReleased?.Invoke(_swingLeft, CombatAim.PartForPitch(_headPitch, _swingLeft));
        }

		ApplyAttackPose();
		UpdateCameraPosition(physicsDelta);

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

	/// <summary>
	/// Keeps the third-person camera from sitting inside a workbench, wall, or crowd.
	/// The camera remains on the player's facing axis and eases back out after an
	/// obstruction clears, so close-range interactions stay readable.
	/// </summary>
	private void UpdateCameraPosition(float delta)
	{
		var camera = GetNode<Camera3D>("Camera3D");
		var proportions = PlayerBodyProportions.From(_profile);
		var pivotLocal = new Vector3(0, proportions.Height * 1.28f, 0);
		var desiredLocal = new Vector3(0, proportions.Height * 1.45f, _cameraZoomDistance);
		var pivot = GlobalTransform * pivotLocal;
		var desired = GlobalTransform * desiredLocal;
		var targetDistance = _cameraZoomDistance;
		if (GetWorld3D() is { } world)
		{
			var query = PhysicsRayQueryParameters3D.Create(pivot, desired, collisionMask: 2);
			query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
			var hit = world.DirectSpaceState.IntersectRay(query);
			if (hit.Count > 0 && hit.TryGetValue("position", out var hitPosition))
			{
				var point = (Vector3)hitPosition;
				targetDistance = Mathf.Clamp(pivot.DistanceTo(point) - CameraCollisionMargin,
					CameraFirstPersonDistance, _cameraZoomDistance);
			}
		}

		// Pull in immediately when an obstruction appears; ease out when clear.
		var currentDistance = camera.Position.Z;
		var nextDistance = targetDistance < currentDistance
			? targetDistance
			: Mathf.MoveToward(currentDistance, targetDistance, CameraZoomSmoothing * delta);
		camera.Position = new Vector3(0, desiredLocal.Y, nextDistance);
	}

    private void StartAttackSwing(bool left, bool shieldEquipped)
    {
        if (shieldEquipped || _swing.IsActive || _attackCooldownRemaining > 0.0)
        {
            return;
        }

        _swingLeft = left;
        _swing = AttackSwing.ForTotalDuration(AttackDuration);
        _swing.Begin();
        AttackStarted?.Invoke();
        _attackCooldownRemaining = AttackCooldown;
    }

    // Layers the attack pose over the walk cycle: wind-up raises the arm and bends the elbow, the strike
    // chops down through the aim, recovery returns. The torso twists into the swing.
    private void ApplyAttackPose()
    {
        var pose = _swing.Pose;
        var shoulder = _swingLeft ? _body.LeftShoulder : _body.RightShoulder;
        var elbow = _swingLeft ? _body.LeftElbow : _body.RightElbow;
        if (_swing.IsActive)
        {
            var raise = pose >= 0 ? 2.2f * pose : 0.9f * pose;
            shoulder.Rotation = new Vector3(raise, 0, 0);
            elbow.Rotation = new Vector3(0.3f + 0.9f * Mathf.Max(0, pose), 0, 0);
            var twist = (_swingLeft ? -1f : 1f) * pose * 0.3f;
            _body.Torso.Rotation = new Vector3(0, twist, 0);
        }

        // Head follows aim pitch; sits on top of the idle sway applied by the gait.
		_headPitch = Mathf.Lerp(_headPitch, _headPitchTarget, 0.2f);
		_body.Head.Rotation = new Vector3(_headPitch, _body.Head.Rotation.Y, 0);
		GetNode<Camera3D>("Camera3D").Rotation = new Vector3(CameraPitchOffset + _headPitch, 0, 0);
    }
}
