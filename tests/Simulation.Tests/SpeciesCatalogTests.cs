using System.Text;
using Simulation;

namespace Simulation.Tests;

public sealed class SpeciesCatalogTests
{
    [Fact]
    public void LoadsDataDrivenSpeciesAndSupportsDietAndCapabilityFiltering()
    {
        var catalog = SpeciesCatalog.LoadFromFile(GetSpeciesDataPath());

        Assert.Equal(new[] { "human", "creature", "ape", "cow", "wolf" }, catalog.Species.Select(species => species.Id));
        Assert.Equal(3.0, catalog.GetRequired("human").MovementSpeed);
        Assert.Equal(0.2, catalog.GetRequired("human").HungerRate);
        Assert.Equal(new[] { ItemIds.Food, ItemIds.Plant, ItemIds.Fruit, ItemIds.Meat }, catalog.GetRequired("human").Diet);
        Assert.True(catalog.GetRequired("human").CanHaul);
        Assert.False(catalog.GetRequired("creature").CanHaul);
        Assert.Equal(new[] { "human", "creature" },
            catalog.GetCompatibleWithDiet(ItemIds.Food).Select(species => species.Id));
        Assert.Equal(new[] { "human", "ape", "cow" }, catalog.GetHaulers().Select(species => species.Id));
        Assert.Empty(catalog.GetCompatibleWithDiet(ItemIds.Wood));
        Assert.True(catalog.GetRequired("ape").CanUseTools);
        Assert.True(catalog.GetRequired("ape").CanOpenDoors);
        Assert.True(catalog.GetRequired("wolf").CanTrack);
        Assert.True(catalog.GetRequired("wolf").SmellRange > catalog.GetRequired("cow").SmellRange);
        Assert.Equal(DietaryProfile.Herbivore, catalog.GetRequired("cow").DietaryProfile);
        Assert.Equal(DietaryProfile.Carnivore, catalog.GetRequired("wolf").DietaryProfile);
        Assert.Equal(new TemperamentProfile(0.75, 0.9, 0.8), catalog.GetRequired("ape").Temperament);
        Assert.NotEqual(catalog.GetRequired("ape").Temperament, catalog.GetRequired("cow").Temperament);
    }

    [Theory]
    [InlineData("unknown", "Species 'unknown' is not defined.")]
    [InlineData("", "A species ID is required.")]
    public void RequiredLookupRejectsInvalidSpeciesReferences(string speciesId, string expectedMessage)
    {
        var catalog = Load("""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"curiosity":0.5,"sociability":0.5}}]
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
            [{"id":"human","movementSpeed":{{movementSpeed}},"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"curiosity":0.5,"sociability":0.5}}]
            """;

        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.Contains("movementSpeed must be finite and greater than zero", exception.Message);
    }

    [Fact]
    public void RejectsUnknownDietItemIds()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"creature","movementSpeed":2,"hungerRate":0.2,"diet":["grain"],"canHaul":false,"temperament":{"boldness":0.5,"curiosity":0.5,"sociability":0.5}}]"""));

        Assert.Contains("references unknown diet item ID 'grain'", exception.Message);
    }

    [Fact]
    public void RejectsDuplicateSpeciesIds()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Load("""
                [
                  {"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"curiosity":0.5,"sociability":0.5}},
                  {"id":"human","movementSpeed":2,"hungerRate":0.2,"diet":["food"],"canHaul":false,"temperament":{"boldness":0.5,"curiosity":0.5,"sociability":0.5}}
                ]
                """));

        Assert.Contains("Species ID 'human' is defined more than once", exception.Message);
    }

    [Fact]
    public void RejectsMissingRequiredFieldsAndInvalidHungerRates()
    {
        var missingField = Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"human","hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"curiosity":0.5,"sociability":0.5}}]"""));
        var invalidHunger = Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"human","movementSpeed":3,"hungerRate":-0.1,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"curiosity":0.5,"sociability":0.5}}]"""));

        Assert.Contains("must define movementSpeed", missingField.Message);
        Assert.Contains("hungerRate must be finite and nonnegative", invalidHunger.Message);
    }

    [Theory]
    [InlineData("""{"boldness":0.5,"curiosity":0.5}""", "must define temperament.sociability")]
    [InlineData("""{"boldness":0.5,"curiosity":"unknown","sociability":0.5}""", "valid species records")]
    [InlineData("""{"boldness":"NaN","curiosity":0.5,"sociability":0.5}""", "temperament.boldness must be finite")]
    [InlineData("""{"boldness":"Infinity","curiosity":0.5,"sociability":0.5}""", "temperament.boldness must be finite")]
    [InlineData("""{"boldness":-0.1,"curiosity":0.5,"sociability":0.5}""", "temperament.boldness must be finite")]
    [InlineData("""{"boldness":1.1,"curiosity":0.5,"sociability":0.5}""", "temperament.boldness must be finite")]
    public void RejectsInvalidTemperamentWithoutPublishingPartialCatalog(string temperamentJson, string expectedMessage)
    {
        var json = $$"""
            [
              {"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"curiosity":0.5,"sociability":0.5}},
              {"id":"creature","movementSpeed":2,"hungerRate":0.2,"diet":["food"],"canHaul":false,"temperament":{{temperamentJson}}}
            ]
            """;

        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void RejectsMissingTemperamentProfile()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true}]"""));

        Assert.Contains("Species 'human' must define temperament", exception.Message);
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
