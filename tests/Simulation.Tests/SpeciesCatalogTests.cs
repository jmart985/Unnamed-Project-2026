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
        Assert.All(catalog.Species.Where(species => species.Id != "human"), species => Assert.True(species.UsesWildTerritory));
        var human = catalog.GetRequired("human");
        Assert.Equal(3.0, human.MovementSpeed);
        Assert.Equal(0.2, human.HungerRate);
        Assert.Equal(new[] { ItemIds.Food, ItemIds.Plant, ItemIds.Fruit, ItemIds.Meat }, human.Diet);
        Assert.True(human.CanHaul);
        Assert.Equal("humanoid", human.BodyCategory);
        Assert.Equal(DietaryProfile.Omnivore, human.DietaryProfile);
        Assert.Equal(100, human.MaxHealth);
        Assert.Equal(1.0, human.Strength);
        Assert.Equal(1.0, human.Endurance);
        Assert.Equal(1.0, human.Dexterity);
        Assert.Equal(1.0, human.SmellRange);
        Assert.Equal(0, human.ThreatDetectionRange);
        Assert.True(human.CanUseTools);
        Assert.True(human.CanOpenDoors);
        Assert.False(human.CanTrack);
        Assert.False(human.UsesWildTerritory);
        Assert.True(human.CanBePlayerCharacter);
        Assert.True(human.CanJoinPlayerSquad);
        Assert.True(human.CanHaveWorkSchedule);
        Assert.False(human.CanBeCaptured);
        Assert.False(human.CanBePetted);

        var ape = catalog.GetRequired("ape");
        Assert.Equal(2.4, ape.MovementSpeed);
        Assert.Equal(0.22, ape.HungerRate);
        Assert.Equal(new[] { ItemIds.Plant, ItemIds.Fruit, ItemIds.Meat }, ape.Diet);
        Assert.True(ape.CanHaul);
        Assert.Equal("primate", ape.BodyCategory);
        Assert.Equal(DietaryProfile.Omnivore, ape.DietaryProfile);
        Assert.Equal(95, ape.MaxHealth);
        Assert.Equal(1.1, ape.Strength);
        Assert.Equal(1.0, ape.Endurance);
        Assert.Equal(1.4, ape.Dexterity);
        Assert.Equal(1.2, ape.SmellRange);
        Assert.True(ape.CanUseTools);
        Assert.True(ape.CanOpenDoors);
        Assert.True(ape.CanTrack);
        Assert.True(ape.UsesWildTerritory);
        Assert.False(ape.CanBePlayerCharacter);
        Assert.False(ape.CanJoinPlayerSquad);
        Assert.False(ape.CanHaveWorkSchedule);
        Assert.True(ape.CanBeCaptured);
        Assert.True(ape.CanBePetted);

        var cow = catalog.GetRequired("cow");
        Assert.Equal(1.8, cow.MovementSpeed);
        Assert.Equal(0.28, cow.HungerRate);
        Assert.Equal(new[] { ItemIds.Plant, ItemIds.Fruit }, cow.Diet);
        Assert.True(cow.CanHaul);
        Assert.Equal("ungulate", cow.BodyCategory);
        Assert.Equal(DietaryProfile.Herbivore, cow.DietaryProfile);
        Assert.Equal(120, cow.MaxHealth);
        Assert.Equal(1.4, cow.Strength);
        Assert.Equal(1.3, cow.Endurance);
        Assert.Equal(0.5, cow.Dexterity);
        Assert.Equal(1.4, cow.SmellRange);
        Assert.Equal(18, cow.ThreatDetectionRange);
        Assert.Equal(300, cow.CarcassFreshnessSeconds);
        Assert.False(cow.CanUseTools);
        Assert.False(cow.CanOpenDoors);
        Assert.False(cow.CanTrack);
        Assert.True(cow.UsesWildTerritory);

        var wolf = catalog.GetRequired("wolf");
        Assert.Equal(3.4, wolf.MovementSpeed);
        Assert.Equal(0.24, wolf.HungerRate);
        Assert.Equal(new[] { ItemIds.Meat }, wolf.Diet);
        Assert.False(wolf.CanHaul);
        Assert.Equal("canid", wolf.BodyCategory);
        Assert.Equal(DietaryProfile.Carnivore, wolf.DietaryProfile);
        Assert.Equal(85, wolf.MaxHealth);
        Assert.Equal(1.0, wolf.Strength);
        Assert.Equal(1.1, wolf.Endurance);
        Assert.Equal(0.8, wolf.Dexterity);
        Assert.Equal(30.0, wolf.SmellRange);
        Assert.False(wolf.CanUseTools);
        Assert.False(wolf.CanOpenDoors);
        Assert.True(wolf.CanTrack);
        Assert.True(wolf.UsesWildTerritory);

        Assert.Equal(new[] { "human", "creature" },
            catalog.GetCompatibleWithDiet(ItemIds.Food).Select(species => species.Id));
        Assert.Equal(new[] { "human", "ape", "cow" }, catalog.GetHaulers().Select(species => species.Id));
        Assert.Empty(catalog.GetCompatibleWithDiet(ItemIds.Wood));
        Assert.True(catalog.GetRequired("creature").CanAcceptFriendlyInteraction);
        Assert.True(ape.CanAcceptFriendlyInteraction);
        Assert.False(wolf.CanAcceptFriendlyInteraction);
        Assert.True(wolf.SmellRange > cow.SmellRange);
        Assert.Equal(DietaryProfile.Herbivore, cow.DietaryProfile);
        Assert.Equal(DietaryProfile.Carnivore, wolf.DietaryProfile);
        Assert.Equal(new TemperamentProfile(0.75, 0.55, 0.3, 0.9, 0.8, 0.65), ape.Temperament);
        Assert.NotEqual(ape.Temperament, cow.Temperament);
    }

    [Fact]
    public void LoadsSemanticInteractionAnchorsForHumanoidsAndCreatures()
    {
        var catalog = SpeciesCatalog.LoadFromFile(GetSpeciesDataPath());

        var human = catalog.GetRequired("human");
        Assert.Contains(InteractionAnchorIds.Mouth, human.Anchors.InteractionAnchors);
        Assert.Contains(InteractionAnchorIds.Vaginal, human.Anchors.BreedingAnchors);
        Assert.Contains(InteractionAnchorIds.Penile, human.Anchors.BreedingAnchors);
        Assert.Contains(InteractionAnchorIds.BreastLeft, human.Anchors.BreedingAnchors);
        Assert.Contains(InteractionAnchorIds.HandRight, human.Anchors.EquipmentAnchors);
        Assert.True(human.Anchors.SupportsBreeding(InteractionAnchorIds.Genital));

        var cow = catalog.GetRequired("cow");
        Assert.Contains(InteractionAnchorIds.Udder, cow.Anchors.InteractionAnchors);
        Assert.Contains(InteractionAnchorIds.TeatLeft, cow.Anchors.BreedingAnchors);

        var wolf = catalog.GetRequired("wolf");
        Assert.Contains("anchor_scent_gland", wolf.Anchors.InteractionAnchors);
        Assert.Empty(wolf.Anchors.EquipmentAnchors);
    }

    [Fact]
    public void RejectsBreedingAnchorThatIsNotAnInteractionAnchor()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Load("""
                [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"interactionAnchors":["anchor_genital"],"breedingAnchors":["anchor_vaginal"],"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
                """));

        Assert.Contains("must also be declared as an interaction anchor", exception.Message);
    }

    [Theory]
    [InlineData("locomotionAnchors")]
    [InlineData("equipmentAnchors")]
    [InlineData("interactionAnchors")]
    [InlineData("breedingAnchors")]
    public void RejectsAnchorPrefixWithoutSemanticNameInEveryCollection(string fieldName)
    {
        // Test both the bare prefix and a suffix containing only separators.
        foreach (var invalidId in new[] { "anchor_", "anchor___" })
        {
            var json = $$$"""
                [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"{{{fieldName}}}":["{{{invalidId}}}"],"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
                """;

            var exception = Assert.Throws<InvalidDataException>(() => Load(json));

            Assert.Contains("Species 'human' has invalid interaction anchors", exception.Message);
            Assert.Contains("lowercase anchor_ naming convention", exception.Message);
        }
    }

    [Theory]
    [InlineData("anchor_a")]
    [InlineData("anchor_1")]
    [InlineData("anchor_teat_front_l")]
    public void AcceptsNamedSpeciesExtensionAnchors(string anchorId)
    {
        var json = $$$"""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"interactionAnchors":["{{{anchorId}}}"],"breedingAnchors":["{{{anchorId}}}"],"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """;

        var species = Load(json).GetRequired("human");

        Assert.True(species.Anchors.SupportsInteraction(anchorId));
        Assert.True(species.Anchors.SupportsBreeding(anchorId));
        Assert.False(species.Anchors.SupportsInteraction(null));
        Assert.False(species.Anchors.SupportsBreeding("anchor_missing"));
    }

    [Fact]
    public void LegacySpeciesWithoutAnchorCollectionsRemainCompatible()
    {
        var species = Load("""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """).GetRequired("human");

        Assert.Empty(species.Anchors.AnatomyRegions);
        Assert.Empty(species.Anchors.InteractionAnchors);
        Assert.Empty(species.Anchors.BreedingAnchors);
    }

    [Fact]
    public void UsesDocumentedDefaultsForOmittedOptionalCapabilityFields()
    {
        var species = Load("""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """).GetRequired("human");

        Assert.Equal("unspecified", species.BodyCategory);
        Assert.Equal(DietaryProfile.Herbivore, species.DietaryProfile);
        Assert.Equal(100, species.MaxHealth);
        Assert.Equal(1, species.Strength);
        Assert.Equal(1, species.Endurance);
        Assert.Equal(1, species.Dexterity);
        Assert.Equal(1, species.SmellRange);
        Assert.False(species.CanUseTools);
        Assert.False(species.CanOpenDoors);
        Assert.False(species.CanTrack);
        Assert.False(species.UsesWildTerritory);
        Assert.False(species.CanBePlayerCharacter);
        Assert.False(species.CanJoinPlayerSquad);
        Assert.False(species.CanHaveWorkSchedule);
        Assert.False(species.CanBeCaptured);
        Assert.False(species.CanBePetted);
        Assert.Equal(300, species.CarcassFreshnessSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonpositiveCarcassFreshness(double seconds)
    {
        var json = $$$"""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"carcassFreshnessSeconds":{{{seconds}}},"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """;

        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.Contains("carcassFreshnessSeconds must be finite and greater than zero", exception.Message);
    }

    [Theory]
    [InlineData("unknown", "Species 'unknown' is not defined.")]
    [InlineData("", "A species ID is required.")]
    public void RequiredLookupRejectsInvalidSpeciesReferences(string speciesId, string expectedMessage)
    {
        var catalog = Load("""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """);

        var exception = Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired(speciesId));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonpositiveMovementSpeed(double movementSpeed)
    {
        var json = $$$"""
            [{"id":"human","movementSpeed":{{{movementSpeed}}},"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """;

        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.Contains("movementSpeed must be finite and greater than zero", exception.Message);
    }

    [Theory]
    [InlineData("maxHealth", 0)]
    [InlineData("maxHealth", -1)]
    [InlineData("strength", 0)]
    [InlineData("strength", -1)]
    [InlineData("endurance", 0)]
    [InlineData("endurance", -1)]
    [InlineData("dexterity", 0)]
    [InlineData("dexterity", -1)]
    [InlineData("smellRange", 0)]
    [InlineData("smellRange", -1)]
    public void RejectsNonpositiveCapabilityNumericValues(string fieldName, double invalidValue)
    {
        var json = $$$"""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"{{{fieldName}}}":{{{invalidValue}}},"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """;

        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.Contains($"Species 'human' {fieldName} must be finite and greater than zero", exception.Message);
    }

    [Fact]
    public void RejectsUnknownDietItemIds()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"creature","movementSpeed":2,"hungerRate":0.2,"diet":["grain"],"canHaul":false,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]"""));

        Assert.Contains("references unknown diet item ID 'grain'", exception.Message);
    }

    [Fact]
    public void RejectsDuplicateSpeciesIds()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Load("""
                [
                  {"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}},
                  {"id":"human","movementSpeed":2,"hungerRate":0.2,"diet":["food"],"canHaul":false,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}
                ]
                """));

        Assert.Contains("Species ID 'human' is defined more than once", exception.Message);
    }

    [Fact]
    public void RejectsMissingRequiredFieldsAndInvalidHungerRates()
    {
        var missingField = Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"human","hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]"""));
        var invalidHunger = Assert.Throws<InvalidDataException>(() =>
            Load("""[{"id":"human","movementSpeed":3,"hungerRate":-0.1,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]"""));

        Assert.Contains("must define movementSpeed", missingField.Message);
        Assert.Contains("hungerRate must be finite and nonnegative", invalidHunger.Message);
    }

    [Theory]
    [InlineData("""{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5}""", "must define temperament.territoriality")]
    [InlineData("""{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":"unknown","sociability":0.5,"territoriality":0.5}""", "valid species records")]
    [InlineData("""{"boldness":"NaN","aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}""", "temperament.boldness must be finite")]
    [InlineData("""{"boldness":"Infinity","aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}""", "temperament.boldness must be finite")]
    [InlineData("""{"boldness":-0.1,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}""", "temperament.boldness must be finite")]
    [InlineData("""{"boldness":1.1,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}""", "temperament.boldness must be finite")]
    public void RejectsInvalidTemperamentWithoutPublishingPartialCatalog(string temperamentJson, string expectedMessage)
    {
        var json = $$$"""
            [
              {"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}},
              {"id":"creature","movementSpeed":2,"hungerRate":0.2,"diet":["food"],"canHaul":false,"temperament":{{{temperamentJson}}}}
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

    [Theory]
    [InlineData("aggression", "-0.1", "finite")]
    [InlineData("aggression", "1.1", "finite")]
    [InlineData("aggression", "\"NaN\"", "finite")]
    [InlineData("fear", "-0.1", "finite")]
    [InlineData("fear", "1.1", "finite")]
    [InlineData("fear", "\"Infinity\"", "finite")]
    [InlineData("territoriality", "-0.1", "finite")]
    [InlineData("territoriality", "1.1", "finite")]
    [InlineData("territoriality", "\"NaN\"", "finite")]
    public void RejectsInvalidNewTemperamentValues(string fieldName, string invalidValue, string errorText)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["boldness"] = "0.5",
            ["aggression"] = "0.5",
            ["fear"] = "0.5",
            ["curiosity"] = "0.5",
            ["sociability"] = "0.5",
            ["territoriality"] = "0.5"
        };
        values[fieldName] = invalidValue;
        var temperament = "{" + string.Join(",", values.Select(pair => $"\"{pair.Key}\":{pair.Value}")) + "}";
        var json = $$$"""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{{{temperament}}}}]
            """;

        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.Contains($"temperament.{fieldName} must be {errorText}", exception.Message);
    }

    [Theory]
    [InlineData("aggression")]
    [InlineData("fear")]
    [InlineData("territoriality")]
    public void RejectsMissingNewTemperamentValues(string missingField)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["boldness"] = "0.5",
            ["aggression"] = "0.5",
            ["fear"] = "0.5",
            ["curiosity"] = "0.5",
            ["sociability"] = "0.5",
            ["territoriality"] = "0.5"
        };
        values.Remove(missingField);
        var temperament = "{" + string.Join(",", values.Select(pair => $"\"{pair.Key}\":{pair.Value}")) + "}";
        var json = $$$"""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{{{temperament}}}}]
            """;

        var exception = Assert.Throws<InvalidDataException>(() => Load(json));

        Assert.Contains($"must define temperament.{missingField}", exception.Message);
    }

    [Fact]
    public void AcceptsInclusiveTemperamentBounds()
    {
        var species = Load(
            """[{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0,"aggression":1,"fear":0,"curiosity":1,"sociability":0,"territoriality":1}}]""")
            .GetRequired("human");

        Assert.Equal(new TemperamentProfile(0, 1, 0, 1, 0, 1), species.Temperament);
    }

    [Fact]
    public void MissingFriendlyInteractionCapabilityDefaultsToUnsupported()
    {
        var catalog = Load("""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """);

        Assert.False(catalog.GetRequired("human").CanAcceptFriendlyInteraction);
    }

    [Fact]
    public void MissingThreatDetectionRangeDefaultsToDisabledAndNegativeValuesAreRejected()
    {
        var legacySpecies = Load("""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """).GetRequired("human");
        Assert.Equal(0, legacySpecies.ThreatDetectionRange);

        var exception = Assert.Throws<InvalidDataException>(() => Load("""
            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"threatDetectionRange":-1,"temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]
            """));
        Assert.Contains("threatDetectionRange must be finite and nonnegative", exception.Message);
    }

    [Fact]
    public void RejectsMalformedFriendlyInteractionCapability()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Load("""            [{"id":"human","movementSpeed":3,"hungerRate":0.2,"diet":["food"],"canHaul":true,"canAcceptFriendlyInteraction":"yes","temperament":{"boldness":0.5,"aggression":0.5,"fear":0.5,"curiosity":0.5,"sociability":0.5,"territoriality":0.5}}]"""));

        Assert.Contains("valid species records", exception.Message);
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
