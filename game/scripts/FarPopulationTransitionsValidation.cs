using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
	private void RunFarPopulationTransitionsValidation()
	{
		try
		{
			_saveSlotPath = ProjectSettings.GlobalizePath("user://far-population-validation-save.json");
			StartSession(PlayerCharacterProfile.Create("Far population validation"), 44,
				"Far population transition validation.");
			SetProcess(false);
			_playerCharacter.SetPhysicsProcess(false);
			_clock.SetPaused(true);

			var transitions = new NearFarTransitionService(_world, _speciesCatalog);
			transitions.SetFarRecord(new("validation-grove", "cow", 2, 45, new Dictionary<string, int> { [ItemIds.Plant] = 8 }));
			transitions.SetFarRecord(new("validation-grove", "wolf", 1, 35, new Dictionary<string, int> { [ItemIds.Meat] = 3 }));
			var cows = transitions.Promote("validation-grove", "cow", new System.Numerics.Vector3(180, 0, 120));
			Require(cows.Count == 2, "only the selected cow aggregate becomes nearby entities");
			Require(_world.FarPopulations.Single(record => record.RegionId == "validation-grove").SpeciesId == "wolf",
				"the other species remains in the region's far population");
			Require(transitions.Demote("validation-grove", "cow"), "cow entities demote to their own species aggregate");
			Require(_world.FarPopulations.Count(record => record.RegionId == "validation-grove") == 2,
				"demotion preserves distinct cow and wolf aggregates");

			_saveSlotStore.Save(_saveSlotPath,
				() => WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime));
			var restored = new World();
			WorldSnapshotSerializer.Load(restored, _saveSlotStore.Load(_saveSlotPath), _speciesCatalog);
			var grove = restored.FarPopulations.Where(record => record.RegionId == "validation-grove").ToArray();
			Require(grove.Length == 2, "save/load restores one far record per species");
			Require(grove.Single(record => record.SpeciesId == "cow").Count == 2,
				"save/load preserves the cow count without duplication");
			Require(grove.Single(record => record.SpeciesId == "wolf").Count == 1,
				"save/load preserves the wolf count without duplication");
			Require(grove.Single(record => record.SpeciesId == "cow").Resources[ItemIds.Plant] == 8,
				"save/load preserves the cow population resources");
			Require(grove.Single(record => record.SpeciesId == "wolf").Resources[ItemIds.Meat] == 3,
				"save/load preserves the wolf population resources");

			GD.Print("FAR POPULATION TRANSITIONS VALIDATION PASSED");
			if (File.Exists(_saveSlotPath)) File.Delete(_saveSlotPath);
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			if (File.Exists(_saveSlotPath)) File.Delete(_saveSlotPath);
			GD.PushError($"FAR POPULATION TRANSITIONS VALIDATION FAILED: {exception}");
			GetTree().Quit(1);
		}
	}
}
