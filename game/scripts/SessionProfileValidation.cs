using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private void RunSessionProfileValidation()
    {
        try
        {
            _playerNameInput.Text = "Survival profile";
            _sexChoice.Select((int)PlayerSex.Male);
            GetNode<Button>("CharacterCreation/Panel/Form/Start")
                .EmitSignal(BaseButton.SignalName.Pressed);
            Require(_world.PlayerProfile?.Sex == PlayerSex.Male
                && _world.TryGetEntity(_world.PlayerEntityId ?? string.Empty, out var createdPlayer)
                && createdPlayer?.Sex == BiologicalSex.Male,
                "character creation stores its male selection on the player entity");
            Require(_world.ResourcePiles.Count(pile => pile.Id.StartsWith("tree-adult-", StringComparison.Ordinal)) == 4,
                "survival profile has four adult trees");
            Require(_world.ResourcePiles.Count(pile => pile.Contents.Counts.ContainsKey(ItemIds.Rock)) == 4,
                "survival profile has four loose rocks");
            Require(WoodTotal() == 48 && !_world.ResourcePiles.Any(pile => pile.Id == "wood-pile-1"),
                "survival profile has tree wood and no free wood pile");

            ResetScenario();
            Require(_world.ResourcePiles.Count(pile => pile.Id.StartsWith("tree-adult-", StringComparison.Ordinal)) == 4
                && WoodTotal() == 48, "reset preserves the survival profile");

            StartSession(PlayerCharacterProfile.Create("Baseline profile"), 42,
                "Baseline profile validation.", survivalGathering: false);
            Require(_world.ResourcePiles.Count == 1 && WoodTotal() == 100
                && _world.ResourcePiles.Single().Id == "wood-pile-1",
                "baseline profile has one deterministic wood pile");

            StartSession(PlayerCharacterProfile.Create("Male model profile", sex: PlayerSex.Male), 44,
                "Male model presentation validation.", survivalGathering: false);
            Require(_world.TryGetEntity(_world.PlayerEntityId ?? string.Empty, out var malePlayer)
                && malePlayer?.Sex == BiologicalSex.Male,
                "male player creation sets the authoritative entity sex");
            var maleNpc = _world.Entities.First(entity => entity.Id != _world.PlayerEntityId
                && entity.SpeciesId == "human" && entity.Sex == BiologicalSex.Male);
            var femaleNpc = _world.Entities.First(entity => entity.Id != _world.PlayerEntityId
                && entity.SpeciesId == "human" && entity.Sex == BiologicalSex.Female);
            Require(_entityViews[maleNpc.Id].PresentationModelAssetPath
                    == "res://assets/humanoids/human/radagon_male_humanoid_rig_v1.glb",
                "male NPC selects the Radagon presentation model");
            Require(_entityViews[femaleNpc.Id].PresentationModelAssetPath
                    == "res://assets/humanoids/human/human_humanoid_rig_v1.glb",
                "female NPC keeps the existing humanoid presentation model");
            GD.Print("SESSION PROFILE VALIDATION PASSED: new-game and baseline resources remain explicit; reset preserves the selected profile.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"SESSION PROFILE VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }
}
