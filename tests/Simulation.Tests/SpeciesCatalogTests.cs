using System.Text;
using Simulation;

namespace Simulation.Tests;

public sealed class SpeciesCatalogTests
{
    [Fact]
    public void LoadsDataDrivenSpeciesAndSupportsDietAndCapabilityFiltering()
    {
        var catalog = SpeciesCatalog.LoadFromFile(GetSpeciesDataPath());

        Assert.Equal(new[] { "human", "creature" }, catalog.Species.Select(species => species.Id));
        Assert.Equal(3.0, catalog.GetRequired("human").MovementSpeed);
        Assert.Equal(0.2, catalog.GetRequired("human").HungerRate);
        Assert.Equal(new[] { ItemIds.Food, ItemIds.Plant, ItemIds.Fruit, ItemIds.Meat }, catalog.GetRequired("human").Diet);
        Assert.True(catalog.GetRequired("human").CanHaul);
        Assert.False(catalog.GetRequired("creature").CanHaul);
        Assert.Equal(new[] { "human", "creature" },
            catalog.GetCompatibleWithDiet(ItemIds.Food).Select(species => species.Id));
        Assert.Equal(new[] { "human" }, catalog.GetHaulers().Select(species => species.Id));
        Assert.Empty(catalog.GetCompatibleWithDiet(ItemIds.Wood));
    }

    [Theory]
    [InlineData("unknown", "Species 'unknown' is not defined.")]
    [InlineData("", "A species ID is required.")]
    public void RequiredLookupRejectsInvalidSpeciesReferences(string speciesId, string expectedMessage)
    {
        var catalog = Load("""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true}]
            """);

        var exception = Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired(speciesId));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonpositiveMovementSpeed(double movementSpeed)
    {
        var json = $$"""
            [{"id":"human","movementSpeed":{{movementSpeed}},"hungerRate":0.2,"diet":["food"],"canHaul":true}]
            """;

        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.Contains("movementSpeed must be finite and greater than zero", exception.Message);
    }

    [Fact]
    public void RejectsUnknownDietItemIds()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"creature","movementSpeed":2,"hungerRate":0.2,"diet":["grain"],"canHaul":false}]"""));

        Assert.Contains("references unknown diet item ID 'grain'", exception.Message);
    }

    [Fact]
    public void RejectsDuplicateSpeciesIds()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Load("""
                [
                  {"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true},
                  {"id":"human","movementSpeed":2,"hungerRate":0.2,"diet":["food"],"canHaul":false}
                ]
                """));

        Assert.Contains("Species ID 'human' is defined more than once", exception.Message);
    }

    [Fact]
    public void RejectsMissingRequiredFieldsAndInvalidHungerRates()
    {
        var missingField = Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"human","hungerRate":0.2,"diet":["food"],"canHaul":true}]"""));
        var invalidHunger = Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"human","movementSpeed":3,"hungerRate":-0.1,"diet":["food"],"canHaul":true}]"""));

        Assert.Contains("must define movementSpeed", missingField.Message);
        Assert.Contains("hungerRate must be finite and nonnegative", invalidHunger.Message);
    }

    private static SpeciesCatalog Load(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return SpeciesCatalog.Load(stream);
    }

    private static string GetSpeciesDataPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "data", "species.json");
    }
}
