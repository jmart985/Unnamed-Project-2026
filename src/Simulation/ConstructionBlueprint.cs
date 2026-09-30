namespace Simulation;

public sealed class ConstructionBlueprint
{
    private readonly Dictionary<string, int> _required;
    private readonly Dictionary<string, int> _progress = new(StringComparer.Ordinal);
    public ConstructionBlueprint(string id, int width, int depth, IReadOnlyDictionary<string, int> required)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A blueprint ID is required.", nameof(id));
        if (width <= 0 || depth <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(required);
        if (required.Count == 0 || required.Any(item => !ItemIds.IsKnown(item.Key) || item.Value <= 0)) throw new ArgumentException("Requirements must contain known items with positive quantities.", nameof(required));
        Id=id; Width=width; Depth=depth; _required=new(required, StringComparer.Ordinal);
    }
    public string Id { get; }
    public int Width { get; }
    public int Depth { get; }
    public bool IsComplete => _required.All(item => GetProgress(item.Key) >= item.Value);
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
}
