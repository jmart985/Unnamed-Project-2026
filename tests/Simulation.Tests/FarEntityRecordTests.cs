using Simulation;

namespace Simulation.Tests;

public sealed class FarEntityRecordTests
{
    [Fact]
    public void RecordsHaveStableEqualityAndSerialization()
    {
        var first = new FarEntityRecord("grove", "creature", 3, 42, [new("wood", 2), new("food", 4)]);
        var second = new FarEntityRecord("grove", "creature", 3, 42, [new("food", 4), new("wood", 2)]);
        Assert.Equal(first, second);
        Assert.Equal(first.Serialize(), second.Serialize());
    }

    [Fact]
    public void ConversionRejectsInvalidSpeciesAndNegativeCounts()
    {
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var converter = new FarEntityConverter(catalog);
        Assert.Equal(FarEntityValidationStatus.InvalidSpecies, converter.Convert("grove", "missing", 1, 10, []).Status);
        Assert.Equal(FarEntityValidationStatus.NegativeCount, converter.Convert("grove", "creature", -1, 10, []).Status);
    }

    [Fact]
    public void RecordsRejectInvalidSatietyAndResources()
    {
        Assert.Throws<ArgumentException>(() => new FarEntityRecord("grove", "creature", 1, 101, []));
        Assert.Throws<ArgumentException>(() => new FarEntityRecord("grove", "creature", 1, 50, [new("unknown", 1)]));
        Assert.Throws<ArgumentException>(() => new FarEntityRecord("grove", "creature", 1, 50, [new(ItemIds.Food, -1)]));
    }
}
