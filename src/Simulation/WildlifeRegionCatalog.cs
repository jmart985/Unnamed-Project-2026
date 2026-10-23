using System.Numerics;
using System.Text.Json;

namespace Simulation;

public sealed record WildlifeSpeciesRegion(string SpeciesId, double AverageSatiety, IReadOnlyList<Vector3> SpawnPositions);
public sealed record WildlifeRegionDefinition(string Id, Vector3 Center, float ActivationRadius, float DeactivationRadius,
    IReadOnlyList<WildlifeSpeciesRegion> Species);

/// <summary>Validated, immutable placement definitions for the bounded wildlife activation prototype.</summary>
public sealed class WildlifeRegionCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IReadOnlyList<WildlifeRegionDefinition> _regions;

    public WildlifeRegionCatalog(IEnumerable<WildlifeRegionDefinition> regions, SpeciesCatalog speciesCatalog)
    {
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(speciesCatalog);
        var values = regions.ToArray();
        if (values.Length == 0 || values.Any(region => region is null)
            || values.Select(region => region.Id).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Wildlife regions must have unique IDs.", nameof(regions));
        foreach (var region in values)
        {
            if (!IsValidId(region.Id) || !IsFinite(region.Center)
                || !float.IsFinite(region.ActivationRadius) || region.ActivationRadius <= 0
                || !float.IsFinite(region.DeactivationRadius) || region.DeactivationRadius < region.ActivationRadius
                || region.Species is null || region.Species.Count == 0 || region.Species.Any(entry => entry is null))
                throw new ArgumentException($"Wildlife region '{region.Id}' has invalid identity, bounds, or species.", nameof(regions));
            if (region.Species.Select(entry => entry.SpeciesId).Distinct(StringComparer.Ordinal).Count() != region.Species.Count)
                throw new ArgumentException($"Wildlife region '{region.Id}' has duplicate species.", nameof(regions));
            foreach (var entry in region.Species)
            {
                if (!speciesCatalog.TryGet(entry.SpeciesId, out _)
                    || !double.IsFinite(entry.AverageSatiety) || entry.AverageSatiety is < 0 or > 100
                    || entry.SpawnPositions is null || entry.SpawnPositions.Count == 0
                    || entry.SpawnPositions.Any(position => !IsFinite(position)
                        || HorizontalDistance(position, region.Center) > region.DeactivationRadius))
                    throw new ArgumentException($"Wildlife region '{region.Id}' has an invalid species entry.", nameof(regions));
            }
        }
        _regions = Array.AsReadOnly(values);
    }

    public IReadOnlyList<WildlifeRegionDefinition> Regions => _regions;

    public static WildlifeRegionCatalog LoadFromFile(string path, SpeciesCatalog speciesCatalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<RegionFile[]>(json, JsonOptions)
            ?? throw new InvalidDataException("Wildlife region data is empty.");
        if (document.Any(region => region is null))
            throw new InvalidDataException("Wildlife region data cannot contain null region entries.");
        if (document.Any(region => region.Species?.Any(entry => entry is null) == true))
            throw new InvalidDataException("Wildlife region data cannot contain null species entries.");
        try
        {
            return new WildlifeRegionCatalog(document.Select(region => new WildlifeRegionDefinition(
                region.Id ?? string.Empty,
                ToVector(region.Center),
                region.ActivationRadius,
                region.DeactivationRadius,
                (region.Species ?? []).Select(entry => new WildlifeSpeciesRegion(
                    entry.SpeciesId ?? string.Empty,
                    entry.AverageSatiety,
                    Array.AsReadOnly((entry.SpawnPositions ?? []).Select(ToVector).ToArray()))).ToArray())), speciesCatalog);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Wildlife region data failed validation.", exception);
        }
    }

    public static bool IsValidId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)
            || !IsAlphaNumeric(id[0])
            || !IsAlphaNumeric(id[^1])) return false;
        var previousWasSeparator = false;
        foreach (var character in id)
        {
            if (character == '-')
            {
                if (previousWasSeparator) return false;
                previousWasSeparator = true;
                continue;
            }
            if (!IsAlphaNumeric(character)) return false;
            previousWasSeparator = false;
        }
        return true;
    }

    private static bool IsAlphaNumeric(char character) => character is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static float HorizontalDistance(Vector3 left, Vector3 right)
    {
        var x = left.X - right.X;
        var z = left.Z - right.Z;
        return MathF.Sqrt(x * x + z * z);
    }

    private static Vector3 ToVector(VectorData? value) => value is null
        ? new Vector3(float.NaN)
        : new Vector3(value.X, value.Y, value.Z);
    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private sealed class RegionFile
    {
        public string? Id { get; set; }
        public VectorData? Center { get; set; }
        public float ActivationRadius { get; set; }
        public float DeactivationRadius { get; set; }
        public SpeciesFile[]? Species { get; set; }
    }
    private sealed class SpeciesFile
    {
        public string? SpeciesId { get; set; }
        public double AverageSatiety { get; set; }
        public VectorData[]? SpawnPositions { get; set; }
    }
    private sealed class VectorData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}
