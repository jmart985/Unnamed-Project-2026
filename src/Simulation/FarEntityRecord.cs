using System.Collections.ObjectModel;
using System.Text.Json;

namespace Simulation;

public sealed class FarEntityRecord : IEquatable<FarEntityRecord>
{
    public FarEntityRecord(string regionId, string speciesId, int count, double averageSatiety, IEnumerable<KeyValuePair<string, int>> resources)
    {
        if (string.IsNullOrWhiteSpace(regionId) || string.IsNullOrWhiteSpace(speciesId) || count < 0 || !double.IsFinite(averageSatiety) || averageSatiety is < 0 or > 100)
            throw new ArgumentException("Far records require IDs, a nonnegative count, and average satiety in the range 0 to 100.");

        RegionId = regionId;
        SpeciesId = speciesId;
        Count = count;
        AverageSatiety = averageSatiety;
        if (resources is null) throw new ArgumentNullException(nameof(resources));
        var entries = resources.ToArray();
        if (entries.Any(pair => !ItemIds.IsKnown(pair.Key) || pair.Value < 0))
            throw new ArgumentException("Far records require known resource IDs and nonnegative quantities.", nameof(resources));
        var sorted = entries.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
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
