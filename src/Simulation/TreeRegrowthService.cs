namespace Simulation;

public sealed record TreeRegrowthSnapshot(string TreeId, double ElapsedSeconds);

public sealed class TreeRegrowthService(World world)
{
    public const double RegrowthSeconds = 120;
    public const int AdultWoodCapacity = 12;
    public double Progress(string treeId)
        => world.TreeRegrowthTimers.TryGetValue(treeId, out var elapsed) ? Math.Clamp(elapsed / RegrowthSeconds, 0, 1) : 1;

    public IReadOnlyList<TreeRegrowthSnapshot> Capture()
        => world.TreeRegrowthTimers.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new TreeRegrowthSnapshot(pair.Key, pair.Value)).ToArray();

    public void Restore(IEnumerable<TreeRegrowthSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        world.ClearTreeRegrowthTimers();
        foreach (var snapshot in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.TreeId)
                || !double.IsFinite(snapshot.ElapsedSeconds)
                || snapshot.ElapsedSeconds < 0
                || snapshot.ElapsedSeconds >= RegrowthSeconds
                || !world.ResourcePiles.Any(tree => tree.Id == snapshot.TreeId))
                throw new ArgumentException("Tree regrowth snapshots must reference valid in-progress trees.", nameof(snapshots));
            world.SetTreeRegrowthTimer(snapshot.TreeId, snapshot.ElapsedSeconds);
        }
    }

    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        foreach (var tree in world.ResourcePiles.Where(source => source.Id.StartsWith("tree-adult-", StringComparison.Ordinal)).ToArray())
        {
            if (tree.Contents.Counts.GetValueOrDefault(ItemIds.Wood) > 0)
            {
                world.RemoveTreeRegrowthTimer(tree.Id);
                continue;
            }
            var elapsed = world.TreeRegrowthTimers.GetValueOrDefault(tree.Id) + seconds;
            if (elapsed >= RegrowthSeconds)
            {
                tree.Contents.TryAdd(ItemIds.Wood, AdultWoodCapacity);
                world.RemoveTreeRegrowthTimer(tree.Id);
            }
            else world.SetTreeRegrowthTimer(tree.Id, elapsed);
        }
    }
}
