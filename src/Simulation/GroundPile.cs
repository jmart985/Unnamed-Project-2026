using System.Numerics;

namespace Simulation;

public sealed class GroundPile : ItemContainer
{
    public GroundPile(ItemCatalog catalog, int capacity, Vector3 position)
        : base(catalog, capacity)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position coordinates must be finite.");
        }

        Position = position;
    }

    public Vector3 Position { get; }
}
