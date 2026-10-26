using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private NpcAnimationTestPanel? _npcAnimationTestPanel;
    private readonly Dictionary<string, System.Numerics.Vector3> _npcAnimationTestRouteTargets = new(StringComparer.Ordinal);

    private bool IsNpcAnimationTestMoving(string entityId) => _npcAnimationTestRouteTargets.ContainsKey(entityId);

    private void ToggleNpcAnimationTestPanel()
    {
        if (_npcAnimationTestPanel?.IsOpen == true)
        {
            _npcAnimationTestPanel.Close();
            return;
        }

        if (_npcAnimationTestPanel is null)
        {
            _npcAnimationTestPanel = new NpcAnimationTestPanel();
            _npcAnimationTestPanel.ActionRequested += HandleNpcAnimationTestAction;
            _npcAnimationTestPanel.MovementRequested += HandleNpcAnimationTestMovement;
            _npcAnimationTestPanel.RefreshRequested += RefreshNpcAnimationTestActors;
            _npcAnimationTestPanel.Closed += () => UpdateGameplayFeedback(0);
            AddChild(_npcAnimationTestPanel);
        }

        if (_menuState.ActiveMenu is not null)
        {
            _menuState.Close();
            ApplyMenuState();
        }

        _npcAnimationTestPanel.Open(GetNpcAnimationTestActors(), id => _entityViews.GetValueOrDefault(id));
        _playerCharacter.MouseLookEnabled = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private IReadOnlyList<NpcAnimationTestActor> GetNpcAnimationTestActors() => _world.Entities
        .Where(entity => entity.Id != _world.PlayerEntityId && !entity.Health.IsDead && _entityViews.ContainsKey(entity.Id))
        .OrderBy(entity => entity.Id, StringComparer.Ordinal)
        .Select(entity => new NpcAnimationTestActor(entity.Id,
            StarterNpcPresentationCatalog.TryGet(entity.Id, entity.Sex, out var presentation)
                ? $"{presentation!.DisplayName} · {entity.Id}"
                : $"{entity.SpeciesId} · {entity.Id}"))
        .ToArray();

    private void RefreshNpcAnimationTestActors()
    {
        _npcAnimationTestPanel?.SetActors(GetNpcAnimationTestActors());
    }

    private void HandleNpcAnimationTestMovement(string entityId, NpcAnimationTestMovement movement)
    {
        if (!_entityViews.TryGetValue(entityId, out var view)
            || !_world.TryGetEntity(entityId, out var entity) || entity is null)
            return;

        if (movement == NpcAnimationTestMovement.Stop)
        {
            StopNpcAnimationTestMovement(entityId);
            _lastActionMessage = $"Stopped animation test movement for {entityId}.";
            UpdateStatus();
            return;
        }

        view.StopPresentationAnimationForTesting();
        var destination = new System.Numerics.Vector3(entity.Position.X + 4f, entity.Position.Y, entity.Position.Z);
        view.SetAnimationTestMovementMultiplier(movement == NpcAnimationTestMovement.Walk ? 0.45f : 1f);
        _npcAnimationTestRouteTargets[entityId] = destination;
        var result = view.RequestNavigation(destination);
        _lastActionMessage = $"{movement} test route for {entityId}: {result}.";
        UpdateStatus();
    }

    private void AdvanceNpcAnimationTestRoutes()
    {
        foreach (var (entityId, target) in _npcAnimationTestRouteTargets.ToArray())
        {
            if (!_world.TryGetEntity(entityId, out var entity) || entity is null || entity.Health.IsDead
                || !_entityViews.TryGetValue(entityId, out var view))
            {
                StopNpcAnimationTestMovement(entityId);
                continue;
            }

            if (view.NavigationOutcome is { } outcome && outcome != NavigationOutcome.InProgress)
            {
                StopNpcAnimationTestMovement(entityId);
                _lastActionMessage = outcome == NavigationOutcome.Arrived
                    ? $"Completed the test route for {entityId}."
                    : $"Stopped the test route for {entityId}: navigation {outcome}.";
                UpdateStatus();
                _npcAnimationTestPanel?.SetStatus(_lastActionMessage);
                continue;
            }

            var position = new System.Numerics.Vector3(entity.Position.X, entity.Position.Y, entity.Position.Z);
            var horizontalTarget = new System.Numerics.Vector3(target.X, position.Y, target.Z);
            if (System.Numerics.Vector3.DistanceSquared(position, horizontalTarget) <= 0.35f * 0.35f)
            {
                StopNpcAnimationTestMovement(entityId);
                continue;
            }

            if (view.NavigationTarget is not { } current
                || System.Numerics.Vector3.DistanceSquared(current, target) > 0.01f)
                view.RequestNavigation(target);
        }
    }

    internal void ValidateNpcAnimationTestRouteFailureRecovery()
    {
        var entity = _world.Entities.FirstOrDefault(candidate => candidate.Id != _world.PlayerEntityId
            && !candidate.Health.IsDead && _entityViews.ContainsKey(candidate.Id))
            ?? throw new InvalidOperationException("NPC Test Lab route validation requires a live NPC view.");
        var view = _entityViews[entity.Id];
        var target = new System.Numerics.Vector3(entity.Position.X + 4f, entity.Position.Y, entity.Position.Z);
        view.SetAnimationTestMovementMultiplier(0.45f);
        _npcAnimationTestRouteTargets[entity.Id] = target;
        var requestId = view.RequestNavigation(target);

        try
        {
            var outcome = view.NavigationAdapter.Poll(requestId, 9d);
            if (outcome != NavigationOutcome.TimedOut)
                throw new InvalidOperationException($"NPC Test Lab timeout fixture ended as {outcome}, expected TimedOut.");

            AdvanceNpcAnimationTestRoutes();
            if (IsNpcAnimationTestMoving(entity.Id))
                throw new InvalidOperationException("NPC Test Lab kept suppressing autonomous behavior after a timed-out route.");
            if (view.NavigationOutcome != NavigationOutcome.TimedOut)
                throw new InvalidOperationException("NPC Test Lab route cleanup lost the terminal navigation outcome.");
        }
        finally
        {
            StopNpcAnimationTestMovement(entity.Id);
        }
    }

    private void StopNpcAnimationTestMovement(string entityId)
    {
        _npcAnimationTestRouteTargets.Remove(entityId);
        if (_entityViews.TryGetValue(entityId, out var view))
        {
            view.CancelNavigation();
            view.SetAnimationTestMovementMultiplier(1f);
            view.StopPresentationAnimationForTesting();
        }

    }

    private void ResetNpcAnimationTestRoutes()
    {
        foreach (var entityId in _npcAnimationTestRouteTargets.Keys.ToArray())
            StopNpcAnimationTestMovement(entityId);
    }

    private void HandleNpcAnimationTestAction(string entityId, NpcAnimationTestAction action)
    {
        if (!_world.TryGetEntity(entityId, out var entity) || entity is null)
        {
            _lastActionMessage = $"Test target {entityId} is unavailable.";
            UpdateStatus();
            return;
        }

        switch (action)
        {
            case NpcAnimationTestAction.SelectTarget:
                SelectNpcTestTarget(entityId);
                break;
            case NpcAnimationTestAction.Greet:
                SelectNpcTestTarget(entityId);
                PerformGreeting();
                break;
            case NpcAnimationTestAction.Interact:
                SelectNpcTestTarget(entityId);
                PerformFriendlyInteraction();
                break;
            case NpcAnimationTestAction.AttackLeft:
            case NpcAnimationTestAction.AttackRight:
                SelectNpcTestTarget(entityId);
                var left = action == NpcAnimationTestAction.AttackLeft;
                if (!_playerCharacter.StartAttackForTesting(left))
                {
                    _lastActionMessage = left
                        ? "Left strike did not start; it may be shielded, cooling down, or already active."
                        : "Right strike did not start; it may be shielded, cooling down, or already active.";
                    UpdateStatus();
                }
                break;
            case NpcAnimationTestAction.Follow:
                StopNpcAnimationTestMovement(entityId);
                IssueIndividualOrder(entityId, "follow");
                break;
            case NpcAnimationTestAction.Stay:
                StopNpcAnimationTestMovement(entityId);
                IssueIndividualOrder(entityId, "stay");
                break;
        }
        _npcAnimationTestPanel?.SetStatus(_lastActionMessage);
    }

    private void SelectNpcTestTarget(string entityId)
    {
        _selectedTarget = new WorldTarget(TargetKind.Entity, entityId);
        SelectEntity(entityId);
        _lastActionMessage = $"Selected {entityId} as the interaction target.";
        UpdateWorldSelection();
    }
}
