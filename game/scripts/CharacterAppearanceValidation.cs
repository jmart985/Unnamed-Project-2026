using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunCharacterAppearanceValidation()
    {
        try
        {
            _clock.SetPaused(true);
            using var conversionManifest = System.Text.Json.JsonDocument.Parse(
                Godot.FileAccess.GetFileAsString("res://data/player_appearance.json"));
            var convertedIds = conversionManifest.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
            Require(convertedIds.SetEquals(PlayerAppearanceCatalog.Default.Entries
                    .Where(item => !item.Id.StartsWith("custom_", StringComparison.Ordinal)).Select(item => item.Id)),
                "runtime options match the generated catalog without retaining retired source variants");
            _characterCreation.Visible = true;
            Require(!_genitaliaChoice.ButtonPressed, "anatomical visibility defaults off in creation");
            var viewport = GetNode<SubViewport>(CreationPreviewColumn + "/PreviewViewport/SubViewport");
            Require(viewport.OwnWorld3D && viewport.World3D != GetViewport().World3D,
                "creation preview is isolated from the playable world");
            var captureIndex = Array.IndexOf(OS.GetCmdlineUserArgs(), "--capture-appearance");
            var captureDirectory = captureIndex >= 0 ? OS.GetCmdlineUserArgs()[captureIndex + 1] : null;
            if (captureDirectory is not null) Directory.CreateDirectory(captureDirectory);
            foreach (var sex in new[] { PlayerSex.Male, PlayerSex.Female })
            {
                _sexChoice.Select((int)sex);
                RefreshAppearanceChoices();
                foreach (var choice in _appearanceChoices.Values) choice.Select(0);
                _genitaliaChoice.ButtonPressed = false;
                UpdateAppearancePreview();
                var skeleton = _appearancePreviewSkeleton!;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                foreach (var name in new[] { "hand_l", "hand_r", "eye_l", "eye_r" })
                {
                    var anchor = _appearancePreviewModel!.FindChild("anchor_" + name, true, false) as Node3D;
                    var bone = skeleton.FindBone(name);
                    Require(anchor is not null && bone >= 0
                        && anchor.GlobalPosition.DistanceTo((skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone)).Origin) < .02f,
                        $"{sex} {name} anchor follows its exported world-space bone");
                }
                Require(skeleton.GetBoneCount() >= 91, $"{sex} canonical, facial, anatomical and secondary cloth bones exist");
                foreach (var bone in new[] { "root", "pelvis", "spine_01", "spine_02", "spine_03",
                    "clavicle_l", "clavicle_r", "hand_l", "hand_r", "toe_l", "toe_r", "jaw",
                    "genital_root", "genital_01", "genital_02", "genital_l", "genital_r",
                    "cloth_skirt_front_01_l", "cloth_skirt_back_02_r", "cloth_cape_03" })
                    Require(skeleton.FindBone(bone) >= 0, $"{sex} required bone {bone} exists");
                var model = skeleton.GetParent<Node3D>();
                while (model != _appearancePreviewModel && model.GetParent() is Node3D parent)
                    model = parent;
                var profile = ReadCreationProfile("Appearance validation");
                var layers = new PlayerAppearanceLayers(model, skeleton, profile);
                layers.Apply(profile.Appearance!.Equipment, []);
                Require(!layers.GenitaliaVisible, $"{sex} default-off option hides every optional anatomy mesh");
                Require(layers.CoveredBodyPixels == 0, $"{sex} bare body is not permanently masked");
                _genitaliaChoice.ButtonPressed = true;
                UpdateAppearancePreview();
                skeleton = _appearancePreviewSkeleton!;
                var anatomy = _appearancePreviewModel!.FindChildren("Anatomy_*", "MeshInstance3D", true, false)
                    .OfType<MeshInstance3D>().ToArray();
                Require(anatomy.Length >= 3 && anatomy.All(mesh => mesh.Visible && mesh.Skin is not null),
                    $"{sex} enabled anatomical attachments are separate skinned meshes");
                Require(_appearancePreviewAnimation!.GetAnimationList().Any(name => name.ToString() == "source_dwight_happy")
                    && _appearancePreviewAnimation.GetAnimationList().Any(name => name.ToString() == "source_yuffie_happy"),
                    $"{sex} both acquired expression sets are available");
                var animation = _appearancePreviewAnimation;
                animation.Play("source_dwight_happy");
                animation.Advance(.45);
                Require(new[] { "jaw", "brow_l", "brow_r", "eyelid_l", "eyelid_r", "mouth_corner_l", "mouth_corner_r" }
                    .Any(name => skeleton.GetBonePosePosition(skeleton.FindBone(name)).Length() > .00001f),
                    $"{sex} source expression deforms facial controls");
                _genitaliaChoice.ButtonPressed = false;
                foreach (var (slot, choice) in _appearanceChoices)
                {
                    for (var i = 1; i < choice.ItemCount; i++)
                        Require(PlayerAppearanceCatalog.Default.Supports(choice.GetItemMetadata(i).AsString(), slot, sex),
                            $"{sex} options exclude incompatible {slot} assets");
                }
                UpdateAppearancePreview();
                skeleton = _appearancePreviewSkeleton!;
                foreach (var item in PlayerAppearanceCatalog.Default.Entries.Where(item => item.Sexes.Contains(sex)))
                {
                    foreach (var choice in _appearanceChoices.Values) choice.Select(0);
                    var selected = _appearanceChoices[item.Slot];
                    selected.Select(Enumerable.Range(1, selected.ItemCount - 1)
                        .Single(i => selected.GetItemMetadata(i).AsString() == item.Id));
                    UpdateAppearancePreview();
                    Require(_appearancePreviewModel!.FindChildren("Appearance_" + item.Id, "", true, false)
                        .OfType<Node3D>().Any(root => root.Visible), $"{sex} asset {item.Id} attaches visibly");
                    Require(_appearancePreviewModel.FindChildren("*", "Skeleton3D", true, false).Count == 1,
                        $"{sex} asset {item.Id} reuses the body's skeleton");
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (captureDirectory is not null)
                    {
                        var wardrobeDirectory = Path.Combine(captureDirectory, "wardrobe");
                        Directory.CreateDirectory(wardrobeDirectory);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        using var image = viewport.GetTexture().GetImage();
                        image.Resize(360, 252, Image.Interpolation.Lanczos);
                        image.SavePng(Path.Combine(wardrobeDirectory, $"{sex.ToString().ToLowerInvariant()}_{item.Id}.png"));
                    }
                }
                foreach (var choice in _appearanceChoices.Values) choice.Select(0);
                foreach (var (slot, id) in new[] { (EquipmentSlotIds.Torso, ItemIds.WorkShirt),
                                                  (EquipmentSlotIds.Legs, ItemIds.WorkTrousers) })
                {
                    var choice = _appearanceChoices[slot];
                    choice.Select(Enumerable.Range(1, choice.ItemCount - 1)
                        .Single(i => choice.GetItemMetadata(i).AsString() == id));
                }
                _genitaliaChoice.ButtonPressed = true;
                UpdateAppearancePreview();
                Require(!_appearancePreviewModel!.FindChildren("Anatomy_*", "MeshInstance3D", true, false)
                    .OfType<MeshInstance3D>().Any(mesh => mesh.Visible),
                    $"{sex} covering clothing suppresses optional anatomy even when enabled");
                Require(_appearancePreviewModel.FindChildren("*", "Skeleton3D", true, false).Count == 1,
                    $"{sex} clothing and body share one skeleton");
                Require(_appearancePreviewLayers!.CoveredBodyPixels > 1000, $"{sex} tight clothing masks its covered skin");
                var equipped = ReadCreationProfile("Mask visibility").Appearance!.Equipment;
                _appearancePreviewLayers.Apply(equipped, [EquipmentSlotIds.Torso, EquipmentSlotIds.Legs]);
                Require(_appearancePreviewLayers.CoveredBodyPixels == 0,
                    $"{sex} hiding both garments restores the complete body");
                _appearancePreviewLayers.Apply(equipped, []);
                var extraEquipment = equipped.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                extraEquipment[EquipmentSlotIds.Hands] = PlayerAppearanceCatalog.Default.ForSlot(EquipmentSlotIds.Hands, sex).First().Id;
                extraEquipment[EquipmentSlotIds.Feet] = PlayerAppearanceCatalog.Default.ForSlot(EquipmentSlotIds.Feet, sex).First().Id;
                var baseCoverage = _appearancePreviewLayers.CoveredBodyPixels;
                _appearancePreviewLayers.Apply(extraEquipment, []);
                Require(extraEquipment.Values.All(_appearancePreviewLayers.IsVisible)
                    && _appearancePreviewLayers.CoveredBodyPixels >= baseCoverage,
                    $"{sex} boots, gloves, shirt and trousers remain independent and combine masks");
                _appearancePreviewLayers.Apply(extraEquipment, [EquipmentSlotIds.Hands, EquipmentSlotIds.Feet]);
                Require(_appearancePreviewLayers.CoveredBodyPixels == baseCoverage,
                    $"{sex} hiding boots/gloves preserves the torso and leg mask");
                _appearancePreviewLayers.Apply(equipped, []);
                var jackets = PlayerAppearanceCatalog.Default.ForSlot(EquipmentSlotIds.Outerwear, sex);
                if (jackets.Count > 0)
                {
                    var layered = equipped.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                    layered[EquipmentSlotIds.Outerwear] = jackets[0].Id;
                    _appearancePreviewLayers.Apply(layered, []);
                    Require(_appearancePreviewLayers.IsVisible(ItemIds.WorkShirt)
                        && _appearancePreviewLayers.IsVisible(jackets[0].Id),
                        $"{sex} jacket and shirt attach independently");
                    _appearancePreviewLayers.Apply(layered, [EquipmentSlotIds.Outerwear]);
                    Require(_appearancePreviewLayers.IsVisible(ItemIds.WorkShirt)
                        && !_appearancePreviewLayers.IsVisible(jackets[0].Id)
                        && _appearancePreviewLayers.CoveredBodyPixels == baseCoverage,
                        $"{sex} hiding a jacket retains its shirt and underlying coverage");
                    _appearancePreviewLayers.Apply(equipped, []);
                }
                var other = GD.Load<PackedScene>(PlayerAppearanceAssets.BodyPath(sex)).Instantiate<Node3D>();
                other.Visible = false;
                _previewRoot.AddChild(other);
                var otherSkeleton = other.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().Single();
                var otherLayers = new PlayerAppearanceLayers(other, otherSkeleton,
                    PlayerCharacterProfile.Create("Independent appearance", sex: sex, appearance: PlayerAppearance.Bare));
                otherLayers.Apply(new Dictionary<string, string>(), []);
                var firstMaterial = ((MeshInstance3D)_appearancePreviewModel.FindChild("HumanBody", true, false)).GetActiveMaterial(0);
                var otherMaterial = ((MeshInstance3D)other.FindChild("HumanBody", true, false)).GetActiveMaterial(0);
                Require(firstMaterial.GetInstanceId() != otherMaterial.GetInstanceId()
                    && otherLayers.CoveredBodyPixels == 0 && _appearancePreviewLayers.CoveredBodyPixels > 1000,
                    $"{sex} outfit masks are isolated between character instances");
                _previewRoot.RemoveChild(other);
                other.QueueFree();
                await ValidateClothingSpring(sex);
                // Restore the starter outfit for the inspection views.
                _genitaliaChoice.ButtonPressed = false;
                foreach (var choice in _appearanceChoices.Values) choice.Select(0);
                foreach (var (slot, id) in equipped)
                {
                    var choice = _appearanceChoices[slot];
                    choice.Select(Enumerable.Range(1, choice.ItemCount - 1)
                        .Single(i => choice.GetItemMetadata(i).AsString() == id));
                }
                UpdateAppearancePreview();
                if (captureDirectory is not null)
                {
                    var hair = _appearanceChoices["hair"];
                    var prefix = sex == PlayerSex.Male ? "dwight_" : "aerith_";
                    hair.Select(Enumerable.Range(1, hair.ItemCount - 1)
                        .First(i => hair.GetItemMetadata(i).AsString().StartsWith(prefix, StringComparison.Ordinal)));
                    UpdateAppearancePreview();
                    foreach (var (view, yaw) in new[] { ("front", 0f), ("side", Mathf.Pi / 2), ("back", Mathf.Pi) })
                    {
                        _appearancePreviewYaw = yaw;
                        RotateAppearancePreview(0);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        viewport.GetTexture().GetImage().SavePng(Path.Combine(captureDirectory,
                            $"{sex.ToString().ToLowerInvariant()}_{view}.png"));
                        if (view == "front")
                            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(captureDirectory,
                                $"{sex.ToString().ToLowerInvariant()}_menu.png"));
                    }
                    foreach (var choice in _appearanceChoices.Values) choice.Select(0);
                    hair.Select(Enumerable.Range(1, hair.ItemCount - 1)
                        .First(i => hair.GetItemMetadata(i).AsString().StartsWith(prefix, StringComparison.Ordinal)));
                    var looseId = sex == PlayerSex.Female ? "aerith_aerith_pink_dress_dress"
                        : "radagon_male_radagon_default_dress";
                    var torso = _appearanceChoices[EquipmentSlotIds.Torso];
                    torso.Select(Enumerable.Range(1, torso.ItemCount - 1)
                        .Single(i => torso.GetItemMetadata(i).AsString() == looseId));
                    _appearancePreviewYaw = 0;
                    UpdateAppearancePreview();
                    _appearancePreviewAnimation!.Play("human_walk");
                    _appearancePreviewAnimation.Advance(.4);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    viewport.GetTexture().GetImage().SavePng(Path.Combine(captureDirectory,
                        $"{sex.ToString().ToLowerInvariant()}_loose.png"));
                    if (sex == PlayerSex.Female)
                    {
                        foreach (var choice in _appearanceChoices.Values) choice.Select(0);
                        foreach (var (slot, id) in equipped.Append(new KeyValuePair<string, string>(
                                     EquipmentSlotIds.Outerwear, "aerith_aerith_pink_dress_jacket")))
                        {
                            var choice = _appearanceChoices[slot];
                            choice.Select(Enumerable.Range(1, choice.ItemCount - 1)
                                .Single(i => choice.GetItemMetadata(i).AsString() == id));
                        }
                        hair.Select(Enumerable.Range(1, hair.ItemCount - 1)
                            .First(i => hair.GetItemMetadata(i).AsString().StartsWith(prefix, StringComparison.Ordinal)));
                        UpdateAppearancePreview();
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        viewport.GetTexture().GetImage().SavePng(Path.Combine(captureDirectory, "female_layered.png"));
                    }
                }
            }
            GD.Print("CHARACTER APPEARANCE VALIDATION PASSED: two clean rigged bases, default-off anatomy, covering garments, source expressions, sex-filtered wardrobe and shared-skeleton attachments.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"CHARACTER APPEARANCE VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task ValidateClothingSpring(PlayerSex sex)
    {
        foreach (var choice in _appearanceChoices.Values) choice.Select(0);
        var id = sex == PlayerSex.Female ? "aerith_aerith_pink_dress_dress"
            : "radagon_male_radagon_default_dress";
        var torso = _appearanceChoices[EquipmentSlotIds.Torso];
        torso.Select(Enumerable.Range(1, torso.ItemCount - 1)
            .Single(i => torso.GetItemMetadata(i).AsString() == id));
        UpdateAppearancePreview();
        var skeleton = _appearancePreviewSkeleton!;
        var spring = _appearancePreviewLayers!.Spring;
        Require(spring is { Active: true }, $"{sex} loose outfit activates cloth springs");
        var animation = _appearancePreviewAnimation!;
        animation.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        animation.Play("human_idle");
        animation.Advance(0);
        skeleton.ModifierCallbackModeProcess = Skeleton3D.ModifierCallbackModeProcessEnum.Manual;
        var model = _appearancePreviewModel!;
        model.Scale = new Vector3(1.45f, 1.18f, .75f);
        var pelvis = skeleton.GetBoneGlobalPose(skeleton.FindBone("pelvis"));
        var peak = 0f;
        for (var frame = 0; frame < 30; frame++)
        {
            model.Position += new Vector3(.005f, 0, .02f);
            skeleton.Advance(1.0 / 60);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            peak = Math.Max(peak, spring!.MaximumOffsetRadians);
        }
        Require(peak > .0001f && peak <= ClothingSpringModifier.AngleLimit + .0001f
            && spring!.ModifiedBoneCount == 8,
            $"{sex} scaled presentation receives bounded, live cloth spring movement");
        Require(skeleton.GetBoneGlobalPose(skeleton.FindBone("pelvis")).Origin.DistanceTo(pelvis.Origin) < .0001f,
            $"{sex} springs do not alter locomotion bones");
        for (var frame = 0; frame < 180; frame++)
        {
            skeleton.Advance(1.0 / 60);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Require(spring!.MaximumOffsetRadians < .001f, $"{sex} cloth settles after motion stops");
        model.Position += Vector3.Right * 30;
        skeleton.Advance(1.0 / 60);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(spring.MaximumOffsetRadians == 0, $"{sex} teleport resets cloth inertia");
        _appearancePreviewLayers.Apply(new Dictionary<string, string> { [EquipmentSlotIds.Torso] = id },
            [EquipmentSlotIds.Torso]);
        Require(!spring.Active && _appearancePreviewLayers.CoveredBodyPixels == 0,
            $"{sex} hiding loose clothing disables its springs and restores skin");
    }
}
