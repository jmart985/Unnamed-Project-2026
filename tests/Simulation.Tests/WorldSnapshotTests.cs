using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Simulation;

namespace Simulation.Tests;

public sealed class WorldSnapshotTests
{
    [Fact]
    public void SnapshotCapturePreservesDurableWorldStateAndCarriedWood()
    {
        var world = new World();
        var sourceContents = new ResourceContainer("food-pile", 10);
        sourceContents.TryAdd(ItemIds.Food, 7);
        sourceContents.TryAdd(ItemIds.Wood, 2);
        world.AddFoodSource("food-pile", new Vector3(2, 0, 3), sourceContents);
        var entity = world.SpawnEntity("human", new Vector3(1, 0, -2), 47.5);
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(sourceContents, entity, ItemIds.Wood, 2));
        var snapshot = WorldSnapshotSerializer.Capture(world, 5.5);

        Assert.Equal(5.5, snapshot.SimulationTime);
        Assert.Equal(entity.Id, snapshot.Entities.Single().Id);
        Assert.Equal(new Vector3(1, 0, -2), snapshot.Entities.Single().Position);
        Assert.Equal(47.5, snapshot.Entities.Single().Satiety);
        Assert.Equal(2, snapshot.Entities.Single().Inventory[ItemIds.Wood]);
        Assert.Equal(entity.Sex, snapshot.Entities.Single().Sex);
        Assert.Equal(7, snapshot.FoodSources.Single().Contents.Counts[ItemIds.Food]);
    }

    [Fact]
    public void SnapshotAllowsMoreThan999TotalItemsButRejectsAnOversizedItemStack()
    {
        var source = new World();
        var entity = source.SpawnEntity("human", Vector3.Zero);
        entity.TryAddCarried(ItemIds.Wood, 999);
        entity.TryAddCarried(ItemIds.Food, 999);
        var json = WorldSnapshotSerializer.Serialize(source, 1);

        var restored = new World();
        WorldSnapshotSerializer.Load(restored, json, GetSpeciesCatalog());
        Assert.Equal(1998, restored.Entities.Single().Inventory.Values.Sum());

        var invalid = JsonNode.Parse(json)!;
        invalid["Entities"]![0]!["Inventory"]![ItemIds.Wood] = 1000;
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            new World(), invalid.ToJsonString(), GetSpeciesCatalog()));
    }

    [Fact]
    public void SnapshotRoundTripPreservesSessionSeedAndPlayerCreationChoices()
    {
        var profile = PlayerCharacterProfile.Create(
            "Ada",
            PlayerHeight.Small,
            PlayerBodyType.Thick,
            PlayerSex.Female);
        var source = NewGameSession.Create(GetSpeciesCatalog(), profile, seed: 42);
        var json = WorldSnapshotSerializer.Serialize(source.World, 9);
        var target = new World();

        var restoredTime = WorldSnapshotSerializer.Load(target, json, GetSpeciesCatalog());

        Assert.Equal(9, restoredTime);
        Assert.Equal(42, target.SessionSeed);
        Assert.Equal(source.PlayerEntityId, target.PlayerEntityId);
        Assert.Equal(profile, target.PlayerProfile);
        Assert.Equal(profile.EntitySex, target.Entities.Single(entity => entity.Id == target.PlayerEntityId).Sex);
        Assert.All(target.Entities, entity => Assert.NotEqual(BiologicalSex.Unspecified, entity.Sex));
        Assert.Equal(
            source.Player.Position,
            Assert.Single(target.Entities, entity => entity.Id == target.PlayerEntityId).Position);
        var restoredWorkbench = Assert.Single(target.Workstations);
        Assert.Equal(SettlementRecipes.BasicMealId, restoredWorkbench.CurrentRecipe!.Id);
    }

    [Fact]
    public void SnapshotRoundTripPreservesPlayerHealthDeathTimerAndFacing()
    {
        var source = NewGameSession.Create(GetSpeciesCatalog(), PlayerCharacterProfile.Create("Ada"), seed: 42);
        var head = source.Player.Health.Parts.Single(part => part.Name == "Head");
        source.Player.Health.TryDamage("Head", 10);
        source.World.SetPlayerFacing(Vector3.UnitX);

        var target = new World();
        WorldSnapshotSerializer.Load(target, WorldSnapshotSerializer.Serialize(source.World, 4), GetSpeciesCatalog());

        var restored = target.Entities.Single(entity => entity.Id == target.PlayerEntityId);
        Assert.Equal(head.CurrentHealth, restored.Health.Parts.Single(part => part.Name == "Head").CurrentHealth);
        Assert.Equal(Vector3.UnitX, target.PlayerFacing);
        Assert.False(restored.Health.IsDead);
    }

    [Fact]
    public void Schema22MigrationAssignsPlayerSexFromCreationProfileAndStableSexToOtherEntities()
    {
        var profile = PlayerCharacterProfile.Create("Mara", sex: PlayerSex.Male);
        var source = NewGameSession.Create(GetSpeciesCatalog(), profile, seed: 42);
        var legacy = JsonNode.Parse(WorldSnapshotSerializer.Serialize(source.World, 9))!;
        legacy["SchemaVersion"] = 22;
        foreach (var entity in legacy["Entities"]!.AsArray())
            entity!.AsObject().Remove("Sex");

        var target = new World();
        WorldSnapshotSerializer.Load(target, legacy.ToJsonString(), GetSpeciesCatalog());

        var restoredPlayer = target.Entities.Single(entity => entity.Id == target.PlayerEntityId);
        Assert.Equal(BiologicalSex.Male, restoredPlayer.Sex);
        Assert.All(target.Entities, entity => Assert.NotEqual(BiologicalSex.Unspecified, entity.Sex));

        var roundTrip = new World();
        WorldSnapshotSerializer.Load(roundTrip,
            WorldSnapshotSerializer.Serialize(target, 9), GetSpeciesCatalog());
        Assert.Equal(target.Entities.OrderBy(entity => entity.Id).Select(entity => entity.Sex),
            roundTrip.Entities.OrderBy(entity => entity.Id).Select(entity => entity.Sex));
    }

    [Theory]
    [InlineData("Id", null)]
    [InlineData("Id", "")]
    [InlineData("Id", " ")]
    [InlineData("SpeciesId", null)]
    [InlineData("SpeciesId", "")]
    [InlineData("SpeciesId", " ")]
    public void LegacyMigrationRejectsInvalidIdentifiersWithoutReplacingWorld(string field, string? value)
    {
        var source = new World();
        source.SpawnEntity("human", Vector3.Zero);
        var legacy = JsonNode.Parse(WorldSnapshotSerializer.Serialize(source, 9))!;
        legacy["SchemaVersion"] = 22;
        var entity = legacy["Entities"]![0]!.AsObject();
        entity.Remove("Sex");
        if (value is null)
            entity.Remove(field);
        else
            entity[field] = value;

        var target = NewGameSession.Create(GetSpeciesCatalog(), PlayerCharacterProfile.Create("Existing"));
        var before = WorldSnapshotSerializer.Serialize(target.World, 3);

        Assert.Throws<InvalidDataException>(() =>
            WorldSnapshotSerializer.Load(target.World, legacy.ToJsonString(), GetSpeciesCatalog()));
        Assert.Equal(before, WorldSnapshotSerializer.Serialize(target.World, 3));
        Assert.Same(target.Player, target.World.Entities.Single(item => item.Id == target.PlayerEntityId));
    }

    [Fact]
    public void SnapshotRoundTripPreservesPlayerPendingDeathAndDeadState()
    {
        var source = NewGameSession.Create(GetSpeciesCatalog(), PlayerCharacterProfile.Create("Ada"));
        source.Player.Health.TryDamage("Head", 25);
        var pendingTarget = new World();
        WorldSnapshotSerializer.Load(pendingTarget, WorldSnapshotSerializer.Serialize(source.World, 4), GetSpeciesCatalog());
        var pending = pendingTarget.Entities.Single(entity => entity.Id == pendingTarget.PlayerEntityId);
        Assert.Equal(1, pending.Health.PendingDeathSeconds);
        Assert.False(pending.Health.IsDead);

        var deadSource = NewGameSession.Create(GetSpeciesCatalog(), PlayerCharacterProfile.Create("Ada"));
        deadSource.Player.Health.TryDamage("Torso", 50);
        var deadTarget = new World();
        WorldSnapshotSerializer.Load(deadTarget, WorldSnapshotSerializer.Serialize(deadSource.World, 5), GetSpeciesCatalog());
        Assert.True(deadTarget.Entities.Single(entity => entity.Id == deadTarget.PlayerEntityId).Health.IsDead);
    }

    [Fact]
    public void InvalidPlayerFacingDoesNotReplaceExistingWorld()
    {
        var source = NewGameSession.Create(GetSpeciesCatalog(), PlayerCharacterProfile.Create("Ada"));
        var json = Regex.Replace(
            WorldSnapshotSerializer.Serialize(source.World, 1),
            "\\\"PlayerFacing\\\"\\s*:\\s*\\{[^}]+\\}",
            "\\\"PlayerFacing\\\": { \\\"X\\\": 2, \\\"Y\\\": 0, \\\"Z\\\": 0 }",
            RegexOptions.Singleline);
        var original = NewGameSession.Create(GetSpeciesCatalog(), PlayerCharacterProfile.Create("Existing"));
        var originalPlayer = original.Player;

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(original.World, json, GetSpeciesCatalog()));
        Assert.Same(originalPlayer, original.World.Entities.Single(entity => entity.Id == original.World.PlayerEntityId));
    }

    [Fact]
    public void SnapshotRoundTripPreservesWorkbenchStorageAndProgress()
    {
        var source = NewGameSession.Create(GetSpeciesCatalog(), PlayerCharacterProfile.Create("Ada"));
        var station = source.World.Workstations.Single();
        station.InputStorage.TryAdd(ItemIds.Plant, 1);
        station.InputStorage.TryAdd(ItemIds.Wood, 1);
        station.ApplyWork(3);

        var target = new World();
        WorldSnapshotSerializer.Load(target, WorldSnapshotSerializer.Serialize(source.World, 4), GetSpeciesCatalog());

        var restored = Assert.Single(target.Workstations);
        Assert.Equal(1, restored.InputStorage.Counts[ItemIds.Plant]);
        Assert.Equal(1, restored.InputStorage.Counts[ItemIds.Wood]);
        Assert.Equal(3, restored.CurrentRecipe!.WorkProgress);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(19)]
    [InlineData(20)]
    public void OlderSnapshotsMoveStarterWorkbenchWithoutLosingItsState(int oldSchema)
    {
        var source = NewGameSession.Create(GetSpeciesCatalog(), PlayerCharacterProfile.Create("Ada"));
        var station = source.World.Workstations.Single();
        station.InputStorage.TryAdd(ItemIds.Plant, 1);
        station.InputStorage.TryAdd(ItemIds.Wood, 1);
        station.ApplyWork(3);
        var snapshot = JsonNode.Parse(WorldSnapshotSerializer.Serialize(source.World, 4))!;
        snapshot["SchemaVersion"] = oldSchema;
        snapshot["Workstations"]![0]!["Position"] = new JsonObject { ["X"] = 0, ["Y"] = 0, ["Z"] = 0 };

        var target = new World();
        WorldSnapshotSerializer.Load(target, snapshot.ToJsonString(), GetSpeciesCatalog());

        var restored = Assert.Single(target.Workstations);
        Assert.Equal(NewGameSession.TownWorkbenchPosition, restored.Position);
        Assert.Equal(1, restored.InputStorage.Counts[ItemIds.Plant]);
        Assert.Equal(1, restored.InputStorage.Counts[ItemIds.Wood]);
        Assert.Equal(3, restored.CurrentRecipe!.WorkProgress);
        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion,
            WorldSnapshotSerializer.Deserialize(WorldSnapshotSerializer.Serialize(target, 4)).SchemaVersion);
    }

    [Fact]
    public void SnapshotRoundTripPreservesFarmCropAndGrowth()
    {
        var source = NewGameSession.Create(GetSpeciesCatalog(), PlayerCharacterProfile.Create("Ada"));
        var plot = source.World.Farms.Single().Plots.Single();
        plot.TryPlant(SettlementCrops.CreateFieldCrop(), 1);
        plot.AdvanceGrowth(3);

        var target = new World();
        WorldSnapshotSerializer.Load(target, WorldSnapshotSerializer.Serialize(source.World, 4), GetSpeciesCatalog());

        var restored = Assert.Single(target.Farms).Plots.Single();
        Assert.Equal(SettlementCrops.FieldCropId, restored.Crop!.Id);
        Assert.Equal(3, restored.Growth);
    }

    [Fact]
    public void SnapshotRoundTripPreservesConstructionProgressAndCompletedDoorState()
    {
        var catalog = GetSpeciesCatalog();
        var source = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Builder"));
        var blueprint = SettlementConstruction.CreateHutBlueprint();
        blueprint.ApplyProgress(ItemIds.Wood, 3);
        source.World.AddConstructionSite("hut-site", new Vector3(30, 0, 30), blueprint);
        var hut = source.World.AddStructure("old-hut", StructureType.Hut, new Vector3(40, 0, 40));
        hut.IsClosed = true;

        var target = new World();
        WorldSnapshotSerializer.Load(target, WorldSnapshotSerializer.Serialize(source.World, 4), catalog);

        var restoredSite = Assert.Single(target.ConstructionSites);
        Assert.Equal(3, restoredSite.Blueprint.GetProgress(ItemIds.Wood));
        var restoredHut = Assert.Single(target.Structures);
        Assert.Equal("old-hut", restoredHut.Id);
        Assert.True(restoredHut.IsClosed);
    }

    [Fact]
    public void InvalidConstructionProgressDoesNotReplaceWorld()
    {
        var catalog = GetSpeciesCatalog();
        var source = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Builder"));
        var blueprint = SettlementConstruction.CreateHutBlueprint();
        blueprint.ApplyProgress(ItemIds.Wood, 3);
        source.World.AddConstructionSite("hut-site", new Vector3(30, 0, 30), blueprint);
        var json = WorldSnapshotSerializer.Serialize(source.World, 4)
            .Replace("\"wood\": 3", "\"wood\": 9", StringComparison.Ordinal);

        var target = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Existing"));
        var existingPlayer = target.Player;
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(target.World, json, catalog));
        Assert.Same(existingPlayer, target.World.Entities.Single(entity => entity.Id == existingPlayer.Id));
        Assert.Empty(target.World.ConstructionSites);
    }

    [Fact]
    public void InvalidSessionMetadataDoesNotReplaceExistingWorld()
    {
        var existing = NewGameSession.Create(
            GetSpeciesCatalog(),
            PlayerCharacterProfile.Create("Existing"),
            seed: 10);
        var originalPlayer = existing.Player;
        var validSnapshot = WorldSnapshotSerializer.Serialize(existing.World, 0);
        var invalidSnapshot = validSnapshot.Replace(
            "\"PlayerEntityId\": \"entity-000014\"",
            "\"PlayerEntityId\": \"missing-player\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            existing.World,
            invalidSnapshot,
            GetSpeciesCatalog()));

        Assert.Same(originalPlayer, existing.World.Entities.Single(entity => entity.Id == originalPlayer.Id));
        Assert.Equal("Existing", existing.World.PlayerProfile!.Name);
        Assert.Equal(10, existing.World.SessionSeed);
    }

    [Fact]
    public void SnapshotIncludesEntityInventoryAndExcludesTransientActionState()
    {
        var world = new World();
        var sourceContents = new ResourceContainer("wood-pile", 10);
        sourceContents.TryAdd(ItemIds.Wood, 4);
        world.AddFoodSource("wood-pile", Vector3.Zero, sourceContents);
        var entity = world.SpawnEntity("human", Vector3.One);
        Assert.Equal(
            ResourceMutationResult.Success,
            ResourceTransferService.TryPickUp(sourceContents, entity, ItemIds.Wood, 3));
        entity.Actions.TryStart(new ActionCandidate("haul-1", ActionKind.Haul));

        var json = WorldSnapshotSerializer.Serialize(world, 12.3);
        var snapshot = WorldSnapshotSerializer.Deserialize(json);

        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, snapshot.SchemaVersion);
        Assert.Equal(12.3, snapshot.SimulationTime);
        Assert.Equal(entity.Id, snapshot.Entities.Single().Id);
        Assert.Equal(Vector3.One, snapshot.Entities.Single().Position);
        Assert.Equal(3, snapshot.Entities.Single().Inventory[ItemIds.Wood]);
        Assert.Contains("wood-pile", json);
        Assert.DoesNotContain("haul-1", json);
    }

    [Fact]
    public void SnapshotRejectsUnsupportedVersionsAndInvalidSimulationTime()
    {
        var world = new World();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorldSnapshotSerializer.Capture(world, double.NaN));

        var json = WorldSnapshotSerializer.Serialize(world, 0)
            .Replace(
                $"\"SchemaVersion\": {WorldSnapshotSerializer.CurrentSchemaVersion}",
                "\"SchemaVersion\": 99",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Deserialize(json));
    }

    [Fact]
    public void LoadAppliesValidSnapshotAfterCompleteValidation()
    {
        var sourceWorld = new World();
        var contents = new ResourceContainer("food-pile", 10);
        contents.TryAdd(ItemIds.Food, 4);
        sourceWorld.AddFoodSource("food-pile", new Vector3(3, 0, 2), contents);
        var sourceEntity = sourceWorld.SpawnEntity("creature", new Vector3(1, 0, -2), 42);
        var json = WorldSnapshotSerializer.Serialize(sourceWorld, 8.5);

        var targetWorld = new World();
        targetWorld.SpawnEntity("human", Vector3.Zero, 90);

        var simulationTime = WorldSnapshotSerializer.Load(
            targetWorld,
            json,
            GetSpeciesCatalog());

        Assert.Equal(8.5, simulationTime);
        var restored = Assert.Single(targetWorld.Entities);
        Assert.Equal(sourceEntity.Id, restored.Id);
        Assert.Equal("creature", restored.SpeciesId);
        Assert.Equal(new Vector3(1, 0, -2), restored.Position);
        Assert.Equal(42, restored.Satiety);
        Assert.Equal(4, targetWorld.FoodSources.Single().Contents.Counts[ItemIds.Food]);
        Assert.Equal("entity-000002", targetWorld.SpawnEntity("human", Vector3.One).Id);
    }

    [Fact]
    public void CarcassFreshnessRoundTripsAndSchema17FoodRemainsNonperishable()
    {
        var sourceWorld = new World();
        var carcassContents = new ResourceContainer("carcass-cow", 6);
        carcassContents.TryAdd(ItemIds.Meat, 6);
        sourceWorld.AddFoodSource("carcass-cow", Vector3.Zero, carcassContents, 123.5);

        var currentJson = WorldSnapshotSerializer.Serialize(sourceWorld, 2);
        var currentSnapshot = WorldSnapshotSerializer.Deserialize(currentJson);
        Assert.Equal(123.5, currentSnapshot.FoodSources.Single().FreshnessSecondsRemaining);

        var legacyDocument = System.Text.Json.Nodes.JsonNode.Parse(currentJson)!.AsObject();
        legacyDocument["SchemaVersion"] = 17;
        legacyDocument["FoodSources"]![0]!.AsObject().Remove("FreshnessSecondsRemaining");
        var legacyJson = legacyDocument.ToJsonString();
        var legacySnapshot = WorldSnapshotSerializer.Deserialize(legacyJson);
        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion, legacySnapshot.SchemaVersion);
        Assert.Null(legacySnapshot.FoodSources.Single().FreshnessSecondsRemaining);

        var restoredWorld = new World();
        WorldSnapshotSerializer.Load(restoredWorld, currentJson, GetSpeciesCatalog());
        Assert.Equal(123.5, restoredWorld.FoodSources.Single().FreshnessSecondsRemaining);
        WorldSnapshotSerializer.Load(restoredWorld, legacyJson, GetSpeciesCatalog());
        Assert.Null(restoredWorld.FoodSources.Single().FreshnessSecondsRemaining);
        Assert.Equal(6, restoredWorld.FoodSources.Single().Contents.Counts[ItemIds.Meat]);
    }

    [Fact]
    public void InvalidSnapshotLeavesExistingWorldUnchanged()
    {
        var world = CreatePopulatedWorld();
        var originalEntity = Assert.Single(world.Entities);
        var originalPosition = originalEntity.Position;
        var originalSatiety = originalEntity.Satiety;
        var originalFood = world.FoodSources.Single().Contents.Counts[ItemIds.Food];
        var validJson = WorldSnapshotSerializer.Serialize(world, 3);

        var invalidSnapshots = new[]
        {
            "{",
            validJson.Replace(
                $"\"SchemaVersion\": {WorldSnapshotSerializer.CurrentSchemaVersion}",
                "\"SchemaVersion\": 99",
                StringComparison.Ordinal),
            validJson.Replace("\"SpeciesId\": \"human\"", "\"SpeciesId\": \"unknown\"", StringComparison.Ordinal),
            validJson.Replace("\"food\": 3", "\"food\": -1", StringComparison.Ordinal)
        };

        foreach (var invalidJson in invalidSnapshots)
        {
            Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
                world,
                invalidJson,
                GetSpeciesCatalog()));

            Assert.Equal(originalPosition, originalEntity.Position);
            Assert.Equal(originalSatiety, originalEntity.Satiety);
            Assert.Equal(originalFood, world.FoodSources.Single().Contents.Counts[ItemIds.Food]);
            Assert.Same(originalEntity, Assert.Single(world.Entities));
        }
    }

    [Fact]
    public void LoadRebuildsTransientHaulingAndDeliversCarriedWoodFirst()
    {
        var world = new World();
        var source = new ResourceContainer("wood-source", 10);
        Assert.Equal(ResourceMutationResult.Success, source.TryAdd(ItemIds.Wood, 4));
        world.AddResourcePile("wood-source", Vector3.Zero, source);
        world.AddStockpile("stockpile", new Vector3(2, 0, 0), new ResourceContainer("stockpile", 20));
        var entity = world.SpawnEntity("human", Vector3.Zero, 80);
        var navigator = new FakeNavigationAdapter();
        var hauling = new WoodHaulingService(world, _ => navigator);
        var species = GetSpeciesCatalog().GetRequired("human");

        Assert.Equal(WoodHaulingStatus.TravelingToSource, hauling.Advance(entity, species, 0).Status);
        Assert.True(navigator.CompleteAsArrived(navigator.Requests.Single().Key));
        Assert.Equal(WoodHaulingStatus.TravelingToStockpile, hauling.Advance(entity, species, 1).Status);
        var json = WorldSnapshotSerializer.Serialize(world, 0.2);

        var persisted = WorldSnapshotSerializer.Deserialize(json);
        Assert.Single(persisted.Stockpiles);

        var restoredTime = WorldSnapshotSerializer.Load(world, json, GetSpeciesCatalog(), hauling);
        var restored = Assert.Single(world.Entities);
        Assert.Null(restored.Actions.Current);
        Assert.DoesNotContain(NavigationOutcome.InProgress, navigator.Requests.Values);
        Assert.Equal(0.2, restoredTime);
        Assert.Equal(4, restored.Inventory[ItemIds.Wood]);
        Assert.Equal(20, world.Stockpiles.Single().Contents.Capacity);
        Assert.Equal(0, world.Stockpiles.Single().Contents.UsedCapacity);

        var resumed = hauling.Advance(restored, species, 2);
        Assert.True(
            resumed.Status == WoodHaulingStatus.TravelingToStockpile,
            $"Expected hauling to resume toward storage, but got {resumed.Status}: {resumed.WaitingReason}");
        Assert.Equal("stockpile", resumed.Stockpile?.Id);
        Assert.Equal(0, world.ResourcePiles.Single().Contents.Counts.GetValueOrDefault(ItemIds.Wood));

        var deliveryRequest = navigator.Requests.Single(pair => pair.Value == NavigationOutcome.InProgress).Key;
        Assert.True(navigator.CompleteAsArrived(deliveryRequest));
        Assert.True(world.TrySetEntityPosition(restored.Id, world.Stockpiles.Single().Position));
        Assert.Equal(WoodHaulingStatus.Delivered, hauling.Advance(restored, species, 3).Status);
        Assert.Empty(restored.Inventory);
        Assert.Equal(4, world.Stockpiles.Single().Contents.Counts[ItemIds.Wood]);
        Assert.Equal(0, world.ResourcePiles.Single().Contents.Counts.GetValueOrDefault(ItemIds.Wood));
    }

    [Fact]
    public void DuplicateEntityIdsAreRejectedBeforeWorldMutation()
    {
        var world = CreatePopulatedWorld();
        world.SpawnEntity("creature", Vector3.One, 45);
        var sourceSnapshot = WorldSnapshotSerializer.Serialize(world, 3);
        var duplicateId = sourceSnapshot.Replace(
            "\"Id\": \"entity-000002\"",
            "\"Id\": \"entity-000001\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            world,
            duplicateId,
            GetSpeciesCatalog()));
        Assert.Equal(2, world.Entities.Count);
    }

    private static World CreatePopulatedWorld()
    {
        var world = new World();
        var contents = new ResourceContainer("food-pile", 10);
        contents.TryAdd(ItemIds.Food, 3);
        world.AddFoodSource("food-pile", Vector3.UnitX, contents);
        world.SpawnEntity("human", Vector3.Zero, 55);
        return world;
    }

    private static SpeciesCatalog GetSpeciesCatalog()
    {
        return SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    }
}
