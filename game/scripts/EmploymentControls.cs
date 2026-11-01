using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private Label _employmentSummary = null!;
    private OptionButton _employmentPositions = null!;
    private OptionButton _employmentStockpiles = null!;
    private CheckButton _autoAssignWork = null!;
    private Button _assignEmployment = null!;
    private Button _releaseEmployment = null!;
    private Button _removeEmploymentPosition = null!;
    private string _employmentChoicesSignature = string.Empty;

    private void ConfigureEmploymentControls()
    {
        var panel = new VBoxContainer { Name = "Employment" };
        _settlementDetails.AddChild(panel);
        _settlementDetails.MoveChild(panel, 2);
        _employmentSummary = MenuLabel();
        panel.AddChild(_employmentSummary);
        _autoAssignWork = new CheckButton { Text = "Automatically find a job when unemployed" };
        panel.AddChild(_autoAssignWork);
        _autoAssignWork.Toggled += enabled => EmploymentFeedback(_world.SetWorkerAutoAssignment(_selectedEntityId, enabled));
        _employmentPositions = new OptionButton { FitToLongestItem = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddChild(_employmentPositions);
        _employmentPositions.ItemSelected += _ => RefreshEmploymentControls();
        var actions = new HBoxContainer();
        panel.AddChild(actions);
        _assignEmployment = AddMenuButton(actions, "Assign selected NPC", () =>
            EmploymentFeedback(_world.AssignWorkerToPosition(_selectedEntityId, SelectedEmploymentPosition())));
        _releaseEmployment = AddMenuButton(actions, "Unassign and pause auto", () =>
            EmploymentFeedback(_world.UnassignWorker(_selectedEntityId)));
        _removeEmploymentPosition = AddMenuButton(actions, "Remove vacancy", () =>
        {
            _lastActionMessage = _world.RemoveWorkplacePosition(SelectedEmploymentPosition())
                ? "Workplace vacancy removed." : "Select an existing vacancy.";
            UpdateStatus();
        });
        _employmentStockpiles = new OptionButton { FitToLongestItem = false };
        panel.AddChild(_employmentStockpiles);
        AddMenuButton(panel, "Add wood-hauler position", () =>
        {
            if (_employmentStockpiles.Selected < 0) return;
            var target = _employmentStockpiles.GetItemMetadata(_employmentStockpiles.Selected).AsString();
            var index = 1;
            while (_world.WorkplacePositions.Any(position => position.Id == $"{target}-hauler-{index:00}")) index++;
            EmploymentFeedback(_world.AddWorkplacePosition($"{target}-hauler-{index:00}", target));
        });
        ShowSettlementTab();
    }

    private string? SelectedEmploymentPosition() => _employmentPositions.Selected < 0 ? null
        : _employmentPositions.GetItemMetadata(_employmentPositions.Selected).AsString();

    private void EmploymentFeedback(EmploymentResult result)
    {
        _lastActionMessage = $"Employment: {result}.";
        UpdateStatus();
    }

    private void RefreshEmploymentControls()
    {
        if (_employmentSummary is null) return;
        var positions = _world.WorkplacePositions;
        var stockpiles = _world.Stockpiles.OrderBy(stockpile => stockpile.Id, StringComparer.Ordinal).ToArray();
        var signature = string.Join("|", positions.Select(position => $"{position.Id}:{position.WorkerId}"))
            + "/" + string.Join("|", stockpiles.Select(stockpile => stockpile.Id));
        if (_employmentChoicesSignature != signature)
        {
            _employmentChoicesSignature = signature;
            var selected = SelectedEmploymentPosition();
            var selectedStockpile = _employmentStockpiles.Selected < 0 ? null
                : _employmentStockpiles.GetItemMetadata(_employmentStockpiles.Selected).AsString();
            _employmentPositions.Clear();
            foreach (var position in positions)
            {
                _employmentPositions.AddItem($"Wood hauler · {position.Id} · {position.WorkerId ?? "Vacant"}");
                _employmentPositions.SetItemMetadata(_employmentPositions.ItemCount - 1, position.Id);
            }
            if (positions.Count > 0) _employmentPositions.Select(Math.Max(0,
                positions.ToList().FindIndex(position => position.Id == selected)));
            _employmentStockpiles.Clear();
            foreach (var stockpile in stockpiles)
            {
                _employmentStockpiles.AddItem(stockpile.Id);
                _employmentStockpiles.SetItemMetadata(_employmentStockpiles.ItemCount - 1, stockpile.Id);
            }
            if (stockpiles.Length > 0) _employmentStockpiles.Select(Math.Max(0,
                Array.FindIndex(stockpiles, stockpile => stockpile.Id == selectedStockpile)));
        }
        var resident = _world.SettlementWorkers.FirstOrDefault(worker => worker.EntityId == _selectedEntityId);
        var employment = _world.GetEmployment(_selectedEntityId);
        var selectedPosition = positions.FirstOrDefault(position => position.Id == SelectedEmploymentPosition());
        _employmentSummary.Text = $"WORKPLACES · {positions.Count(position => position.WorkerId is null)} / {positions.Count} vacant\n"
            + (resident is null ? "Selected entity is not enrolled for settlement work."
                : employment is null ? "Employment: Unemployed\n" + (resident.AutoAssign
                    ? "Waiting for a suitable vacancy." : "Automatic job assignment is paused.")
                : $"Employment: Wood hauler at {employment.WorkplaceId}");
        _autoAssignWork.Disabled = resident is null;
        _autoAssignWork.SetPressedNoSignal(resident?.AutoAssign ?? false);
        _assignEmployment.Disabled = resident is null || employment is not null || selectedPosition is null
            || selectedPosition.WorkerId is not null;
        _releaseEmployment.Disabled = employment is null;
        _removeEmploymentPosition.Disabled = selectedPosition is null || selectedPosition.WorkerId is not null;
    }
}
