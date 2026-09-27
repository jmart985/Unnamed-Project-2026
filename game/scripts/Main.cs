using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main : Node3D
{
    private readonly SimulationClock _clock = new();
    private MeshInstance3D _map = null!;
    private Label _statusLabel = null!;
    private Button _pauseButton = null!;

    public override void _Ready()
    {
        _map = GetNode<MeshInstance3D>("Map");
        ResetScenario();
        _statusLabel = GetNode<Label>("Controls/Panel/Margin/VBox/Status");
        _pauseButton = GetNode<Button>("Controls/Panel/Margin/VBox/Pause");
        _pauseButton.Pressed += TogglePause;
        GetNode<Button>("Controls/Panel/Margin/VBox/OneX").Pressed += () => _clock.SetSpeed(SimulationSpeed.OneX);
        GetNode<Button>("Controls/Panel/Margin/VBox/FourX").Pressed += () => _clock.SetSpeed(SimulationSpeed.FourX);
        GetNode<Button>("Controls/Panel/Margin/VBox/Reset").Pressed += ResetScenario;
        UpdateStatus();
    }

    public override void _Process(double delta)
    {
        _clock.Advance(delta);
        UpdateStatus();
    }

    private void TogglePause()
    {
        _clock.SetPaused(!_clock.IsPaused);
        UpdateStatus();
    }

    private void ResetScenario()
    {
        _clock.Reset();
        if (_map is not null)
        {
            _map.Mesh = new PlaneMesh { Size = new Vector2(20, 20) };
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Text = $"Simulation time: {_clock.SimulationTime:0.0}s\nSpeed: {(int)_clock.Speed}x";
        _pauseButton.Text = _clock.IsPaused ? "Resume" : "Pause";
    }
}
