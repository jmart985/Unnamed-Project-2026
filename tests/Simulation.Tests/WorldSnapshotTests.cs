using System.Numerics;
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
        Assert.Equal(7, snapshot.FoodSources.Single().Contents.Counts[ItemIds.Food]);
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
        Assert.Equal(
            source.Player.Position,
            Assert.Single(target.Entities, entity => entity.Id == target.PlayerEntityId).Position);
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
