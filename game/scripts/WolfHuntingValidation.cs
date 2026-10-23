using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
	private async void RunWolfHuntingValidation()
	{
		try
		{
			_saveSlotPath = ProjectSettings.GlobalizePath("user://wolf-hunting-validation-save.json");
			StartSession(PlayerCharacterProfile.Create("Wolf hunting validation"), 42,
				"Wolf hunting and feeding integration validation.");
			SetProcess(false);
			_playerCharacter.SetPhysicsProcess(false);
			_clock.SetPaused(true);
			await PhysicsFrames(4);

			var wolf = _world.Entities.Single(entity => entity.SpeciesId == "wolf");
			var initialSatiety = wolf.Satiety;
			Require(wolf.Needs.IsHungry, "starter wolf begins hungry");
			Require(_world.FoodSources.All(source => !source.Contents.Counts.ContainsKey(ItemIds.Meat)),
				"no free meat is available before the hunt");

			var completedHuntAndFeed = false;
			for (var frame = 0; frame < 2400; frame++)
			{
				await PhysicsFrames(1);
				AdvanceSimulationTick(0.1);
				var carcass = _world.FoodSources.FirstOrDefault(source =>
					source.Id.StartsWith("carcass-", StringComparison.Ordinal));
				if (carcass is not null && _foodConsumption!.FoodUnitsConsumed > 0 && wolf.Satiety > initialSatiety)
				{
					completedHuntAndFeed = true;
					break;
				}
			}

			Require(completedHuntAndFeed,
				$"wolf hunted, created a carcass, and ate (satiety {initialSatiety:0.0} -> {wolf.Satiety:0.0}; consumed {_foodConsumption!.FoodUnitsConsumed} food units)");
			Require(_world.Entities.All(entity => entity.SpeciesId != "cow"), "hunted prey is removed exactly once");
			Require(_world.FoodSources.Any(source => source.Id.StartsWith("carcass-", StringComparison.Ordinal)),
				"harvested prey leaves its carcass food source in the world");

			GD.Print("WOLF HUNTING VALIDATION PASSED");
			if (File.Exists(_saveSlotPath)) File.Delete(_saveSlotPath);
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			if (File.Exists(_saveSlotPath)) File.Delete(_saveSlotPath);
			GD.PushError($"WOLF HUNTING VALIDATION FAILED: {exception}");
			GetTree().Quit(1);
		}
	}
}
