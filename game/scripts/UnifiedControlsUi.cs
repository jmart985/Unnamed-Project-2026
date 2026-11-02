using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private bool _captureAlternate;
    private readonly List<Action> _refreshControlPreferences = [];
    private ControlLayoutDiagram _controlDiagram = null!;
    private LineEdit _controlSearch = null!;
    private OptionButton _controlCategory = null!;
    private CheckBox _showReservedControls = null!;
    private readonly Dictionary<string, Control> _controlRows = new(StringComparer.Ordinal);

    private void ConfigureUnifiedControlsUi(VBoxContainer mapping)
    {
        _bindingStatus = MenuLabel(_controlSettings.Status);
        mapping.AddChild(_bindingStatus);
        mapping.AddChild(MenuLabel("LB/L1 is the utility modifier. B/Circle: tap dodge; hold 0.30s crouch. Reserved mechanics stay inactive.\n" +
            "A/Cross confirm · B/Circle back · X/Square item actions · Y/Triangle equipment visibility."));
        _controlDiagram = new ControlLayoutDiagram { Settings = _controlSettings, CustomMinimumSize = new Vector2(560, 225) };
        mapping.AddChild(_controlDiagram);
        var filters = new HBoxContainer();
        mapping.AddChild(filters);
        _controlSearch = new LineEdit { PlaceholderText = "Search controls", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        filters.AddChild(_controlSearch);
        _controlCategory = new OptionButton();
        _controlCategory.AddItem("All categories");
        foreach (var category in ControlCatalog.All.Select(d => d.Category).Distinct()) _controlCategory.AddItem(category);
        filters.AddChild(_controlCategory);
        _showReservedControls = new CheckBox { Text = "Show reserved" };
        filters.AddChild(_showReservedControls);
        var alternate = new CheckBox { Text = "Add alternate instead of replacing this device's bindings" };
        mapping.AddChild(alternate);
        alternate.Toggled += value => _captureAlternate = value;
        _controlSearch.TextChanged += _ => FilterUnifiedControls();
        _controlCategory.ItemSelected += _ => FilterUnifiedControls();
        _showReservedControls.Toggled += _ => FilterUnifiedControls();
        foreach (var definition in ControlCatalog.All)
        {
            var row = new HBoxContainer();
            mapping.AddChild(row);
            _controlRows.Add(definition.Id, row);
            row.AddChild(new Label { Text = definition.Label + (definition.Available ? "" : " [reserved]"),
                CustomMinimumSize = new Vector2(245, 0), TooltipText = definition.Contexts.ToString() });
            foreach (var controller in new[] { false, true })
            {
                var button = new Button { CustomMinimumSize = new Vector2(180, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                button.Pressed += () =>
                {
                    _captureModifierEvent = null;
                    _captureBinding = (definition.Id, controller);
                    _bindingStatus.Text = "Mapping " + definition.Label + ". Press a key/button or move an axis; Escape cancels.";
                    SynchronizeUnifiedContext();
                };
                _mappingButtons.Add((definition.Id, controller), button);
                row.AddChild(button);
            }
        }
        FilterUnifiedControls();
        var restore = new HBoxContainer();
        mapping.AddChild(restore);
        AddMenuButton(restore, "Reset category", () => ResetUnifiedControls(_controlCategory.Selected == 0
            ? null : _controlCategory.GetItemText(_controlCategory.Selected)));
        AddMenuButton(restore, "Restore default button mapping", () => ResetUnifiedControls(null));
        var preferences = new VBoxContainer();
        mapping.AddChild(preferences);
        AddControlPreference(preferences, "Toggle crouch (hold B again to change stance)", p => p.ToggleCrouch,
            value => _controlSettings.Preferences with { ToggleCrouch = value });
        AddControlPreference(preferences, "Toggle wheels instead of holding", p => p.ToggleWheels,
            value => _controlSettings.Preferences with { ToggleWheels = value });
        AddControlPreference(preferences, "Invert vertical look", p => p.InvertLookY,
            value => _controlSettings.Preferences with { InvertLookY = value });
        AddControlNumber(preferences, "Interaction hold (seconds)", 0.10, 2, 0.05, p => p.InteractionHoldSeconds,
            value => _controlSettings.Preferences with { InteractionHoldSeconds = value });
        AddControlNumber(preferences, "Crouch hold (seconds)", 0.10, 2, 0.05, p => p.CrouchHoldSeconds,
            value => _controlSettings.Preferences with { CrouchHoldSeconds = value });
        AddControlNumber(preferences, "Stick deadzone", 0.05, 0.60, 0.01, p => p.StickDeadzone,
            value => _controlSettings.Preferences with { StickDeadzone = (float)value });
        AddControlNumber(preferences, "Look sensitivity", 0.10, 5, 0.05, p => p.LookSensitivity,
            value => _controlSettings.Preferences with { LookSensitivity = (float)value });
    }

    private void FilterUnifiedControls()
    {
        if (_controlSearch is null) return;
        var category = _controlCategory.Selected <= 0 ? null : _controlCategory.GetItemText(_controlCategory.Selected);
        foreach (var definition in ControlCatalog.All)
            _controlRows[definition.Id].Visible = (_showReservedControls.ButtonPressed || definition.Available)
                && (category is null || definition.Category == category)
                && (definition.Label.Contains(_controlSearch.Text, StringComparison.OrdinalIgnoreCase)
                    || definition.Id.Contains(_controlSearch.Text, StringComparison.OrdinalIgnoreCase));
    }

    private void ResetUnifiedControls(string? category)
    {
        _captureBinding = null;
        _captureModifierEvent = null;
        _controlSettings.RestoreDefaults(category, out var reason);
        _bindingStatus.Text = reason;
        _inputRouter.Cancel();
        ApplyUnifiedProfile();
        RefreshMappingButtons();
        RefreshControlPreferences();
    }

    private void AddControlPreference(Control parent, string label, Func<ControlPreferences, bool> value, Func<bool, ControlPreferences> change)
    {
        var checkbox = new CheckBox { Text = label, ButtonPressed = value(_controlSettings.Preferences) };
        parent.AddChild(checkbox);
        checkbox.Toggled += selected => SaveUnifiedPreferences(change(selected));
        _refreshControlPreferences.Add(() => checkbox.SetPressedNoSignal(value(_controlSettings.Preferences)));
    }

    private void AddControlNumber(Control parent, string label, double minimum, double maximum, double step,
        Func<ControlPreferences, double> value, Func<double, ControlPreferences> change)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(260, 0) });
        var spinner = new SpinBox { MinValue = minimum, MaxValue = maximum, Step = step, Value = value(_controlSettings.Preferences) };
        row.AddChild(spinner);
        spinner.ValueChanged += selected => SaveUnifiedPreferences(change(selected));
        _refreshControlPreferences.Add(() => spinner.SetValueNoSignal(value(_controlSettings.Preferences)));
    }

    private void SaveUnifiedPreferences(ControlPreferences preferences)
    {
        _controlSettings.SetPreferences(preferences, out var reason);
        _bindingStatus.Text = reason;
        _inputRouter.Cancel();
        ApplyUnifiedProfile();
        RefreshControlPreferences();
    }

    private void RefreshControlPreferences()
    {
        foreach (var refresh in _refreshControlPreferences) refresh();
        _controlDiagram.QueueRedraw();
    }
}

