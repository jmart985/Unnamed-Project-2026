using Godot;

namespace UnnamedProject2026;

public sealed record ControlWheelEntry(string Label, Action? Execute);

/// A live selection overlay: movement is retained and look is owned by the wheel.
public partial class ControlWheel : Control
{
    private readonly List<Label> _labels = [];
    private IReadOnlyList<ControlWheelEntry> _entries = [];
    private Label _heading = null!;
    public bool IsOpen => Visible;
    public string Opener { get; private set; } = "";
    public int Selection { get; private set; } = -1;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _heading = new Label { HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore };
        _heading.AddThemeFontSizeOverride("font_size", 18);
        AddChild(_heading);
        Hide();
    }

    public void Open(string title, string opener, IReadOnlyList<ControlWheelEntry> entries, string confirmationHint)
    {
        _entries = entries;
        Opener = opener;
        Selection = -1;
        foreach (var label in _labels) label.QueueFree();
        _labels.Clear();
        _heading.Text = title + "\nMove to choose · center cancels\n" + confirmationHint;
        foreach (var entry in entries)
        {
            var label = new Label { Text = entry.Label, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(135, 65) };
            label.AddThemeFontSizeOverride("font_size", 17);
            AddChild(label);
            _labels.Add(label);
        }
        Show();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Input.WarpMouse(GetViewportRect().GetCenter());
        LayoutLabels();
        QueueRedraw();
    }

    public void Pointer(Vector2 position) => SelectDirection(position - GetViewportRect().GetCenter(), 65);
    public void Stick(Vector2 direction) => SelectDirection(direction, 0.35f);
    private void SelectDirection(Vector2 direction, float deadzone)
    {
        Selection = direction.Length() < deadzone || _entries.Count == 0 ? -1
            : (Mathf.RoundToInt((Mathf.Atan2(direction.Y, direction.X) + Mathf.Pi / 2 + Mathf.Tau)
                / Mathf.Tau * _entries.Count) % _entries.Count);
        QueueRedraw();
    }

    public Action? Close(bool confirm)
    {
        var action = confirm && Selection >= 0 && Selection < _entries.Count ? _entries[Selection].Execute : null;
        Hide();
        Selection = -1;
        Opener = "";
        return action;
    }

    private void LayoutLabels()
    {
        var center = GetViewportRect().GetCenter();
        _heading.Position = center + new Vector2(-130, -75);
        _heading.Size = new Vector2(260, 150);
        for (var i = 0; i < _labels.Count; i++)
        {
            var angle = Mathf.Tau * i / Math.Max(1, _labels.Count) - Mathf.Pi / 2;
            _labels[i].Position = center + Vector2.FromAngle(angle) * 225 - _labels[i].Size / 2;
        }
    }

    public override void _Draw()
    {
        if (!Visible) return;
        DrawRect(GetViewportRect(), new Color(0.015f, 0.025f, 0.04f, 0.48f));
        var center = GetViewportRect().GetCenter();
        DrawCircle(center, 145, new Color(0.04f, 0.06f, 0.10f, 0.97f));
        for (var i = 0; i < _labels.Count; i++)
        {
            var selected = i == Selection;
            DrawRect(new Rect2(_labels[i].Position, _labels[i].Size), selected ? new Color(0.12f, 0.36f, 0.30f, 0.98f)
                : new Color(0.04f, 0.06f, 0.10f, 0.95f));
            _labels[i].Modulate = _entries[i].Execute is null ? new Color(0.55f, 0.58f, 0.62f) : Colors.White;
        }
    }
}
