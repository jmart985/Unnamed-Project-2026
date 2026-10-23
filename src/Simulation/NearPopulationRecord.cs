using System.Collections.ObjectModel;

namespace Simulation;

/// <summary>Durable membership linking active entities to their regional population.</summary>
public sealed class NearPopulationRecord
{
    public NearPopulationRecord(string regionId, string speciesId, IEnumerable<string> entityIds,
        IEnumerable<KeyValuePair<string, int>> resources)
    {
        if (string.IsNullOrWhiteSpace(regionId) || string.IsNullOrWhiteSpace(speciesId))
            throw new ArgumentException("Near populations require region and species IDs.");
        ArgumentNullException.ThrowIfNull(entityIds);
        ArgumentNullException.ThrowIfNull(resources);
        var ids = entityIds.ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new ArgumentException("Near populations require distinct entity IDs.", nameof(entityIds));
        var entries = resources.ToArray();
        if (entries.Any(pair => !ItemIds.IsKnown(pair.Key) || pair.Value < 0)
            || entries.Select(pair => pair.Key).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new ArgumentException("Near populations require unique known resource IDs and nonnegative quantities.", nameof(resources));
        RegionId = regionId;
        SpeciesId = speciesId;
        EntityIds = Array.AsReadOnly(ids);
        Resources = new ReadOnlyDictionary<string, int>(entries.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    public string RegionId { get; }
    public string SpeciesId { get; }
    public IReadOnlyList<string> EntityIds { get; }
    public IReadOnlyDictionary<string, int> Resources { get; }
}
