using System.Numerics;

namespace Simulation;

public enum WildTerritoryResult
{
    Accepted,
    Unchanged,
    InvalidEntity,
    InvalidGeometry
}

public sealed record WildTerritory(string EntityId, Vector3 Center, float Radius)
{
    public bool Contains(Vector3 position)
    {
        if (!IsValidGeometry(Center, Radius)
            || !float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            return false;

        var x = (double)position.X - Center.X;
        var y = (double)position.Y - Center.Y;
        var z = (double)position.Z - Center.Z;
        return x * x + y * y + z * z <= (double)Radius * Radius;
    }

    public static bool IsValidGeometry(Vector3 center, float radius) =>
        float.IsFinite(center.X) && float.IsFinite(center.Y) && float.IsFinite(center.Z)
        && float.IsFinite(radius) && radius > 0;
}
