using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class WildlifeProximityCoordinatorTests
{
    private static SpeciesCatalog Species => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    private static WildlifeRegionCatalog Regions(SpeciesCatalog species) => WildlifeRegionCatalog.LoadFromFile(
        Path.Combine(AppContext.BaseDirectory, "data", "wildlife_regions.json"), species);

    [Fact]
    public void StarterWildlifeActivatesOnceAndUsesSeparateEntryAndExitThresholds()
    {
        var species = Species;
        var regions = Regions(species);
        var session = NewGameSession.Create(species, PlayerCharacterProfile.Create("Tester"), wildlifeRegions: regions,
            startStarterWildlifeAsFar: true);
        var world = session.World;
        var region = Assert.Single(regions.Regions);
        var coordinator = new WildlifeProximityCoordinator(world, species, regions);
        Assert.DoesNotContain(world.Entities, entity => entity.SpeciesId != "human");
        Assert.Equal(3, world.FarPopulations.Count);

        Assert.Empty(coordinator.Update(Vector3.Zero).ActivatedEntityIds);
        var activated = coordinator.Update(region.Center);
        Assert.Equal(3, activated.ActivatedEntityIds.Count);
        Assert.Equal(3, world.NearPopulations.Count);
        Assert.Empty(world.FarPopulations);
        Assert.Empty(coordinator.Update(region.Center).ActivatedEntityIds);
        Assert.Equal(3, world.Entities.Count(entity => entity.SpeciesId != "human"));

        Assert.Empty(coordinator.Update(region.Center + new Vector3(region.ActivationRadius + 5, 0, 0)).DeactivatedEntityIds);
        var deactivated = coordinator.Update(region.Center + new Vector3(region.DeactivationRadius + 1, 0, 0));
        Assert.Equal(3, deactivated.DeactivatedEntityIds.Count);
        Assert.Empty(world.NearPopulations);
        Assert.Equal(3, world.FarPopulations.Count);
        Assert.Equal(0, world.Entities.Count(entity => entity.SpeciesId != "human"));
    }

    [Fact]
    public void ActiveMembershipSurvivesSaveLoadAndRemovedPreyIsNotCountedAgain()
    {
        var species = Species;
        var regions = Regions(species);
        var session = NewGameSession.Create(species, PlayerCharacterProfile.Create("Tester"), wildlifeRegions: regions,
            startStarterWildlifeAsFar: true);
        var world = session.World;
        var region = Assert.Single(regions.Regions);
        var coordinator = new WildlifeProximityCoordinator(world, species, regions);
        coordinator.Update(region.Center);
        var cow = Assert.Single(world.Entities, entity => entity.SpeciesId == "cow");
        Assert.True(world.TryRemoveEntity(cow.Id));
        Assert.Empty(world.NearPopulations.Single(group => group.SpeciesId == "cow").EntityIds);
        var wolfBeforeSave = Assert.Single(world.Entities, entity => entity.SpeciesId == "wolf");
        Assert.True(wolfBeforeSave.Health.TryDamage("Torso", double.MaxValue));

        var restored = new World();
        WorldSnapshotSerializer.Load(restored, WorldSnapshotSerializer.Serialize(world, 12), species);
        Assert.Empty(restored.FarPopulations);
        Assert.Equal(3, restored.NearPopulations.Count);
        var wolf = Assert.Single(restored.Entities, entity => entity.SpeciesId == "wolf");
        Assert.True(wolf.Health.IsDead);
        var restoredCoordinator = new WildlifeProximityCoordinator(restored, species, regions);
        Assert.Empty(restoredCoordinator.Update(region.Center).ActivatedEntityIds);
        restoredCoordinator.Update(region.Center + new Vector3(region.DeactivationRadius + 1, 0, 0));

        Assert.Equal(0, restored.FarPopulations.Single(record => record.SpeciesId == "cow").Count);
        Assert.Equal(1, restored.FarPopulations.Single(record => record.SpeciesId == "ape").Count);
        Assert.Equal(0, restored.FarPopulations.Single(record => record.SpeciesId == "wolf").Count);
        Assert.Empty(restored.NearPopulations);
        var final = new World();
        WorldSnapshotSerializer.Load(final, WorldSnapshotSerializer.Serialize(restored, 13), species);
        Assert.Equal(0, final.FarPopulations.Single(record => record.SpeciesId == "cow").Count);
        Assert.Equal(1, final.FarPopulations.Single(record => record.SpeciesId == "ape").Count);
    }

    [Fact]
    public void RestoredActiveRegionRecognizesExtinctSpeciesWithZeroCountFarRecord()
    {
        var species = Species;
        var regions = Regions(species);
        var session = NewGameSession.Create(species, PlayerCharacterProfile.Create("Tester"), wildlifeRegions: regions,
            startStarterWildlifeAsFar: true);
        var world = session.World;
        var region = Assert.Single(regions.Regions);
        var coordinator = new WildlifeProximityCoordinator(world, species, regions);
        coordinator.Update(region.Center);

        var cow = Assert.Single(world.Entities, entity => entity.SpeciesId == "cow");
        Assert.True(world.TryRemoveEntity(cow.Id));
        var outside = region.Center + new Vector3(region.DeactivationRadius + 1, 0, 0);
        coordinator.Update(outside);
        Assert.Equal(0, world.FarPopulations.Single(record => record.SpeciesId == "cow").Count);

        coordinator.Update(region.Center);
        Assert.Equal(2, world.NearPopulations.Count);
        Assert.Equal(0, world.FarPopulations.Single(record => record.SpeciesId == "cow").Count);

        var restored = new World();
        WorldSnapshotSerializer.Load(restored, WorldSnapshotSerializer.Serialize(world, 12), species);
        var restoredCoordinator = new WildlifeProximityCoordinator(restored, species, regions);
        restoredCoordinator.Update(outside);

        Assert.Empty(restored.NearPopulations);
        Assert.Equal(1, restored.FarPopulations.Single(record => record.SpeciesId == "ape").Count);
        Assert.Equal(1, restored.FarPopulations.Single(record => record.SpeciesId == "wolf").Count);
        Assert.Equal(0, restored.FarPopulations.Single(record => record.SpeciesId == "cow").Count);
    }

    [Fact]
    public void DemotionPreservesIndividuallySignificantCreaturesAndTheirHistoryAcrossSaveLoad()
    {
        var species = Species;
        var regions = Regions(species);
        var session = NewGameSession.Create(species, PlayerCharacterProfile.Create("Tester"), wildlifeRegions: regions,
            startStarterWildlifeAsFar: true);
        var world = session.World;
        var region = Assert.Single(regions.Regions);
        var coordinator = new WildlifeProximityCoordinator(world, species, regions);
        coordinator.Update(region.Center);
        var player = Assert.Single(world.Entities, entity => entity.Id == session.PlayerEntityId);
        var ape = Assert.Single(world.Entities, entity => entity.SpeciesId == "ape");
        var wolf = Assert.Single(world.Entities, entity => entity.SpeciesId == "wolf");
        Assert.Equal(RelationshipAdjustmentResult.Applied,
            world.ApplyRelationshipAdjustment(ape.Id, player.Id, new(20, 10, 0, 0)));
        Assert.Equal(CreatureCaptureResult.Captured, world.TryCaptureCreature(wolf.Id));

        var outside = region.Center + new Vector3(region.DeactivationRadius + 1, 0, 0);
        coordinator.Update(outside);
        Assert.Empty(world.NearPopulations);
        Assert.True(world.TryGetEntity(ape.Id, out var retainedApe));
        Assert.True(world.TryGetEntity(wolf.Id, out var retainedWolf));
        Assert.Equal(CreatureCaptivityState.Captured, retainedWolf!.CaptivityState);
        Assert.True(world.TryGetRelationship(ape.Id, player.Id, out var relationship));
        Assert.Equal(20, relationship!.Familiarity);
        Assert.Equal(0, world.FarPopulations.Single(record => record.SpeciesId == "ape").Count);
        Assert.Equal(0, world.FarPopulations.Single(record => record.SpeciesId == "wolf").Count);
        Assert.Equal(1, world.FarPopulations.Single(record => record.SpeciesId == "cow").Count);

        var restored = new World();
        WorldSnapshotSerializer.Load(restored, WorldSnapshotSerializer.Serialize(world, 12), species);
        Assert.True(restored.TryGetEntity(ape.Id, out _));
        Assert.True(restored.TryGetEntity(wolf.Id, out var restoredWolf));
        Assert.Equal(CreatureCaptivityState.Captured, restoredWolf!.CaptivityState);
        Assert.True(restored.TryGetRelationship(ape.Id, player.Id, out var restoredRelationship));
        Assert.Equal(20, restoredRelationship!.Familiarity);

        var restoredCoordinator = new WildlifeProximityCoordinator(restored, species, regions);
        var activated = restoredCoordinator.Update(region.Center);
        Assert.Single(activated.ActivatedEntityIds);
        Assert.Single(restored.Entities, entity => entity.SpeciesId == "ape");
        Assert.Single(restored.Entities, entity => entity.SpeciesId == "wolf");
        Assert.Equal(CreatureCaptivityState.Captured,
            Assert.Single(restored.Entities, entity => entity.SpeciesId == "wolf").CaptivityState);
    }

    [Fact]
    public void RejectsInvalidRegionBoundsAndUnknownSpecies()
    {
        var species = Species;
        Assert.Throws<ArgumentException>(() => new WildlifeRegionCatalog(
            [new("starter", Vector3.Zero, 10, 9, [new("cow", 50, [Vector3.Zero])])], species));
        Assert.Throws<ArgumentException>(() => new WildlifeRegionCatalog(
            [new("starter", Vector3.Zero, 10, 12, [new("unknown", 50, [Vector3.Zero])])], species));
        Assert.False(WildlifeRegionCatalog.IsValidId("-bad-region"));
        Assert.False(WildlifeRegionCatalog.IsValidId("bad--region"));
    }

    [Fact]
    public void SnapshotRejectsDuplicateOrMismatchedNearPopulationMembersBeforeReplacingWorld()
    {
        var species = Species;
        var regions = Regions(species);
        var source = NewGameSession.Create(species, PlayerCharacterProfile.Create("Tester"), wildlifeRegions: regions,
            startStarterWildlifeAsFar: true);
        var coordinator = new WildlifeProximityCoordinator(source.World, species, regions);
        coordinator.Update(Assert.Single(regions.Regions).Center);
        var json = System.Text.Json.Nodes.JsonNode.Parse(WorldSnapshotSerializer.Serialize(source.World, 1))!.AsObject();
        var target = new World { SpeciesCatalog = species };
        target.SetFarPopulation(new FarEntityRecord("preserved", "wolf", 1, 50, []));

        var duplicateGroup = System.Text.Json.Nodes.JsonNode.Parse(json.ToJsonString())!.AsObject();
        duplicateGroup["NearPopulations"]!.AsArray().Add(duplicateGroup["NearPopulations"]![0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(target, duplicateGroup.ToJsonString(), species));

        var wrongSpecies = System.Text.Json.Nodes.JsonNode.Parse(json.ToJsonString())!.AsObject();
        wrongSpecies["NearPopulations"]![0]!["SpeciesId"] = "wolf";
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(target, wrongSpecies.ToJsonString(), species));
        Assert.Equal("preserved", Assert.Single(target.FarPopulations).RegionId);
        Assert.Empty(target.Entities);
    }
}
