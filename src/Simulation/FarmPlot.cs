namespace Simulation;

public sealed record CropDefinition
{
    public CropDefinition(string id, int growthRequired, int harvestYield)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A crop ID is required.", nameof(id));
        if (growthRequired <= 0 || harvestYield <= 0) throw new ArgumentOutOfRangeException(nameof(growthRequired));
        Id = id; GrowthRequired = growthRequired; HarvestYield = harvestYield;
    }
    public string Id { get; }
    public int GrowthRequired { get; }
    public int HarvestYield { get; }
}

public sealed class FarmPlot
{
    public FarmPlot(string id, int capacity)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A plot ID is required.", nameof(id));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Id = id; Capacity = capacity;
    }
    public string Id { get; }
    public int Capacity { get; }
    public CropDefinition? Crop { get; private set; }
    public int Growth { get; private set; }
    public bool IsPlanted => Crop is not null;
    public bool IsMature => Crop is not null && Growth >= Crop.GrowthRequired;
    public bool TryPlant(CropDefinition? crop, int amount)
    {
        if (crop is null || amount <= 0 || amount > Capacity || IsPlanted) return false;
        Crop = crop; Growth = 0; return true;
    }
    public bool AdvanceGrowth(int amount)
    {
        if (!IsPlanted || amount <= 0) return false;
        Growth = Math.Min(Crop!.GrowthRequired, Growth + amount); return true;
    }
    public int Harvest()
    {
        if (!IsMature) return 0;
        var yield = Crop!.HarvestYield; Crop = null; Growth = 0; return yield;
    }
}
