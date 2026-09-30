using Godot;

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

    private Vector3 _lastGroundedPosition;
    private MeshInstance3D _rightArm = null!;
    private double _attackCooldownRemaining;
    private float _restingArmRotation;

    public bool IsAttacking { get; private set; }

    public override void _Ready()
    {
        PrototypeInputMap.EnsureDefaults();
        _lastGroundedPosition = GlobalPosition;
        _rightArm = GetNode<MeshInstance3D>("RightArm");
        _restingArmRotation = _rightArm.Rotation.X;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && (!key.Pressed || key.Echo))
        {
            return;
        }

        if (@event is InputEventMouseButton mouse && (!mouse.Pressed || mouse.ButtonIndex != MouseButton.Left))
        {
            return;
        }

        if (Input.IsActionPressed(ControllerActions.Attack))
        {
            StartAttackSwing();
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

        var input = Input.GetVector(ControllerActions.MoveLeft, ControllerActions.MoveRight, ControllerActions.MoveForward, ControllerActions.MoveBackward);
        var direction = new Vector3(input.X, 0.0f, input.Y);
        if (direction.LengthSquared() > 1.0f)
        {
            direction = direction.Normalized();
        }

        var target = direction * MovementSpeed;
        var rate = direction == Vector3.Zero ? Deceleration : Acceleration;
        Velocity = new Vector3(
            Mathf.MoveToward(Velocity.X, target.X, rate * physicsDelta),
            Velocity.Y,
            Mathf.MoveToward(Velocity.Z, target.Z, rate * physicsDelta));

        MoveAndSlide();

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

    private void StartAttackSwing()
    {
        if (IsAttacking || _attackCooldownRemaining > 0.0)
        {
            return;
        }

        IsAttacking = true;
        _attackCooldownRemaining = AttackCooldown;
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_rightArm, "rotation:x", _restingArmRotation - Mathf.Pi * 0.75f, AttackDuration * 0.45f);
        tween.TweenProperty(_rightArm, "rotation:x", _restingArmRotation, AttackDuration * 0.55f);
        tween.Finished += () => IsAttacking = false;
    }
}
