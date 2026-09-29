using Godot;

namespace UnnamedProject2026;

/// A simple first-person-ready player body for the playable prototype.
/// Input actions are defined in project.godot so keyboard bindings remain configurable.
public partial class PlayerCharacter : CharacterBody3D
{
    [Export] public float MovementSpeed { get; set; } = 5.0f;
    [Export] public float JumpVelocity { get; set; } = 5.5f;
    [Export] public float Gravity { get; set; } = 9.8f;
    [Export] public float Acceleration { get; set; } = 20.0f;
    [Export] public float Deceleration { get; set; } = 24.0f;

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
    }
}
