using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private const string CreationFields = "CharacterCreation/Panel/Layout/Form/Fields";
    private const string CreationPreviewColumn = "CharacterCreation/Panel/Layout/PreviewColumn";
    private readonly Dictionary<string, OptionButton> _appearanceChoices = new(StringComparer.Ordinal);
    private CheckButton _genitaliaChoice = null!;
    private OptionButton _appearanceActionChoice = null!;
    private Node3D? _appearancePreviewModel;
    private AnimationPlayer? _appearancePreviewAnimation;
    private Skeleton3D? _appearancePreviewSkeleton;
    private PlayerAppearanceLayers? _appearancePreviewLayers;
    private float _appearancePreviewYaw;
    private bool _appearanceEditorReady;

    private void ConfigureAppearanceEditor()
    {
        if (_appearanceEditorReady) return;
        _characterCreation.Layer = 20;
        GetNode<PanelContainer>("CharacterCreation/Panel").AddThemeStyleboxOverride("panel",
            new StyleBoxFlat { BgColor = new Color(.075f, .085f, .10f) });
        var previewViewport = GetNode<SubViewport>(CreationPreviewColumn + "/PreviewViewport/SubViewport");
        previewViewport.TransparentBg = false;
        GetNode<WorldEnvironment>(CreationPreviewColumn + "/PreviewViewport/SubViewport/PreviewEnvironment").Environment =
            new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(.075f, .085f, .10f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White, AmbientLightEnergy = .25f };
        GetNode<DirectionalLight3D>(CreationPreviewColumn + "/PreviewViewport/SubViewport/PreviewLight").LightEnergy = .6f;
        GetNode<DirectionalLight3D>(CreationPreviewColumn + "/PreviewViewport/SubViewport/PreviewFill").LightEnergy = .2f;
        var fields = GetNode<VBoxContainer>(CreationFields);
        var group = new VBoxContainer { Name = "AppearanceChoices", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        fields.AddChild(group);
        fields.MoveChild(group, _muscleChoice.GetIndex() + 1);
        _genitaliaChoice = new CheckButton
        {
            Name = "ShowGenitalia", Text = "Show genitalia", ButtonPressed = false,
            TooltipText = "Off by default. Visible covering garments also hide these anatomical attachments."
        };
        group.AddChild(_genitaliaChoice);
        _genitaliaChoice.Toggled += _ => UpdateCreationPreview();
        foreach (var (slot, label) in new[]
        {
            ("hair", "Hair"), (EquipmentSlotIds.Head, "Head / glasses"),
            (EquipmentSlotIds.Torso, "Torso / outfit"), (EquipmentSlotIds.Outerwear, "Jacket / coat"), (EquipmentSlotIds.Legs, "Legs"),
            (EquipmentSlotIds.Hands, "Hands / sleeves"), (EquipmentSlotIds.Feet, "Footwear"),
            (EquipmentSlotIds.Accessory, "Accessory")
        })
        {
            group.AddChild(new Label { Text = label });
            var choice = new OptionButton { Name = slot, FitToLongestItem = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            group.AddChild(choice);
            _appearanceChoices.Add(slot, choice);
            choice.ItemSelected += _ => UpdateCreationPreview();
        }
        var preview = GetNode<VBoxContainer>(CreationPreviewColumn);
        var rotation = new HBoxContainer();
        preview.AddChild(rotation);
        var left = new Button { Text = "Turn left" };
        var right = new Button { Text = "Turn right" };
        rotation.AddChild(left);
        rotation.AddChild(right);
        left.Pressed += () => RotateAppearancePreview(-0.35f);
        right.Pressed += () => RotateAppearancePreview(0.35f);
        preview.AddChild(new Label { Text = "Preview movement / expression" });
        _appearanceActionChoice = new OptionButton { FitToLongestItem = false };
        preview.AddChild(_appearanceActionChoice);
        _appearanceActionChoice.ItemSelected += _ => PlayAppearancePreview();
        var play = new Button { Text = "Play / replay" };
        preview.AddChild(play);
        play.Pressed += PlayAppearancePreview;
        _appearanceEditorReady = true;
        RefreshAppearanceChoices();
    }

    private void RefreshAppearanceChoices()
    {
        if (!_appearanceEditorReady) return;
        var sex = (PlayerSex)_sexChoice.Selected;
        foreach (var (slot, choice) in _appearanceChoices)
        {
            var previous = SelectedAppearanceId(choice);
            choice.Clear();
            choice.AddItem("None");
            choice.SetItemMetadata(0, "none");
            foreach (var definition in PlayerAppearanceCatalog.Default.ForSlot(slot, sex))
            {
                choice.AddItem(PlayerAppearanceAssets.Default.GetItem(definition.Id).ItemName);
                choice.SetItemMetadata(choice.ItemCount - 1, definition.Id);
                choice.SetItemTooltip(choice.ItemCount - 1, definition.Name);
            }
            var selected = Enumerable.Range(0, choice.ItemCount)
                .FirstOrDefault(index => choice.GetItemMetadata(index).AsString() == previous);
            choice.Select(selected);
        }
    }

    private static string SelectedAppearanceId(OptionButton choice) =>
        choice.Selected >= 0 ? choice.GetItemMetadata(choice.Selected).AsString() : "none";

    private PlayerCharacterProfile ReadCreationProfile(string name)
    {
        string Selected(string slot) => _appearanceChoices.TryGetValue(slot, out var choice)
            ? SelectedAppearanceId(choice) : "none";
        var appearance = new PlayerAppearance(_genitaliaChoice?.ButtonPressed == true,
            Selected("hair"), Selected(EquipmentSlotIds.Head), Selected(EquipmentSlotIds.Torso),
            Selected(EquipmentSlotIds.Legs), Selected(EquipmentSlotIds.Hands),
            Selected(EquipmentSlotIds.Feet), Selected(EquipmentSlotIds.Accessory), Selected(EquipmentSlotIds.Outerwear));
        return PlayerCharacterProfile.Create(name, (PlayerHeight)_heightChoice.Selected,
            (PlayerBodyType)_bodyTypeChoice.Selected, (PlayerSex)_sexChoice.Selected,
            _weightChoice.Value, _muscleChoice.Value, appearance);
    }

    private void UpdateAppearancePreview()
    {
        if (!_appearanceEditorReady || _sexChoice.Selected < 0) return;
        try
        {
            var profile = ReadCreationProfile("Preview");
            var proportions = PlayerBodyProportions.From(profile);
            foreach (var child in _previewRoot.GetChildren())
            {
                _previewRoot.RemoveChild(child);
                child.QueueFree();
            }
            var adapter = new Node3D { Rotation = Vector3.Up * (Mathf.Pi + _appearancePreviewYaw),
                Scale = PlayerCharacter.PlayerVisualScale(proportions) };
            _previewRoot.AddChild(adapter);
            var model = GD.Load<PackedScene>(PlayerAppearanceAssets.BodyPath(profile.Sex)).Instantiate<Node3D>();
            adapter.AddChild(model);
            var skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().Single();
            _appearancePreviewLayers = new PlayerAppearanceLayers(model, skeleton, profile);
            _appearancePreviewLayers.Apply(profile.Appearance!.Equipment, []);
            _appearancePreviewModel = adapter;
            _appearancePreviewSkeleton = skeleton;
            _appearancePreviewAnimation = model.FindChildren("*", "AnimationPlayer", true, false)
                .OfType<AnimationPlayer>().Single();
            _appearancePreviewAnimation.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Idle;
            var previous = _appearanceActionChoice.Selected >= 0
                ? _appearanceActionChoice.GetItemMetadata(_appearanceActionChoice.Selected).AsString() : "human_idle";
            _appearanceActionChoice.Clear();
            foreach (var action in _appearancePreviewAnimation.GetAnimationList().Select(name => name.ToString())
                         .Where(name => name != "RESET").OrderBy(name => name, StringComparer.Ordinal))
            {
                _appearanceActionChoice.AddItem(action.Replace("human_", "").Replace("expression_", "").Replace('_', ' '));
                _appearanceActionChoice.SetItemMetadata(_appearanceActionChoice.ItemCount - 1, action);
            }
            var selected = Enumerable.Range(0, _appearanceActionChoice.ItemCount)
                .FirstOrDefault(index => _appearanceActionChoice.GetItemMetadata(index).AsString() == previous);
            _appearanceActionChoice.Select(selected);
            PlayAppearancePreview();
            var camera = GetNode<Camera3D>(CreationPreviewColumn + "/PreviewViewport/SubViewport/PreviewCamera");
            camera.Position = new Vector3(0, proportions.Height * 0.53f, -3.0f);
            camera.Fov = 40;
            camera.LookAt(new Vector3(0, proportions.Height * 0.53f, 0));
            _creationPreview.Text = $"{profile.Sex} · {proportions.Height:0.00} m";
        }
        catch (ArgumentException exception)
        {
            _creationPreview.Text = exception.Message;
        }
    }

    private void RotateAppearancePreview(float amount)
    {
        _appearancePreviewYaw += amount;
        if (_appearancePreviewModel is { } model) model.Rotation = Vector3.Up * (Mathf.Pi + _appearancePreviewYaw);
    }

    private void PlayAppearancePreview()
    {
        if (_appearancePreviewAnimation is null || _appearancePreviewSkeleton is null || _appearanceActionChoice.Selected < 0) return;
        _appearancePreviewAnimation.Stop();
        for (var index = 0; index < _appearancePreviewSkeleton.GetBoneCount(); index++)
            _appearancePreviewSkeleton.ResetBonePose(index);
        _appearancePreviewAnimation.Play(_appearanceActionChoice.GetItemMetadata(_appearanceActionChoice.Selected).AsString());
    }
}
