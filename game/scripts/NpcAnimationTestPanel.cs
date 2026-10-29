using Godot;

namespace UnnamedProject2026;

internal sealed record NpcAnimationTestActor(string Id, string Label);

internal enum NpcAnimationTestAction
{
    SelectTarget,
    Greet,
    Interact,
    AttackLeft,
    AttackRight,
    Follow,
    Stay
}

internal enum NpcAnimationTestMovement
{
    Walk,
    Run,
    Stop
}

/// <summary>In-game harness for inspecting and exercising each loaded actor's clips and interactions.</summary>
internal partial class NpcAnimationTestPanel : CanvasLayer
{
    private Control _root = null!;
    private OptionButton _actorChoice = null!;
    private OptionButton _animationChoice = null!;
    private CheckButton _loopChoice = null!;
    private Label _status = null!;
    private IReadOnlyList<NpcAnimationTestActor> _actors = [];
    private Func<string, EntityView?>? _resolveView;

    public event Action<string, NpcAnimationTestAction>? ActionRequested;
    public event Action<string, NpcAnimationTestMovement>? MovementRequested;
    public event Action? RefreshRequested;
    public event Action? Closed;

    public bool IsOpen => _root is { Visible: true };
    public int ActorCount => _actorChoice?.ItemCount ?? 0;
    public int AnimationCount => _animationChoice?.ItemCount ?? 0;

