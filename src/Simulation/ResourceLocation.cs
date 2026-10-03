using System.Numerics;

namespace Simulation;

public sealed class ResourceLocation
{
    public ResourceLocation(string id, Vector3 position, ResourceContainer contents)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A resource location ID is required.", nameof(id));
        }

        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position coordinates must be finite.");
        }

        ArgumentNullException.ThrowIfNull(contents);

        Id = id;
        Position = position;
        Contents = contents;
    }

    public string Id { get; }
    public Vector3 Position { get; }
    public ResourceContainer Contents { get; }
    public string? FactionOwnerId { get; internal set; }
}
