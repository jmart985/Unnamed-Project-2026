using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunWildTerritoryValidation()
    {
        try
        {
            _saveSlotPath = ProjectSettings.GlobalizePath("user://wild-territory-validation-save.json");
            StartSession(PlayerCharacterProfile.Create("Wild territory validation"), 42,
                "Wild territory integration validation.");
            SetProcess(false);
            _playerCharacter.SetPhysicsProcess(false);
            _clock.SetPaused(true);
            await PhysicsFrames(4);

            var wolf = _world.Entities.Single(entity => entity.SpeciesId == "wolf");
            var species = _speciesCatalog.GetRequired(wolf.SpeciesId);
            Require(_world.TryGetWildTerritory(wolf.Id, out var territory) && territory is not null,
                "starter wolf has a durable home territory");
            var home = territory!;
            var view = _entityViews[wolf.Id];

            // Isolate territory behavior from the intentionally hungry starter state.
            wolf.Needs.AddNutrition(100);
            _world.TrySetEntityPosition(wolf.Id, home.Center);
            view.GlobalPosition = ToGodot(home.Center);
            var roaming = _wandering!.Advance(wolf, species, simulationTick: 1);
            Require(roaming.Status == WanderingStatus.Traveling
                && roaming.Target is { } roamTarget && home.Contains(roamTarget),
                "wild roaming destination stays inside home territory");

            var displaced = home.Center + new NumericsVector3(home.Radius + 2, 0, 0);
            _world.TrySetEntityPosition(wolf.Id, displaced);
            view.GlobalPosition = ToGodot(displaced);
            var returning = _wandering.Advance(wolf, species, simulationTick: 2);
            Require(returning.Status == WanderingStatus.ReturningToTerritory
                && returning.Target == home.Center,
                "displaced wild wolf selects ReturnToArea and targets its home");

            var returned = false;
            for (var frame = 0; frame < 120; frame++)
            {
                await PhysicsFrames(1);
                AdvanceNavigationTick(0.1);
                _simulationTick++;
                var result = _wandering.Advance(wolf, species, _simulationTick);
                if (result.Status == WanderingStatus.ReturnedToTerritory)
                {
                    returned = true;
                    break;
                }
            }

            Require(returned && home.Contains(wolf.Position),
                $"Godot navigation returned wolf to its home (position {wolf.Position})");
            var resumedRoam = _wandering.Advance(wolf, species, _simulationTick + 1);
            Require(resumedRoam.Status == WanderingStatus.Traveling
                && resumedRoam.Target is { } resumedTarget && home.Contains(resumedTarget),
                "normal territory roaming resumes after return");

            GD.Print("WILD TERRITORY VALIDATION PASSED");
            if (File.Exists(_saveSlotPath)) File.Delete(_saveSlotPath);
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            if (File.Exists(_saveSlotPath)) File.Delete(_saveSlotPath);
            GD.PushError($"WILD TERRITORY VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private static Vector3 ToGodot(NumericsVector3 position) =>
        new(position.X, position.Y, position.Z);
}
