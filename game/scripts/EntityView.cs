using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class EntityView : Node3D
{
    public string EntityId { get; private set; } = string.Empty;

    public override void _Ready()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Height = 0.6f, Radius = 0.3f }
        });
    }

    public void Bind(string entityId)
    {
        EntityId = entityId;
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
}
