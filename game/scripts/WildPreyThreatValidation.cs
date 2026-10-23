using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunWildPreyThreatValidation()
    {
        GD.Print("WILD PREY THREAT VALIDATION STARTED");
        try
        {
            _saveSlotPath = ProjectSettings.GlobalizePath("user://wild-prey-threat-validation-save.json");
            StartSession(PlayerCharacterProfile.Create("Wild prey threat validation"), 42,
                "Wild prey threat-response integration validation.");
            SetProcess(false);
            _playerCharacter.SetPhysicsProcess(false);
            _clock.SetPaused(true);
            await PhysicsFrames(4);
            ActivateStarterWildlifeForValidation();

            var wolf = _world.Entities.Single(entity => entity.SpeciesId == "wolf");
            var cow = _world.Entities.Single(entity => entity.SpeciesId == "cow");
            var wolfStart = new System.Numerics.Vector3(150, 0, 40);
            var cowStart = new System.Numerics.Vector3(164, 0, 40);
            _world.TrySetEntityPosition(wolf.Id, wolfStart);
            _world.TrySetEntityPosition(cow.Id, cowStart);
            _entityViews[wolf.Id].GlobalPosition = ConvertThreatPosition(wolfStart);
            _entityViews[cow.Id].GlobalPosition = ConvertThreatPosition(cowStart);
            var wolfSatietyBeforeHunt = wolf.Satiety;

            AdvanceSimulationTick(0.1);
            var cowView = _entityViews[cow.Id];
            GD.Print($"Prey response: action={cow.Actions.Current?.Kind}, target={cow.TargetId}, navigation={cowView.NavigationTarget}");
            Require(cow.Actions.Current?.Kind == ActionKind.Flee
                && cow.Activity == "Fleeing"
                && cow.TargetId == wolf.Id,
                "wild cow detects the configured wolf threat and starts fleeing");
            Require(cowView.NavigationTarget is { } target && target.X > cowStart.X,
                "the flee navigation target is placed away from the approaching wolf");

            var movedAway = false;
            for (var frame = 0; frame < 40; frame++)
            {
                await PhysicsFrames(1);
                AdvanceSimulationTick(0.1);
                if (cow.Position.X > cowStart.X + 0.2f)
                {
                    movedAway = true;
                    break;
                }
            }
            Require(movedAway, $"cow moves away from the wolf (x {cowStart.X:0.0} -> {cow.Position.X:0.0})");

            var completedHuntAndFeed = false;
            for (var frame = 0; frame < 2400; frame++)
            {
                await PhysicsFrames(1);
                AdvanceSimulationTick(0.1);
                var carcass = _world.FoodSources.FirstOrDefault(source =>
                    source.Id.StartsWith("carcass-", StringComparison.Ordinal));
                if (carcass is not null && _foodConsumption!.FoodUnitsConsumed > 0
                    && wolf.Satiety > wolfSatietyBeforeHunt)
                {
                    completedHuntAndFeed = true;
                    break;
                }
            }

            Require(completedHuntAndFeed,
                "wolf still pursues, harvests, and feeds after the cow's escape response");
            Require(_world.Entities.All(entity => entity.Id != cow.Id),
                "the hunted cow is removed exactly once after the complete predator/prey loop");

            GD.Print("WILD PREY THREAT VALIDATION PASSED");
            if (File.Exists(_saveSlotPath)) File.Delete(_saveSlotPath);
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            if (File.Exists(_saveSlotPath)) File.Delete(_saveSlotPath);
            GD.PushError($"WILD PREY THREAT VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private static Vector3 ConvertThreatPosition(System.Numerics.Vector3 position) =>
        new(position.X, position.Y, position.Z);
}
