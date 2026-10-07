namespace Simulation;

public sealed record TreeRegrowthSnapshot(string TreeId, double ElapsedSeconds);

public sealed class TreeRegrowthService(World world)
{
    public const double RegrowthSeconds = 120;
    public const int AdultWoodCapacity = 12;
    private readonly Dictionary<string, double> _timers = new(StringComparer.Ordinal);

    public double Progress(string treeId)
        => _timers.TryGetValue(treeId, out var elapsed) ? Math.Clamp(elapsed / RegrowthSeconds, 0, 1) : 1;

    public IReadOnlyList<TreeRegrowthSnapshot> Capture()
        => _timers.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new TreeRegrowthSnapshot(pair.Key, pair.Value)).ToArray();

    public void Restore(IEnumerable<TreeRegrowthSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        _timers.Clear();
        foreach (var snapshot in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.TreeId)
                || !double.IsFinite(snapshot.ElapsedSeconds)
                || snapshot.ElapsedSeconds < 0
                || snapshot.ElapsedSeconds >= RegrowthSeconds
                || !world.ResourcePiles.Any(tree => tree.Id == snapshot.TreeId))
                throw new ArgumentException("Tree regrowth snapshots must reference valid in-progress trees.", nameof(snapshots));
            _timers[snapshot.TreeId] = snapshot.ElapsedSeconds;
        }
    }

    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        foreach (var tree in world.ResourcePiles.Where(source => source.Id.StartsWith("tree-adult-", StringComparison.Ordinal)).ToArray())
        {
            if (tree.Contents.Counts.GetValueOrDefault(ItemIds.Wood) > 0)
            {
                _timers.Remove(tree.Id);
                continue;
            }
            var elapsed = _timers.GetValueOrDefault(tree.Id) + seconds;
            if (elapsed >= RegrowthSeconds)
            {
                tree.Contents.TryAdd(ItemIds.Wood, AdultWoodCapacity);
                _timers.Remove(tree.Id);
            }
            else _timers[tree.Id] = elapsed;
        }
    }
}
