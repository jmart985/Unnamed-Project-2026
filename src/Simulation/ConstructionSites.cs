using System.Numerics;

namespace Simulation;

public enum ConstructionSiteState { Planned, Complete }

public sealed class SettlementConstructionSite
{
    public SettlementConstructionSite(string id, Vector3 position, ConstructionBlueprint blueprint)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A site ID is required.", nameof(id));
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)) throw new ArgumentOutOfRangeException(nameof(position));
        ArgumentNullException.ThrowIfNull(blueprint);
        Id=id; Position=position; Blueprint=blueprint;
    }
    public string Id { get; }
    public Vector3 Position { get; }
    public ConstructionBlueprint Blueprint { get; }
    public ConstructionSiteState State => Blueprint.IsComplete ? ConstructionSiteState.Complete : ConstructionSiteState.Planned;
}

public sealed class ConstructionSiteRegistry
{
    private readonly Dictionary<string,SettlementConstructionSite> _sites = new(StringComparer.Ordinal);
    public IReadOnlyCollection<SettlementConstructionSite> Sites => _sites.Values;
    public bool TryRegister(SettlementConstructionSite? site) => site is not null && _sites.TryAdd(site.Id, site);
    public bool TryGet(string? id, out SettlementConstructionSite? site) => _sites.TryGetValue(id ?? string.Empty, out site);
}
