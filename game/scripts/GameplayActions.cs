using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private const double PlayerMeleeReach = 2.2;
    private readonly CollectUseGesture _collectUse = new();
    private WorldTarget? _collectTarget;
    private string? _swingTargetId;
    private Label _actionHud = null!;
    private Label3D _targetResponse = null!;
    private string? _responseTargetId;
    private double _feedbackSeconds;

    private bool GameplayActionsAllowed => _sessionActive && _menuState.ActiveMenu is null && !_clock.IsPaused;

    private void ConfigureGameplayActions()
    {
        _actionHud = new Label { Name = "ActionFeedback", Position = new Vector2(20, 88),
            Size = new Vector2(620, 110), AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        _actionHud.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _actionHud.AddThemeConstantOverride("shadow_offset_x", 2);
        _actionHud.AddThemeConstantOverride("shadow_offset_y", 2);
        _menuHud.AddChild(_actionHud);
        _targetResponse = new Label3D { Name = "TargetResponse", Visible = false,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, PixelSize = 0.008f };
        AddChild(_targetResponse);
        GetWindow().FocusExited += CancelGameplayActions;
        _playerCharacter.AttackStarted += () =>
        {
            _swingTargetId = GameplayActionsAllowed ? _selectedEntityId : null;
            ShowActionFeedback(_swingTargetId is null ? "Select an entity with the target key before attacking." : "Swinging…");
        };
        _playerCharacter.StrikeReleased += ResolvePlayerStrike;
    }

    private void CancelGameplayActions()
    {
        _collectUse.Cancel(); _collectTarget = null; _swingTargetId = null;
        _feedbackSeconds = 0; _responseTargetId = null;
        if (_targetResponse is not null) _targetResponse.Visible = false;
        if (_actionHud is not null) _actionHud.Text = "";
    }

    private void ShowActionFeedback(string message, string? targetId = null, string? response = null)
    {
        _lastActionMessage = message; _feedbackSeconds = 6;
        _responseTargetId = response is null ? null : targetId;
        _targetResponse.Text = response ?? "";
        UpdateStatus();
    }

    private void UpdateGameplayFeedback(double delta)
    {
        if (_actionHud is null) return;
        _feedbackSeconds = Math.Max(0, _feedbackSeconds - delta);
        _actionHud.Visible = _sessionActive && _menuState.ActiveMenu is null;
        var collect = PlayerButtonBindings.Label(_buttonBindings.Get(ControllerActions.Collect).Desktop);
        var interact = PlayerButtonBindings.Label(_buttonBindings.Get(ControllerActions.Interact).Desktop);
        var greet = PlayerButtonBindings.Label(_buttonBindings.Get(ControllerActions.Greet).Desktop);
        var hint = $"Tap {collect}: collect · Hold {collect} 0.6s: use carried food · {interact}: feed/pet · {greet}: greet (2 m)";
        _actionHud.Text = _collectUse.IsPressed && !_collectUse.Used
            ? $"Hold to use… {Math.Min(100, _collectUse.Elapsed / CollectUseGesture.HoldSeconds * 100):0}%\nRelease now to collect.\n{hint}"
            : (_feedbackSeconds > 0 ? _lastActionMessage + "\n" : "") + hint;
        _targetResponse.Visible = false;
        if (!_actionHud.Visible || _feedbackSeconds <= 0 || _responseTargetId is null) return;
        var targets = WorldTargets();
        var view = targets.FirstOrDefault(target => target.Target == new WorldTarget(TargetKind.Entity, _responseTargetId));
        var camera = GetViewport().GetCamera3D();
        if (view is null || camera is null || !TargetVisible(view, camera, targets)) return;
        _targetResponse.GlobalPosition = new Vector3(view.Center.X, view.Bounds.End.Y + 0.35f, view.Center.Z);
        _targetResponse.Visible = true;
    }

    private void BeginCollectUse()
    {
        if (_collectUse.IsPressed) return;
        _collectTarget = _selectedTarget;
        _collectUse.Press();
        UpdateGameplayFeedback(0);
    }
    private void AdvanceCollectUse(double seconds)
    {
        if (!GameplayActionsAllowed) { _collectUse.Cancel(); _collectTarget = null; return; }
        if (_collectUse.Advance(seconds) == CollectUseIntent.Use)
            ShowActionFeedback(new PlayerResourceService(_world, _speciesCatalog).UseCarriedFood().Message);
    }
    private void CompleteCollectUse()
    {
        if (_collectUse.Release() != CollectUseIntent.Collect) return;
        SyncPlayerPosition();
        var service = new PlayerResourceService(_world, _speciesCatalog);
        var target = _collectTarget;
        _collectTarget = null;
        if (target?.Kind == TargetKind.Entity) { ShowActionFeedback("Select a resource to collect, or clear the target first."); return; }
        if (target is null)
        {
            var visible = VisibleWorldTargets().Select(view => view.Target).ToHashSet();
            var nearest = service.Targets.Where(resource => service.HasAvailableItems(resource)
                && visible.Contains(new WorldTarget((TargetKind)((int)resource.Kind + 1), resource.Id)))
                .OrderBy(resource => System.Numerics.Vector3.Distance(resource.Position, _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId).Position))
                .ThenBy(resource => resource.Id, StringComparer.Ordinal).FirstOrDefault();
            if (nearest is null) { ShowActionFeedback("No visible resource to collect."); return; }
            target = new((TargetKind)((int)nearest.Kind + 1), nearest.Id);
        }
        var views = WorldTargets();
        var resourceView = views.FirstOrDefault(view => view.Target == target);
        if (resourceView is null) { ShowActionFeedback("The resource no longer exists."); return; }
        if (!resourceView.Samples.Any(point => TargetLineClear(_playerCharacter.GlobalPosition + Vector3.Up, point, target, views)))
        { ShowActionFeedback("Collection blocked by an obstacle."); return; }
        ShowActionFeedback(service.Collect((PlayerResourceKind)((int)target.Kind - 1), target.Id).Message);
    }

    private void ResolvePlayerStrike(bool left, string bodyPart)
    {
        var id = _swingTargetId; _swingTargetId = null;
        if (!GameplayActionsAllowed) return;
        SyncPlayerPosition();
        if (id is null || !_world.TryGetEntity(id, out var defender) || defender is null)
        { ShowActionFeedback("Miss: the selected entity is unavailable."); return; }
        var targets = WorldTargets();
        var view = targets.FirstOrDefault(view => view.Target == new WorldTarget(TargetKind.Entity, id));
        var direction = new Vector3(defender.Position.X, 0, defender.Position.Z) - new Vector3(_playerCharacter.GlobalPosition.X, 0, _playerCharacter.GlobalPosition.Z);
        if (direction.LengthSquared() > 0.01f && (-_playerCharacter.GlobalBasis.Z).Dot(direction.Normalized()) < 0.5f)
        { ShowActionFeedback("Miss: face the target before attacking."); return; }
        if (view is null || !view.Samples.Any(point => TargetLineClear(_playerCharacter.GlobalPosition + Vector3.Up, point, view.Target, targets)))
        { ShowActionFeedback("Miss: the target is blocked."); return; }
        var player = _world.Entities.Single(entity => entity.Id == _world.PlayerEntityId);
        var combat = new SocialMeleeCombatService(_world, _relationshipEventCatalog)
            .Resolve(new(player.Id, id, bodyPart, 10 * player.BaseStats.Strength), PlayerMeleeReach).Combat;
        ShowActionFeedback(combat.Result == MeleeAttackResult.Success
            ? $"Hit {defender.SpeciesId} {bodyPart} for {combat.AppliedDamage:0.#} damage."
            : $"Miss: {combat.Result}. Melee reach is {PlayerMeleeReach:0.#} m.",
            id, combat.Result == MeleeAttackResult.Success ? $"-{combat.AppliedDamage:0.#} {bodyPart}" : null);
    }

    private void PerformFriendlyInteraction()
    {
        if (!GameplayActionsAllowed) return;
        SyncPlayerPosition();
        if (_selectedTarget is null)
        {
            ShowActionFeedback("No target selected.");
            return;
        }

        if (_world.PlayerEntityId is not { } playerId || !_world.TryGetEntity(playerId, out var player) || player is null)
        {
            ShowActionFeedback("Player not found.");
            return;
        }

        // 1. Structures -> Toggle Door
        var structure = _world.Structures.FirstOrDefault(s => s.Id == _selectedTarget.Id);
        if (structure != null)
        {
            var result = _interactionResolver.Resolve(new InteractionRequest(
                playerId,
                _selectedTarget.Id,
                InteractionType.Physical,
                "toggle_door"));
            ShowActionFeedback(result.Reason);
            return;
        }

        // 2. Entities
        if (_selectedTarget.Kind != TargetKind.Entity || !_world.TryGetEntity(_selectedTarget.Id, out var target) || target is null)
        {
            ShowActionFeedback("Target not found.");
            return;
        }

        if (string.Equals(target.SpeciesId, "human", StringComparison.Ordinal))
        {
            ShowActionFeedback($"Press {PlayerButtonBindings.Label(_buttonBindings.Get(ControllerActions.Greet).Desktop)} to greet this person.");
            return;
        }
        else
        {
            // Creature: Feed if possible, otherwise Pet
            var species = _speciesCatalog.GetRequired(target.SpeciesId);
            var foodItem = player.Inventory
                .Where(item => item.Value > 0 && species.Diet.Contains(item.Key))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .FirstOrDefault();

            if (foodItem.Key != null)
            {
                var itemContainer = new ResourceContainer("player-hand", 1);
                itemContainer.TryAdd(foodItem.Key, 1);

                var result = _interactionResolver.Resolve(new InteractionRequest(
                    playerId,
                    target.Id,
                    InteractionType.Physical,
                    "feed",
                    itemContainer));
                ShowEntityInteractionResult(result, target.Id, "Food accepted");
            }
            else
            {
                var result = _interactionResolver.Resolve(new InteractionRequest(
                    playerId,
                    target.Id,
                    InteractionType.Physical,
                    "pet"));
                ShowEntityInteractionResult(result, target.Id, "Pet acknowledged");
            }
        }
    }

    private void PerformGreeting()
    {
        if (!GameplayActionsAllowed) return;
        SyncPlayerPosition();
        if (_selectedTarget is null)
        {
            ShowActionFeedback("No target selected.");
            return;
        }

        if (_world.PlayerEntityId is not { } playerId || !_world.TryGetEntity(playerId, out var player))
        {
            ShowActionFeedback("Player not found.");
            return;
        }

        if (!_world.TryGetEntity(_selectedTarget.Id, out var target) || target is null)
        {
            ShowActionFeedback("Target not found.");
            return;
        }

        if (!string.Equals(target.SpeciesId, "human", StringComparison.Ordinal))
        {
            var reason = GreetSelectedEntity();
            ShowEntityInteractionResult(new InteractionResult(reason.StartsWith("Greeted", StringComparison.Ordinal), reason), target.Id, "Friendly greeting acknowledged");
            return;
        }

        var result = _interactionResolver.Resolve(new InteractionRequest(
            playerId,
            target.Id,
            InteractionType.Social,
            "greet"));

        ShowEntityInteractionResult(result, target.Id, "Friendly greeting acknowledged");
    }

    private void ShowEntityInteractionResult(InteractionResult result, string targetId, string acknowledgment)
    {
        if (result.Succeeded)
        {
            if (_entityViews.TryGetValue(targetId, out var view))
                view.AcknowledgeGreeting(() => _world.TryGetEntity(_playerCharacter.EntityId, out _) ? _playerCharacter.GlobalPosition : null);
            ShowActionFeedback(result.Reason, targetId, acknowledgment);
        }
        else ShowActionFeedback(result.Reason);
    }
}