/// A generated controller reference; labels follow the active profile.
public partial class ControlLayoutDiagram : Control
{
    public UnifiedControlSettings Settings { get; set; } = null!;
    public override void _Draw()
    {
        if (Settings is null) return;
        var font = ThemeDB.FallbackFont;
        var center = new Vector2(Size.X / 2, 115);
        DrawRect(new Rect2(center - new Vector2(150, 65), new Vector2(300, 130)), new Color(0.10f, 0.14f, 0.19f));
        DrawCircle(center + new Vector2(-100, 35), 30, new Color(0.18f, 0.24f, 0.30f));
        DrawCircle(center + new Vector2(60, 35), 30, new Color(0.18f, 0.24f, 0.30f));
        DrawString(font, center + new Vector2(-110, 40), "LS", fontSize: 17);
        DrawString(font, center + new Vector2(50, 40), "RS", fontSize: 17);
        foreach (var pair in new[] { ("Y", new Vector2(115, -45)), ("X", new Vector2(85, -15)),
            ("B", new Vector2(145, -15)), ("A", new Vector2(115, 15)) })
        {
            DrawCircle(center + pair.Item2, 15, new Color(0.22f, 0.36f, 0.40f));
            DrawString(font, center + pair.Item2 + new Vector2(-5, 6), pair.Item1, fontSize: 16);
        }
        var title = "Controller · Xbox / PlayStation";
        DrawString(font, new Vector2(12, 23), title, fontSize: 18);
        string Binding(string id) => UnifiedControlSettings.Label(Settings.Get(id).FirstOrDefault(b => b.Device is "joypad" or "axis"));
        DrawString(font, new Vector2(12, 52), "Primary: " + Binding(ControllerActions.PrimaryAction)
            + " · Secondary: " + Binding(ControllerActions.SecondaryAction), fontSize: 14);
        DrawString(font, new Vector2(12, 218), "Build: " + Binding(ControllerActions.BuildMode)
            + " · Inventory: " + Binding(ControllerActions.Inventory) + " · Pause: " + Binding(ControllerActions.Settings), fontSize: 14);
    }
}
