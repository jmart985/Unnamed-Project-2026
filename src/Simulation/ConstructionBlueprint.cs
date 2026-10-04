namespace Simulation;

public sealed class ConstructionBlueprint
{
    private readonly Dictionary<string, int> _required;
    private readonly Dictionary<string, int> _progress = new(StringComparer.Ordinal);
    public ConstructionBlueprint(string id, int width, int depth, IReadOnlyDictionary<string, int> required, int workRequired = 0)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A blueprint ID is required.", nameof(id));
        if (width <= 0 || depth <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(required);
        if (required.Count == 0 || required.Any(item => !ItemIds.IsKnown(item.Key) || item.Value <= 0)) throw new ArgumentException("Requirements must contain known items with positive quantities.", nameof(required));
        if (workRequired < 0) throw new ArgumentOutOfRangeException(nameof(workRequired));
        Id=id; Width=width; Depth=depth; WorkRequired=workRequired; _required=new(required, StringComparer.Ordinal);
    }
    public string Id { get; }
    public int Width { get; }
    public int Depth { get; }
    public int WorkRequired { get; }
    public int WorkProgress { get; private set; }
    public bool MaterialsComplete => _required.All(item => GetProgress(item.Key) >= item.Value);
    public bool IsComplete => MaterialsComplete && WorkProgress >= WorkRequired;
    public IReadOnlyDictionary<string,int> RequiredMaterials => _required;
    public IReadOnlyDictionary<string,int> Progress => _progress;
    public int GetProgress(string itemId) => _progress.GetValueOrDefault(itemId);
    public ResourceMutationResult ApplyProgress(string? itemId, int amount)
    {
        if (!ItemIds.IsKnown(itemId) || !_required.ContainsKey(itemId!)) return ResourceMutationResult.UnknownItem;
        if (amount <= 0) return ResourceMutationResult.InvalidAmount;
        var remaining = _required[itemId!] - GetProgress(itemId!);
        _progress[itemId!] = GetProgress(itemId!) + Math.Min(amount, remaining);
        return ResourceMutationResult.Success;
    }
    public int Remaining(string itemId) => Math.Max(0, _required.GetValueOrDefault(itemId) - GetProgress(itemId));
    public bool ApplyWork(int amount)
    {
        if (!MaterialsComplete || IsComplete || amount <= 0) return false;
        WorkProgress = Math.Min(WorkRequired, WorkProgress + amount);
        return true;
    }
    internal void Restore(IReadOnlyDictionary<string, int> progress, int workProgress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.Any(item => !_required.TryGetValue(item.Key, out var required) || item.Value < 0 || item.Value > required)
            || _required.Any(item => progress.GetValueOrDefault(item.Key) > item.Value)
            || (workProgress > 0 && _required.Any(item => progress.GetValueOrDefault(item.Key) < item.Value))
            || workProgress < 0 || workProgress > WorkRequired)
            throw new ArgumentException("Construction progress is invalid.");
        _progress.Clear();
        foreach (var item in progress.Where(item => item.Value > 0)) _progress.Add(item.Key, item.Value);
        WorkProgress = workProgress;
    }
}
