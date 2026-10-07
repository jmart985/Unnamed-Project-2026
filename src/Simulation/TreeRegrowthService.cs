namespace Simulation;

public sealed class TreeRegrowthService(World world)
{
    public const double RegrowthSeconds = 120;
    public const int AdultWoodCapacity = 12;
    private readonly Dictionary<string, double> _timers = new(StringComparer.Ordinal);

    public double Progress(string treeId)
        => _timers.TryGetValue(treeId, out var elapsed) ? Math.Clamp(elapsed / RegrowthSeconds, 0, 1) : 1;

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
