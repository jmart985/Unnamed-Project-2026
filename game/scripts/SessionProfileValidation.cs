using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private void RunSessionProfileValidation()
    {
        try
        {
            var previewViewport = GetNode<SubViewport>(
                "CharacterCreation/Panel/Form/PreviewViewport/SubViewport");
            Require(previewViewport.OwnWorld3D
                && previewViewport.World3D != GetViewport().World3D,
                "character creation preview renders in an isolated 3D world");

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
            var starterNpcs = _world.Entities
                .Where(entity => entity.Id != _world.PlayerEntityId && entity.SpeciesId == "human")
                .ToDictionary(entity => entity.Id, StringComparer.Ordinal);
            Require(starterNpcs.Count == NewGameSession.HumanNpcCount
                && starterNpcs.Values.Count(entity => entity.Sex == BiologicalSex.Male) == 4
                && starterNpcs.Values.Count(entity => entity.Sex == BiologicalSex.Female) == 6,
                "starter NPCs use the configured four-male, six-female mix");
            foreach (var presentation in StarterNpcPresentationCatalog.All)
            {
                Require(starterNpcs.TryGetValue(presentation.EntityId, out var npc)
                    && npc.Sex == presentation.Sex
                    && _entityViews[presentation.EntityId].PresentationModelAssetPath == presentation.AssetPath,
                    $"{presentation.DisplayName} has its matching sex and distinct model");
            }
            Require(!StarterNpcPresentationCatalog.All.Any(model => model.EntityId == _world.PlayerEntityId)
                && _world.TryGetEntity(_world.PlayerEntityId ?? string.Empty, out var maleProfilePlayer)
                && maleProfilePlayer?.Sex == BiologicalSex.Male,
                "player model assignment remains separate from the NPC roster");
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
