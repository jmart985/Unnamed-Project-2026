using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
	private async void RunStarterWildlifeProximityValidation()
	{
		GD.Print("STARTER WILDLIFE PROXIMITY VALIDATION STARTED");
		try
		{
			StartSession(PlayerCharacterProfile.Create("Wildlife proximity validation"), 45,
				"Starter wildlife proximity integration validation.", startStarterWildlifeAsFar: true);
			SetProcess(false);
			_playerCharacter.SetPhysicsProcess(false);
			_clock.SetPaused(true);
			await PhysicsFrames(4);

			var region = _wildlifeRegionCatalog.Regions.Single();
			Require(!_world.Entities.Any(entity => entity.SpeciesId is "ape" or "wolf" or "cow"),
				"new session starts with starter wildlife represented only by far population records");
			Require(_world.FarPopulations.Count == 3, "starter region has one durable far record per species");
			ActivateStarterWildlifeForValidation();
			Require(_world.NearPopulations.Count == 3 && _world.FarPopulations.Count == 0,
				"entering the activation radius creates the configured local populations");
			Require(_world.Entities.Count(entity => entity.SpeciesId is "ape" or "wolf" or "cow") == 3,
				"activation creates each configured animal once");
			_wildlifeProximity!.Update(region.Center);
			Require(_world.Entities.Count(entity => entity.SpeciesId is "ape" or "wolf" or "cow") == 3,
				"repeated proximity checks do not duplicate active wildlife");

			var activeSave = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
			WorldSnapshotSerializer.Load(_world, activeSave, _speciesCatalog,
				_foodConsumption!, _creatureHunting!, _wildPreyThreat!, _wandering!, _woodHauling!, _squad!);
			_wildlifeProximity = new WildlifeProximityCoordinator(_world, _speciesCatalog, _wildlifeRegionCatalog);
			RebuildPresentationFromWorld();
			Require(_world.NearPopulations.Count == 3 && _world.Entities.Count(entity => entity.SpeciesId is "ape" or "wolf" or "cow") == 3,
				"save/load retains active region membership and does not create duplicate wildlife");
			_wildlifeProximity.Update(region.Center);
			Require(_world.Entities.Count(entity => entity.SpeciesId is "ape" or "wolf" or "cow") == 3,
				"the restored coordinator recognizes the already-active region");
			await PhysicsFrames(4);

			var wolf = _world.Entities.Single(entity => entity.SpeciesId == "wolf");
			var initialSatiety = wolf.Satiety;
			var completedHuntAndFeed = false;
			for (var tick = 0; tick < 2400; tick++)
			{
				await PhysicsFrames(1);
				AdvanceSimulationTick(0.1);
				if (_world.FoodSources.Any(source => source.Id.StartsWith("carcass-", StringComparison.Ordinal))
					&& _foodConsumption!.FoodUnitsConsumed > 0 && wolf.Satiety > initialSatiety)
				{
					completedHuntAndFeed = true;
					break;
				}
			}
			Require(completedHuntAndFeed, "active wolf hunts local prey, creates a carcass, and eats");
			Require(!_world.Entities.Any(entity => entity.SpeciesId == "cow"), "hunted cow is removed from active simulation");

			var outside = region.Center + new System.Numerics.Vector3(region.DeactivationRadius + 1, 0, 0);
			_playerCharacter.GlobalPosition = new Vector3(outside.X, outside.Y, outside.Z);
			_world.TrySetEntityPosition(_world.PlayerEntityId, outside);
			var deactivated = _wildlifeProximity.Update(outside);
			SyncEntityViewMembership();
			Require(deactivated.ChangedRegionIds.Contains(region.Id), "leaving the larger exit radius demotes the region");
			Require(_world.NearPopulations.Count == 0, "active membership clears after demotion");
			Require(_world.FarPopulations.Single(record => record.SpeciesId == "cow").Count == 0,
				"hunted wildlife does not return to the regional count");
			Require(_world.FarPopulations.Single(record => record.SpeciesId == "ape").Count == 1
				&& _world.FarPopulations.Single(record => record.SpeciesId == "wolf").Count == 1,
				"survivors return to their own species counts");
			Require(!_world.Entities.Any(entity => entity.SpeciesId is "ape" or "wolf" or "cow"),
				"demotion removes active wildlife presentation entities");

			var finalJson = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
			var restored = new World();
			WorldSnapshotSerializer.Load(restored, finalJson, _speciesCatalog);
			Require(restored.FarPopulations.Single(record => record.SpeciesId == "cow").Count == 0
				&& restored.FarPopulations.Single(record => record.SpeciesId == "ape").Count == 1
				&& restored.FarPopulations.Single(record => record.SpeciesId == "wolf").Count == 1,
				"save/load preserves survivor counts and the removed prey count without duplicates");

			GD.Print("STARTER WILDLIFE PROXIMITY VALIDATION PASSED");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"STARTER WILDLIFE PROXIMITY VALIDATION FAILED: {exception}");
			GetTree().Quit(1);
		}
	}
}
