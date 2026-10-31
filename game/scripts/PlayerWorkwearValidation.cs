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
            main.ValidateEquipmentVisibilityMenuForTesting();

            foreach (var (sex, expectedPath) in new[]
            {
                (PlayerSex.Male, PlayerAppearanceAssets.BodyPath(PlayerSex.Male)),
                (PlayerSex.Female, PlayerAppearanceAssets.BodyPath(PlayerSex.Female))
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
                Require(player.Inventory.GetValueOrDefault(ItemIds.DeluxeBunnySuitTop) == 1
                    && player.Inventory.GetValueOrDefault(ItemIds.DeluxeBunnySuitLegs) == 1
                    && !character.IsClothingVisibleForTesting(ItemIds.DeluxeBunnySuitTop)
                    && !character.IsClothingVisibleForTesting(ItemIds.DeluxeBunnySuitLegs),
                    $"{sex} player does not start with the unequipped Deluxe Bunny outfit in inventory");

                var equipment = new PlayerEquipmentService(session.World);
                Require(equipment.SetVisibility(EquipmentSlotIds.Torso, visible: false).Code
                    == PlayerEquipmentResultCode.VisibilityChanged,
                    $"{sex} equipped torso could not be hidden");
                character.SetHiddenEquipmentSlots(player.HiddenEquipmentSlots);
                Require(!character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                    && character.IsClothingVisibleForTesting(ItemIds.WorkTrousers)
                    && player.EquippedItems[EquipmentSlotIds.Torso] == ItemIds.WorkShirt,
                    $"{sex} hiding torso changed the wrong presentation or unequipped the work shirt");
                Require(equipment.SetVisibility(EquipmentSlotIds.Torso, visible: true).Code
                    == PlayerEquipmentResultCode.VisibilityChanged,
                    $"{sex} equipped torso could not be shown again");
                character.SetHiddenEquipmentSlots(player.HiddenEquipmentSlots);
                Require(character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                    && character.IsClothingVisibleForTesting(ItemIds.WorkTrousers),
                    $"{sex} showing torso did not restore only the equipped clothing visuals");

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

                var walkingSpeed = movementSpeed * 0.5f;
                character.AdvancePresentationForTesting(walkingSpeed, 0.08f);
                Require(character.CurrentPresentationAnimation == "human_walk",
                    $"{sex} player did not resume locomotion before the moving-attack check");
                var leftThighBeforeAttack = character.PresentationBonePoseForTesting("thigh_l")
                    ?? throw new InvalidOperationException($"{sex} player model is missing its left thigh bone");
                Require(character.StartAttackForTesting(left: true),
                    $"{sex} left attack did not start for the locomotion-layer check");
                Require(character.CurrentPresentationAnimation == "human_walk"
                    && character.CurrentAttackPresentationAnimation == "human_attack_l",
                    $"{sex} attack replaced the active locomotion animation instead of layering over it");
                Require(character.HasAttackBoneTrackForTesting("human_attack_l", "upperarm_l")
                    && !character.HasAttackBoneTrackForTesting("human_attack_l", "thigh_l")
                    && !character.HasAttackBoneTrackForTesting("human_attack_l", "calf_l")
                    && character.HasAttackBoneTrackForTesting("human_attack_r", "upperarm_r")
                    && !character.HasAttackBoneTrackForTesting("human_attack_r", "thigh_r")
                    && !character.HasAttackBoneTrackForTesting("human_attack_r", "calf_r"),
                    $"{sex} attack overlay did not isolate upper-body tracks from the leg tracks");
                character.AdvancePresentationForTesting(walkingSpeed, 0.08f);
                var leftThighDuringAttack = character.PresentationBonePoseForTesting("thigh_l")
                    ?? throw new InvalidOperationException($"{sex} player model lost its left thigh bone");
                Require(character.CurrentAttackPresentationAnimation == "human_attack_l"
                    && character.CurrentPresentationAnimation == "human_walk"
                    && leftThighBeforeAttack.AngleTo(leftThighDuringAttack) > 0.02f,
                    $"{sex} lower-body walk animation did not continue under the imported attack overlay");
                character.AdvancePresentationForTesting(0, 0.4f);

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
                Require(loadedPlayer.IsEquipmentVisible(EquipmentSlotIds.Torso)
                    && loadedPlayer.IsEquipmentVisible(EquipmentSlotIds.Legs),
                    $"{sex} unequipping clothing left a hidden-slot override behind");
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

                var bunnyEquipment = new PlayerEquipmentService(reloadedSession.World);
                Require(bunnyEquipment.Equip(ItemIds.DeluxeBunnySuitTop).Code == PlayerEquipmentResultCode.Equipped
                    && bunnyEquipment.Equip(ItemIds.DeluxeBunnySuitLegs).Code == PlayerEquipmentResultCode.Equipped,
                    $"{sex} Deluxe Bunny top and legs could not be equipped");
                character.SetEquippedClothing(reloadedPlayer.EquippedItems);
                Require(character.IsClothingVisibleForTesting(ItemIds.DeluxeBunnySuitTop)
                    && character.IsClothingVisibleForTesting(ItemIds.DeluxeBunnySuitLegs)
                    && !character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                    && !character.IsClothingVisibleForTesting(ItemIds.WorkTrousers),
                    $"{sex} equipped Deluxe Bunny outfit did not replace the workwear visuals");

                Require(bunnyEquipment.SetVisibility(EquipmentSlotIds.Torso, visible: false).Code
                    == PlayerEquipmentResultCode.VisibilityChanged,
                    $"{sex} Deluxe Bunny top could not be hidden before saving");
                character.SetHiddenEquipmentSlots(reloadedPlayer.HiddenEquipmentSlots);
                Require(!character.IsClothingVisibleForTesting(ItemIds.DeluxeBunnySuitTop)
                    && character.IsClothingVisibleForTesting(ItemIds.DeluxeBunnySuitLegs),
                    $"{sex} hiding the Deluxe Bunny top also hid the leg item");
                var bunnySave = WorldSnapshotSerializer.Serialize(reloadedSession.World, 9.0);
                var bunnySession = NewGameSession.Create(speciesCatalog, profile);
                WorldSnapshotSerializer.Load(bunnySession.World, bunnySave, speciesCatalog);
                var bunnyPlayer = bunnySession.World.Entities.Single(entity => entity.Id == player.Id);
                character.BindPlayer(
                    bunnyPlayer.Id,
                    bunnySession.World.PlayerProfile!,
                    (float)speciesCatalog.GetRequired(bunnyPlayer.SpeciesId).MovementSpeed);
                character.SetEquippedClothing(bunnyPlayer.EquippedItems);
                character.SetHiddenEquipmentSlots(bunnyPlayer.HiddenEquipmentSlots);
                await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(!character.IsClothingVisibleForTesting(ItemIds.DeluxeBunnySuitTop)
                    && character.IsClothingVisibleForTesting(ItemIds.DeluxeBunnySuitLegs),
                    $"{sex} saved equipment visibility was not restored independently on its matching player model");
            }

            foreach (var sex in new[] { PlayerSex.Male, PlayerSex.Female })
                await ValidateIndependentEquipment(main, character, speciesCatalog, sex);
            GD.Print("PLAYER WORKWEAR VALIDATION PASSED: male/female Blender bases, imported animations, independent boots/gloves/jackets, workwear and Deluxe Bunny outfit visibility, equipment swaps and save/load.");
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

    private static async Task ValidateIndependentEquipment(Main main, PlayerCharacter character,
        SpeciesCatalog speciesCatalog, PlayerSex sex)
    {
        var boots = PlayerAppearanceCatalog.Default.ForSlot(EquipmentSlotIds.Feet, sex).First().Id;
        var gloves = PlayerAppearanceCatalog.Default.ForSlot(EquipmentSlotIds.Hands, sex).First().Id;
        var jacket = PlayerAppearanceCatalog.Default.ForSlot(EquipmentSlotIds.Outerwear, sex).FirstOrDefault()?.Id ?? "none";
        var appearance = PlayerAppearance.Bare with
        {
            Torso = ItemIds.WorkShirt, Legs = ItemIds.WorkTrousers,
            Feet = boots, Hands = gloves, Outerwear = jacket
        };
        var profile = PlayerCharacterProfile.Create($"Modular {sex}", sex: sex, appearance: appearance);
        var session = NewGameSession.Create(speciesCatalog, profile);
        var player = session.Player;
        var speed = (float)speciesCatalog.GetRequired(player.SpeciesId).MovementSpeed;
        character.BindPlayer(player.Id, profile, speed);
        character.SetEquippedClothing(player.EquippedItems);
        character.SetHiddenEquipmentSlots(player.HiddenEquipmentSlots);
        await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
        character.AdvancePresentationForTesting(speed * .5f, .2f);
        Require(appearance.Equipment.Values.All(character.IsClothingVisibleForTesting),
            $"{sex} independent pieces were not visible on the moving production player");
        Require(new PlayerEquipmentService(session.World).SetVisibility(EquipmentSlotIds.Feet, false).Code
            == PlayerEquipmentResultCode.VisibilityChanged, $"{sex} boots could not be hidden");
        character.SetHiddenEquipmentSlots(player.HiddenEquipmentSlots);
        character.AdvancePresentationForTesting(speed, .1f);
        Require(!character.IsClothingVisibleForTesting(boots) && character.IsClothingVisibleForTesting(gloves)
            && character.IsClothingVisibleForTesting(ItemIds.WorkShirt),
            $"{sex} hiding boots affected other items during locomotion");
        var restored = NewGameSession.Create(speciesCatalog, profile);
        WorldSnapshotSerializer.Load(restored.World, WorldSnapshotSerializer.Serialize(session.World, 12), speciesCatalog);
        var loaded = restored.World.Entities.Single(entity => entity.Id == restored.World.PlayerEntityId);
        character.BindPlayer(loaded.Id, restored.World.PlayerProfile!, speed);
        character.SetEquippedClothing(loaded.EquippedItems);
        character.SetHiddenEquipmentSlots(loaded.HiddenEquipmentSlots);
        await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(!character.IsClothingVisibleForTesting(boots) && character.IsClothingVisibleForTesting(gloves)
            && character.IsClothingVisibleForTesting(ItemIds.WorkShirt),
            $"{sex} independent presentation was not restored from the save");
        if (jacket != "none")
        {
            Require(character.IsClothingVisibleForTesting(jacket), "restored jacket is missing");
            Require(new PlayerEquipmentService(restored.World).Unequip(EquipmentSlotIds.Outerwear).Code
                == PlayerEquipmentResultCode.Unequipped, "jacket could not be unequipped");
            character.SetEquippedClothing(loaded.EquippedItems);
            Require(!character.IsClothingVisibleForTesting(jacket) && character.IsClothingVisibleForTesting(ItemIds.WorkShirt)
                && loaded.Inventory.GetValueOrDefault(jacket) == 1,
                "unequipping a jacket replaced the shirt or lost the carried item");
        }
    }

    private static void Require(bool condition, string detail)
    {
        if (!condition)
            throw new InvalidOperationException($"Player workwear validation failed: {detail}.");
    }
}
