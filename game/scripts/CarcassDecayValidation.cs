using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
	private void RunCarcassDecayValidation()
	{
		try
		{
			_saveSlotPath = ProjectSettings.GlobalizePath("user://carcass-decay-validation-save.json");
			StartSession(PlayerCharacterProfile.Create("Carcass decay validation"), 43,
				"Carcass freshness integration validation.");
			SetProcess(false);
			_playerCharacter.SetPhysicsProcess(false);
			_clock.SetPaused(true);

			var contents = new ResourceContainer("validation-carcass", 3);
			contents.TryAdd(ItemIds.Meat, 3);
			var carcass = _world.AddFoodSource("carcass-validation", new System.Numerics.Vector3(2, 0, 0), contents, 0.2);
			AddResourceMarker(new(TargetKind.Food, carcass.Id), carcass.Position, new Color(0.55f, 0.18f, 0.12f));

			AdvanceSimulationTick(0.1);
			Require(_world.TryGetFoodSource(carcass.Id, out var stillFresh) && stillFresh is not null,
				"carcass remains available before its freshness deadline");
			Require(_resourceMarkers.ContainsKey(new(TargetKind.Food, carcass.Id)),
				"fresh carcass marker remains visible");
			Require(Math.Abs(stillFresh!.FreshnessSecondsRemaining!.Value - 0.1) < 0.000001,
				"freshness advances by simulation time");

			AdvanceSimulationTick(0.1);
			Require(!_world.TryGetFoodSource(carcass.Id, out _), "expired carcass is removed from food selection");
			Require(!_resourceMarkers.ContainsKey(new(TargetKind.Food, carcass.Id)), "expired carcass marker is removed");
			Require(!_world.FoodSources.Any(source => source.Id == carcass.Id), "expired meat cannot remain in the world");

			GD.Print("CARCASS DECAY VALIDATION PASSED");
			if (File.Exists(_saveSlotPath)) File.Delete(_saveSlotPath);
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			if (File.Exists(_saveSlotPath)) File.Delete(_saveSlotPath);
			GD.PushError($"CARCASS DECAY VALIDATION FAILED: {exception}");
			GetTree().Quit(1);
		}
	}
}
