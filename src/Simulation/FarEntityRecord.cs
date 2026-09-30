using System.Collections.ObjectModel;
using System.Text.Json;

namespace Simulation;

public sealed class FarEntityRecord : IEquatable<FarEntityRecord>
{
    public FarEntityRecord(string regionId, string speciesId, int count, double averageSatiety, IEnumerable<KeyValuePair<string, int>> resources)
    {
        if (string.IsNullOrWhiteSpace(regionId) || string.IsNullOrWhiteSpace(speciesId) || count < 0 || !double.IsFinite(averageSatiety))
            throw new ArgumentException("Far records require IDs, a nonnegative count, and finite needs state.");

        RegionId = regionId;
        SpeciesId = speciesId;
        Count = count;
        AverageSatiety = averageSatiety;
        var sorted = resources?.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            ?? throw new ArgumentNullException(nameof(resources));
        Resources = new ReadOnlyDictionary<string, int>(sorted);
    }

    public string RegionId { get; }
    public string SpeciesId { get; }
    public int Count { get; }
    public double AverageSatiety { get; }
    public IReadOnlyDictionary<string, int> Resources { get; }

    public string Serialize() => JsonSerializer.Serialize(new { regionId = RegionId, speciesId = SpeciesId, count = Count, averageSatiety = AverageSatiety, resources = Resources });
    public bool Equals(FarEntityRecord? other) => other is not null && RegionId == other.RegionId && SpeciesId == other.SpeciesId && Count == other.Count && AverageSatiety.Equals(other.AverageSatiety) && Resources.SequenceEqual(other.Resources);
    public override bool Equals(object? obj) => Equals(obj as FarEntityRecord);
    public override int GetHashCode() => HashCode.Combine(RegionId, SpeciesId, Count, AverageSatiety, Resources.Count);
}

public enum FarEntityValidationStatus { Valid, InvalidSpecies, NegativeCount }
public sealed record FarEntityValidationResult(FarEntityValidationStatus Status, FarEntityRecord? Record = null);

public sealed class FarEntityConverter
{
    private readonly SpeciesCatalog _catalog;
    public FarEntityConverter(SpeciesCatalog catalog) => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    public FarEntityValidationResult Convert(string regionId, string speciesId, int count, double averageSatiety, IEnumerable<KeyValuePair<string, int>> resources)
    {
        if (count < 0) return new(FarEntityValidationStatus.NegativeCount);
        if (!_catalog.TryGet(speciesId, out _)) return new(FarEntityValidationStatus.InvalidSpecies);
        return new(FarEntityValidationStatus.Valid, new FarEntityRecord(regionId, speciesId, count, averageSatiety, resources));
    }
}
