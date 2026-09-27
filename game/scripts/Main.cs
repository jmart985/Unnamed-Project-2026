using Godot;

namespace UnnamedProject2026;

public partial class Main : Node3D
{
    public override void _Ready()
    {
        var map = GetNode<MeshInstance3D>("Map");
        map.Mesh = new PlaneMesh { Size = new Vector2(20, 20) };
    }
}
