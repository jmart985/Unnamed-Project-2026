using System.Collections.Generic;
using System.Numerics;

namespace Simulation;

public sealed class Farm
{
    public string Id { get; }
    public Vector3 Position { get; }
    public List<FarmPlot> Plots { get; } = new();

    public Farm(string id, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Farm ID is required.");
        Id = id;
        Position = position;
    }

    public void AddPlot(FarmPlot plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        Plots.Add(plot);
    }

    public void AdvanceAllGrowth(int amount)
    {
        foreach (var plot in Plots)
        {
            plot.AdvanceGrowth(amount);
        }
    }
}
