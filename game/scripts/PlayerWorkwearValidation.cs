using Godot;
using Simulation;

namespace UnnamedProject2026;

internal static class PlayerWorkwearValidation
{
    public static async void Run(
        Main main,
        PlayerCharacter character,
        SpeciesCatalog speciesCatalog)
    {
        try
        {
            character.SetPhysicsProcess(false);
            character.SetProcessUnhandledInput(false);
            foreach (var (sex, expectedPath) in new[]
            {
                (PlayerSex.Male, "res://assets/humanoids/human/player_male_humanoid_rig_v1.glb"),
                (PlayerSex.Female, "res://assets/humanoids/human/player_female_humanoid_rig_v1.glb")
            })
            {
                var profile = PlayerCharacterProfile.Create($"Workwear {sex}", sex: sex);
                var session = NewGameSession.Create(speciesCatalog, profile);
                var player = session.Player;
                character.BindPlayer(
                    player.Id,
                    profile,
                    (float)speciesCatalog.GetRequired(player.SpeciesId).MovementSpeed);
                character.SetEquippedClothing(player.EquippedItems);
                await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);

                Require(character.UsesImportedHumanoidModel,
                    $"{sex} player did not use its imported, skinned model");
                Require(character.PresentationModelAssetPath == expectedPath,
                    $"{sex} player did not select its matching Blender base model");
                Require(character.PresentationAnimationNames.ToHashSet(StringComparer.Ordinal).IsSupersetOf(
                    ["human_idle", "human_walk", "human_run", "human_attack_l", "human_attack_r"]),
                    $"{sex} player model is missing required locomotion or attack animations");
                Require(character.CurrentPresentationAnimation == "human_idle",
                    $"{sex} player did not start in its idle animation");
                Require(character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                    && character.IsClothingVisibleForTesting(ItemIds.WorkTrousers),
                    $"{sex} starter outfit is not visible on the player model");

                var movementSpeed = (float)speciesCatalog.GetRequired(player.SpeciesId).MovementSpeed;
                character.SetPresentationMovementSpeedForTesting(movementSpeed * 0.5f);
                await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(character.CurrentPresentationAnimation == "human_walk"
                    && character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                    && character.IsClothingVisibleForTesting(ItemIds.WorkTrousers),
                    $"{sex} workwear did not remain visible during the walk animation");

                character.SetPresentationMovementSpeedForTesting(movementSpeed);
                await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(character.CurrentPresentationAnimation == "human_run"
                    && character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                    && character.IsClothingVisibleForTesting(ItemIds.WorkTrousers),
                    $"{sex} workwear did not remain visible during the run animation");

                character.SetPresentationMovementSpeedForTesting(0);
                await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);

                var neutralHeadPose = character.PresentationHeadPoseForTesting
                    ?? throw new InvalidOperationException($"{sex} player model is missing its canonical head bone");
                character._UnhandledInput(new InputEventMouseMotion { Relative = new Vector2(0, -40) });
                for (var frame = 0; frame < 12; frame++)
                    character.AdvancePresentationForTesting(0, 0);
                var lookedHeadPose = character.PresentationHeadPoseForTesting
                    ?? throw new InvalidOperationException($"{sex} player model lost its canonical head bone");
                var localPitchDelta = (neutralHeadPose.Inverse() * lookedHeadPose).GetEuler().X;
                Require(localPitchDelta < -0.1f,
                    $"{sex} upward mouse look did not pitch the imported head upward in player coordinates");

                await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
                var headPoseAfterAnimationProcess = character.PresentationHeadPoseForTesting
                    ?? throw new InvalidOperationException($"{sex} player model lost its canonical head bone");
                Require(lookedHeadPose.AngleTo(headPoseAfterAnimationProcess) < 0.02f,
                    $"{sex} imported animation overwrote the head-look pose after its physics update");

                character._UnhandledInput(new InputEventMouseMotion { Relative = new Vector2(0, 40) });
                for (var frame = 0; frame < 24; frame++)
                    character.AdvancePresentationForTesting(0, 0);

                var equipment = new PlayerEquipmentService(session.World);
                Require(equipment.Unequip(EquipmentSlotIds.Torso).Code == PlayerEquipmentResultCode.Unequipped,
                    $"{sex} work shirt could not be unequipped");
                Require(equipment.Unequip(EquipmentSlotIds.Legs).Code == PlayerEquipmentResultCode.Unequipped,
                    $"{sex} work trousers could not be unequipped");
                character.SetEquippedClothing(player.EquippedItems);
                Require(!character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                    && !character.IsClothingVisibleForTesting(ItemIds.WorkTrousers),
                    $"{sex} unequipped garments remained visible");

                var unequippedSave = WorldSnapshotSerializer.Serialize(session.World, 3.0);
                var loadedSession = NewGameSession.Create(speciesCatalog, profile);
                WorldSnapshotSerializer.Load(loadedSession.World, unequippedSave, speciesCatalog);
                var loadedPlayer = loadedSession.World.Entities.Single(entity => entity.Id == player.Id);
                character.BindPlayer(
                    loadedPlayer.Id,
                    loadedSession.World.PlayerProfile!,
                    (float)speciesCatalog.GetRequired(loadedPlayer.SpeciesId).MovementSpeed);
                character.SetEquippedClothing(loadedPlayer.EquippedItems);
                await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(!character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                    && !character.IsClothingVisibleForTesting(ItemIds.WorkTrousers),
                    $"{sex} saved unequipped state was not reflected after loading. "
                    + $"Snapshot equipment: {string.Join(", ", loadedPlayer.EquippedItems.Select(pair => $"{pair.Key}={pair.Value}"))}; "
                    + $"shirt visible: {character.IsClothingVisibleForTesting(ItemIds.WorkShirt)}; "
                    + $"trousers visible: {character.IsClothingVisibleForTesting(ItemIds.WorkTrousers)}");

                var loadedEquipment = new PlayerEquipmentService(loadedSession.World);
                Require(loadedEquipment.Equip(ItemIds.WorkShirt).Code == PlayerEquipmentResultCode.Equipped,
                    $"{sex} work shirt could not be equipped from inventory");
                Require(loadedEquipment.Equip(ItemIds.WorkTrousers).Code == PlayerEquipmentResultCode.Equipped,
                    $"{sex} work trousers could not be equipped from inventory");
                character.SetEquippedClothing(loadedPlayer.EquippedItems);
                Require(character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                    && character.IsClothingVisibleForTesting(ItemIds.WorkTrousers),
                    $"{sex} equipped garments were not shown");

                var equippedSave = WorldSnapshotSerializer.Serialize(loadedSession.World, 6.0);
                var reloadedSession = NewGameSession.Create(speciesCatalog, profile);
                WorldSnapshotSerializer.Load(reloadedSession.World, equippedSave, speciesCatalog);
                var reloadedPlayer = reloadedSession.World.Entities.Single(entity => entity.Id == player.Id);
                character.BindPlayer(
                    reloadedPlayer.Id,
                    reloadedSession.World.PlayerProfile!,
                    (float)speciesCatalog.GetRequired(reloadedPlayer.SpeciesId).MovementSpeed);
                character.SetEquippedClothing(reloadedPlayer.EquippedItems);
                await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                    && character.IsClothingVisibleForTesting(ItemIds.WorkTrousers),
                    $"{sex} saved equipped state was not reflected after loading");
            }

            GD.Print("PLAYER WORKWEAR VALIDATION PASSED: male/female Blender base selection, imported animations, outfit visibility, equipment swaps and save/load.");
            main.GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"PLAYER WORKWEAR VALIDATION FAILED: {exception}");
            main.GetTree().Quit(1);
        }
        finally
        {
            character.QueueFree();
        }
    }

    private static void Require(bool condition, string detail)
    {
        if (!condition)
            throw new InvalidOperationException($"Player workwear validation failed: {detail}.");
    }
}
