using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class FarmPlotTests
{
    private static readonly CropDefinition Wheat = new("wheat", 5, 3);

    [Fact]
    public void PlotPlantsGrowsAndHarvestsYield()
    {
        var plot = new FarmPlot("plot-1", 2);
        Assert.True(plot.TryPlant(Wheat, 2));
        Assert.True(plot.AdvanceGrowth(10));
        Assert.True(plot.IsMature);
        Assert.Equal(3, plot.Harvest());
        Assert.False(plot.IsPlanted);
    }

    [Fact]
    public void InvalidPlantingAndPrematureHarvestDoNotMutatePlot()
    {
        var plot = new FarmPlot("plot-1", 1);
        Assert.False(plot.TryPlant(null, 1));
        Assert.False(plot.TryPlant(Wheat, 2));
        Assert.True(plot.TryPlant(Wheat, 1));
        Assert.False(plot.AdvanceGrowth(0));
        Assert.Equal(0, plot.Harvest());
        Assert.True(plot.IsPlanted);
    }
}
