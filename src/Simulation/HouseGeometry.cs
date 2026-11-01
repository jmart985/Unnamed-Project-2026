using System.Numerics;

namespace Simulation;

public sealed record HousePart(string Name, Vector3 Center, Vector3 Size);

/// <summary>Metre-scale house geometry shared by collision, navigation and presentation.</summary>
public static class HouseGeometry
{
    public const float Width = 8;
    public const float Depth = 8;
    public const float WallHeight = 3.2f;
    public const float WallThickness = .3f;
    public const float DoorWidth = 2.4f;
    public const float DoorHeight = 2.6f;
    public const float DoorZ = Depth / 2 - WallThickness / 2;
    public const float NavigationClearance = .35f;

    public static IEnumerable<HousePart> SolidParts(bool closed)
    {
        yield return new("Floor", new(0, -.05f, 0), new(Width, .1f, Depth));
        yield return new("Ceiling", new(0, WallHeight + .1f, 0), new(Width, .2f, Depth));
        yield return new("WallLeft", new(-DoorZ, WallHeight / 2, 0), new(WallThickness, WallHeight, Depth));
        yield return new("WallRight", new(DoorZ, WallHeight / 2, 0), new(WallThickness, WallHeight, Depth));
        yield return new("WallBack", new(0, WallHeight / 2, -DoorZ), new(Width, WallHeight, WallThickness));
        var sideWidth = (Width - DoorWidth) / 2;
        var sideX = (Width + DoorWidth) / 4;
        yield return new("WallFrontLeft", new(-sideX, WallHeight / 2, DoorZ), new(sideWidth, WallHeight, WallThickness));
        yield return new("WallFrontRight", new(sideX, WallHeight / 2, DoorZ), new(sideWidth, WallHeight, WallThickness));
        yield return new("DoorHeader", new(0, (DoorHeight + WallHeight) / 2, DoorZ),
            new(DoorWidth, WallHeight - DoorHeight, WallThickness));
        if (closed) yield return new("Door", new(0, DoorHeight / 2, DoorZ), new(DoorWidth, DoorHeight, .2f));
    }

    public static Vector3 Rotate(Vector3 point, int quarterTurns) => quarterTurns switch
    {
        0 => point, 1 => new(point.Z, point.Y, -point.X),
        2 => new(-point.X, point.Y, -point.Z), 3 => new(-point.Z, point.Y, point.X),
        _ => throw new ArgumentOutOfRangeException(nameof(quarterTurns))
    };

    public static bool Contains(Structure house, Vector3 position, float margin = 0) =>
        Math.Abs(position.X - house.Position.X) < Width / 2 + margin
        && Math.Abs(position.Z - house.Position.Z) < Depth / 2 + margin;
}
