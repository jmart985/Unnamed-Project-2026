using System.Collections.ObjectModel;
using System.Numerics;

namespace Simulation;

public enum PerceptionQueryStatus
{
    Success,
    InvalidObserver,
    InvalidSpecies,
    TrackingUnsupported
}

public sealed record PerceptionQueryResult<T>(
    PerceptionQueryStatus Status,
    ReadOnlyCollection<T> Results);

public sealed class CreatureTrack
{
    /// <summary>
    /// Describes an explicitly supplied track observation. Track creation, expiry, and
    /// persistence are intentionally owned by future behavior that consumes perception.
    /// </summary>
    public CreatureTrack(string targetEntityId, Vector3 position, long recordedAtTick)
    {
        if (string.IsNullOrWhiteSpace(targetEntityId))
        {
            throw new ArgumentException("A tracked entity ID is required.", nameof(targetEntityId));
        }

        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Track position coordinates must be finite.");
        }

        if (recordedAtTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recordedAtTick), "Track tick cannot be negative.");
        }

        TargetEntityId = targetEntityId;
        Position = position;
        RecordedAtTick = recordedAtTick;
    }

    public string TargetEntityId { get; }
    public Vector3 Position { get; }
    public long RecordedAtTick { get; }
}

public sealed class PerceptionQueryService
{
    private readonly World _world;
    private readonly SpeciesCatalog _catalog;

    public PerceptionQueryService(World world, SpeciesCatalog catalog)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public PerceptionQueryResult<FoodSource> FindNearbyFood(string? observerEntityId)
    {
        if (!TryResolveObserver(observerEntityId, out var observer, out var species, out var failure))
        {
            return Empty<FoodSource>(failure);
        }

        var results = _world.FoodSources
            .Where(source => IsInRange(observer.Position, source.Position, species.SmellRange)
                && source.Contents.Counts.Any(item => item.Value > 0 && species.Eats(item.Key)))
            .OrderBy(source => DistanceSquared(observer.Position, source.Position))
            .ThenBy(source => source.Id, StringComparer.Ordinal)
            .ToArray();
        return Success(results);
    }

    public PerceptionQueryResult<EntityRecord> FindNearbyEntities(string? observerEntityId)
    {
        if (!TryResolveObserver(observerEntityId, out var observer, out var species, out var failure))
        {
            return Empty<EntityRecord>(failure);
        }

        var results = _world.Entities
            .Where(entity => !string.Equals(entity.Id, observer.Id, StringComparison.Ordinal)
                && IsInRange(observer.Position, entity.Position, species.SmellRange))
            .OrderBy(entity => DistanceSquared(observer.Position, entity.Position))
            .ThenBy(entity => entity.Id, StringComparer.Ordinal)
            .ToArray();
        return Success(results);
    }

    public PerceptionQueryResult<CreatureTrack> FindNearbyTracks(
        string? observerEntityId,
        IEnumerable<CreatureTrack> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        var candidates = tracks.ToArray();
        if (candidates.Any(track => track is null))
        {
            throw new ArgumentException("Tracks cannot contain null entries.", nameof(tracks));
        }

        if (!TryResolveObserver(observerEntityId, out var observer, out var species, out var failure))
        {
            return Empty<CreatureTrack>(failure);
        }

        if (!species.CanTrack)
        {
            return Empty<CreatureTrack>(PerceptionQueryStatus.TrackingUnsupported);
        }

        var results = candidates
            .Where(track => !string.Equals(track.TargetEntityId, observer.Id, StringComparison.Ordinal)
                && IsInRange(observer.Position, track.Position, species.SmellRange))
            .OrderBy(track => DistanceSquared(observer.Position, track.Position))
            .ThenBy(track => track.TargetEntityId, StringComparer.Ordinal)
            .ThenBy(track => track.RecordedAtTick)
            .ThenBy(track => track.Position.X)
            .ThenBy(track => track.Position.Y)
            .ThenBy(track => track.Position.Z)
            .ToArray();
        return Success(results);
    }

    private bool TryResolveObserver(
        string? observerEntityId,
        out EntityRecord observer,
        out SpeciesDefinition species,
        out PerceptionQueryStatus failure)
    {
        if (!_world.TryGetEntity(observerEntityId, out var entity) || entity is null)
        {
            observer = null!;
            species = null!;
            failure = PerceptionQueryStatus.InvalidObserver;
            return false;
        }

        if (!_catalog.TryGet(entity.SpeciesId, out var definition) || definition is null)
        {
            observer = entity;
            species = null!;
            failure = PerceptionQueryStatus.InvalidSpecies;
            return false;
        }

        observer = entity;
        species = definition;
        failure = PerceptionQueryStatus.Success;
        return true;
    }

    private static bool IsInRange(Vector3 origin, Vector3 target, double range)
    {
        return DistanceSquared(origin, target) <= range * range;
    }

    private static double DistanceSquared(Vector3 first, Vector3 second)
    {
        var x = (double)first.X - second.X;
        var y = (double)first.Y - second.Y;
        var z = (double)first.Z - second.Z;
        return x * x + y * y + z * z;
    }

    private static PerceptionQueryResult<T> Success<T>(T[] results)
    {
        return new PerceptionQueryResult<T>(PerceptionQueryStatus.Success, Array.AsReadOnly(results));
    }

    private static PerceptionQueryResult<T> Empty<T>(PerceptionQueryStatus status)
    {
        return new PerceptionQueryResult<T>(status, Array.AsReadOnly(Array.Empty<T>()));
    }
}