    public override void _Ready()
    {
        Layer = 5;
        _root = new Control { Name = "NpcAnimationTestOverlay", Visible = false };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var panel = new PanelContainer
        {
            Name = "NpcAnimationTestPanel",
            AnchorLeft = 0.58f,
            AnchorTop = 0.08f,
            AnchorRight = 0.98f,
            AnchorBottom = 0.92f,
            CustomMinimumSize = new Vector2(400, 0)
        };
        _root.AddChild(panel);

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 12);
        panel.AddChild(margin);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        margin.AddChild(scroll);

        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);

        var header = new HBoxContainer();
        content.AddChild(header);
        var title = new Label
        {
            Text = "NPC ANIMATION & INTERACTION TEST",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 19);
        header.AddChild(title);
        AddButton(header, "Close", Close);

        content.AddChild(new Label
        {
            Text = "Choose any loaded NPC or creature. Playback and orders use the selected entity's imported model and normal simulation navigation.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        _actorChoice = new OptionButton { Name = "NpcAnimationTestActor", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _actorChoice.ItemSelected += _ => RefreshAnimations();
        content.AddChild(_actorChoice);
        AddButton(content, "Refresh actors", () => RefreshRequested?.Invoke());

        content.AddChild(new Label { Text = "Animation clip" });
        _animationChoice = new OptionButton { Name = "NpcAnimationTestClip", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddChild(_animationChoice);
        var playback = new HBoxContainer();
        _loopChoice = new CheckButton { Text = "Loop", ButtonPressed = true };
        playback.AddChild(_loopChoice);
        AddButton(playback, "Play clip", PlayClip);
        AddButton(playback, "Stop / locomotion", StopClip);
        content.AddChild(playback);

        content.AddChild(new HSeparator());
        content.AddChild(new Label { Text = "Movement" });
        var movement = new HBoxContainer();
        AddButton(movement, "Walk 4 m", () => RequestMovement(NpcAnimationTestMovement.Walk));
        AddButton(movement, "Run 4 m", () => RequestMovement(NpcAnimationTestMovement.Run));
        AddButton(movement, "Stop move", () => RequestMovement(NpcAnimationTestMovement.Stop));
        content.AddChild(movement);

        content.AddChild(new HSeparator());
        content.AddChild(new Label { Text = "Interaction tests (normal reach and rules apply)" });
        var targeting = new HBoxContainer();
        AddButton(targeting, "Set target", () => RequestAction(NpcAnimationTestAction.SelectTarget));
        AddButton(targeting, "Greet", () => RequestAction(NpcAnimationTestAction.Greet));
        AddButton(targeting, "Interact / feed / pet", () => RequestAction(NpcAnimationTestAction.Interact));
        content.AddChild(targeting);
        var combat = new HBoxContainer();
        AddButton(combat, "Left strike", () => RequestAction(NpcAnimationTestAction.AttackLeft));
        AddButton(combat, "Right strike", () => RequestAction(NpcAnimationTestAction.AttackRight));
        content.AddChild(combat);
        var orders = new HBoxContainer();
        AddButton(orders, "Follow player", () => RequestAction(NpcAnimationTestAction.Follow));
        AddButton(orders, "Stay here", () => RequestAction(NpcAnimationTestAction.Stay));
        content.AddChild(orders);

        _status = new Label { Name = "NpcAnimationTestStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(_status);

        var theme = new Theme { DefaultFontSize = 15 };
        theme.SetStylebox("panel", "PanelContainer", new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.075f, 0.11f, 0.98f),
            BorderColor = new Color(0.22f, 0.32f, 0.43f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        });
        panel.Theme = theme;
    }

    public void Open(IReadOnlyList<NpcAnimationTestActor> actors, Func<string, EntityView?> resolveView)
    {
        _resolveView = resolveView;
        _root.Visible = true;
        SetActors(actors);
    }

    public void SetActors(IReadOnlyList<NpcAnimationTestActor> actors)
    {
        _actors = actors;
        PopulateActors();
    }

    public void SetStatus(string message)
    {
        if (_status is not null) _status.Text = message;
    }

    public void Close()
    {
        if (!IsOpen) return;
        _root.Visible = false;
        Closed?.Invoke();
    }

    private void PopulateActors()
    {
        if (_actorChoice is null) return;
        var previousId = SelectedActorId();
        _actorChoice.Clear();
        var selectedIndex = -1;
        foreach (var actor in _actors)
        {
            var index = _actorChoice.ItemCount;
            _actorChoice.AddItem(actor.Label);
            _actorChoice.SetItemMetadata(index, actor.Id);
            if (actor.Id == previousId) selectedIndex = index;
        }

        if (_actorChoice.ItemCount == 0)
        {
            _animationChoice.Clear();
            _status.Text = "No active non-player model views are available.";
            return;
        }

        _actorChoice.Select(selectedIndex >= 0 ? selectedIndex : 0);
        RefreshAnimations();
    }

    private void RefreshAnimations()
    {
        if (_animationChoice is null) return;
        _animationChoice.Clear();
        var actorId = SelectedActorId();
        var view = actorId is null ? null : _resolveView?.Invoke(actorId);
        if (view is null)
        {
            _status.Text = "The selected entity has no active model view.";
            return;
        }

        foreach (var animation in view.PresentationAnimationNames)
            _animationChoice.AddItem(animation);
        if (_animationChoice.ItemCount > 0)
            _animationChoice.Select(0);
        _status.Text = _animationChoice.ItemCount == 0
            ? "This model has no AnimationPlayer clips."
            : $"{actorId}: {_animationChoice.ItemCount} clips available. Facial clips play as standalone clips; upper/lower-body blending is not wired yet.";
    }

    private void PlayClip()
    {
        var actorId = SelectedActorId();
        var view = actorId is null ? null : _resolveView?.Invoke(actorId);
        if (view is null || _animationChoice.Selected < 0)
        {
            _status.Text = "Select an entity and an animation clip first.";
            return;
        }

        var clip = _animationChoice.GetItemText(_animationChoice.Selected);
        _status.Text = view.PlayPresentationAnimationForTesting(clip, _loopChoice.ButtonPressed)
            ? $"Playing {clip} on {actorId}."
            : $"{clip} is no longer available on {actorId}. Refresh actors to reload its clip list.";
    }

    private void StopClip()
    {
        var actorId = SelectedActorId();
        var view = actorId is null ? null : _resolveView?.Invoke(actorId);
        if (view is null) return;
        view.StopPresentationAnimationForTesting();
        _status.Text = $"Stopped manual playback for {actorId}; locomotion playback resumed.";
    }

    private void RequestMovement(NpcAnimationTestMovement movement)
    {
        if (SelectedActorId() is { } actorId)
            MovementRequested?.Invoke(actorId, movement);
    }

    private void RequestAction(NpcAnimationTestAction action)
    {
        if (SelectedActorId() is { } actorId)
            ActionRequested?.Invoke(actorId, action);
    }

    private string? SelectedActorId()
    {
        if (_actorChoice is null || _actorChoice.Selected < 0) return null;
        var metadata = _actorChoice.GetItemMetadata(_actorChoice.Selected);
        return metadata.VariantType == Variant.Type.String ? metadata.AsString() : null;
    }

    private static void AddButton(Control parent, string text, Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += pressed;
        parent.AddChild(button);
    }
}
