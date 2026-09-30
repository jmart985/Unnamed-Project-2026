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

    private Vector3 _lastGroundedPosition;

    public override void _Ready()
    {
        EnsureDefaultInputActions();
        _lastGroundedPosition = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        var physicsDelta = (float)delta;
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * physicsDelta, Velocity.Z);
        }

        if (Input.IsActionJustPressed("jump") && IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
        }

        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
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

        // Keep a safe checkpoint so falls and map clipping return to the nearest known floor.
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

    private static void EnsureDefaultInputActions()
    {
        AddKeyboardAction("move_left", Key.A);
        AddKeyboardAction("move_right", Key.D);
        AddKeyboardAction("move_forward", Key.W);
        AddKeyboardAction("move_backward", Key.S);
        AddKeyboardAction("jump", Key.Space);
    }

    private static void AddKeyboardAction(StringName actionName, Key physicalKey)
    {
        if (InputMap.HasAction(actionName))
        {
            return;
        }

        InputMap.AddAction(actionName);
        InputMap.ActionAddEvent(actionName, new InputEventKey { PhysicalKeycode = physicalKey });
    }
}
