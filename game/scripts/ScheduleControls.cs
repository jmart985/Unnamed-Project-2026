using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private SpinBox _workStart = null!;
    private SpinBox _workEnd = null!;
    private Button _applySchedule = null!;

    private static string ScheduleTime(double hour) => $"{(int)hour:00}:{(int)((hour % 1) * 60):00}";

    private void ConfigureScheduleControls()
    {
        var controls = GetNode<VBoxContainer>("Controls/Panel/Scroll/Margin/VBox");
        var label = new Label { Text = "Selected NPC work hours (0–23; overnight allowed)", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        controls.AddChild(label);
        controls.MoveChild(label, 11);
        var row = new HBoxContainer();
        controls.AddChild(row);
        controls.MoveChild(row, 12);
        _workStart = new SpinBox { MinValue = 0, MaxValue = 23, Step = 1, Value = 8, TooltipText = "Work begins at this hour" };
        _workEnd = new SpinBox { MinValue = 0, MaxValue = 23, Step = 1, Value = 18, TooltipText = "Rest begins at this hour" };
        row.AddChild(new Label { Text = "Start" });
        row.AddChild(_workStart);
        row.AddChild(new Label { Text = "End" });
        row.AddChild(_workEnd);
        _applySchedule = new Button { Text = "Apply hours", Disabled = true };
        _applySchedule.Pressed += () =>
        {
            var result = _world.SetWorkSchedule(_selectedEntityId, (int)_workStart.Value, (int)_workEnd.Value);
            _lastActionMessage = result == ScheduleUpdateResult.InvalidHours
                ? "Work start and end must be different hours." : $"Work schedule: {result}.";
            UpdateStatus();
        };
        row.AddChild(_applySchedule);
    }

    private void ShowSelectedSchedule(string entityId)
    {
        var schedule = _world.GetWorkSchedule(entityId);
        _workStart.Value = schedule.StartHour;
        _workEnd.Value = schedule.EndHour;
    }
}
