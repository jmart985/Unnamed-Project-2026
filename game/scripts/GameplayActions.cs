using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private void PerformRestAction()
    {
        if (_world.PlayerEntityId is not { } playerId) return;
        var rest = new SettlementRestService(_world);
        var result = _world.Entities.FirstOrDefault(entity => entity.Id == playerId)?.SleepState == SleepState.Sleeping
            ? rest.Wake(playerId)
            : _selectedTarget is { Kind: TargetKind.Structure } target
                ? rest.Sleep(playerId, target.Id)
                : new RestAction(RestActionResult.NoShelter, "Select a completed hut to rest.");
        ShowActionFeedback(result.Message);
        if (_world.PlayerEntityId is { } currentPlayer && _world.TryGetEntity(currentPlayer, out var player) && player is not null)
            _playerCharacter.SetGameplayInputEnabled(player.SleepState != SleepState.Sleeping);
        UpdateWorldSelection();
    }

    private const double PlayerMeleeReach = 2.2;
    private readonly CollectUseGesture _collectUse = new();
    private WorldTarget? _collectTarget;
    private string? _swingTargetId;
    private Label _actionHud = null!;
    private Label3D _targetResponse = null!;
    private string? _responseTargetId;
    private double _feedbackSeconds;
    private sealed record SocialPopup(string EntityId, Label3D Label)
    {
        public double Age { get; set; }
    }
    private readonly List<SocialPopup> _socialPopups = [];

    private void AttachSocialFeedback()
    {
        _world.RelationshipChanged += ShowRelationshipChange;
        _world.FactionStandingChanged += ShowFactionChange;
    }

    private void DetachSocialFeedback()
    {
        _world.RelationshipChanged -= ShowRelationshipChange;
        _world.FactionStandingChanged -= ShowFactionChange;
    }

    private void ShowRelationshipChange(string observerId, string targetId, RelationshipAdjustment change)
    {
        if (observerId == _world.PlayerEntityId) return;
        var changes = new[] { ("Familiarity", change.FamiliarityDelta), ("Trust", change.TrustDelta),
            ("Fear", change.FearDelta), ("Loyalty", change.LoyaltyDelta) };
        ShowSocialPopup(observerId, string.Join("\n", changes.Where(pair => pair.Item2 != 0)
            .Select(pair => $"{pair.Item1} {pair.Item2.ToString("+0.##;-0.##", System.Globalization.CultureInfo.InvariantCulture)}")));
    }

    private void ShowFactionChange(string entityId, string factionId, double change)
        => ShowSocialPopup(entityId, $"{factionId} standing {change.ToString("+0.##;-0.##", System.Globalization.CultureInfo.InvariantCulture)}");

    private void ShowSocialPopup(string entityId, string text)
    {
        if (text.Length == 0 || !GameplayActionsAllowed) return;
        var label = new Label3D { Name = "SocialChange", Text = text, Visible = false,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 32, PixelSize = 0.007f,
            Modulate = Colors.White };
        AddChild(label);
        _socialPopups.Add(new(entityId, label));
        UpdateSocialPopups(0);
        UpdateWorldSelection();
    }

    private void UpdateSocialPopups(double delta)
    {
        if (_socialPopups.Count == 0) return;
        var targets = WorldTargets();
        var camera = GetViewport().GetCamera3D();
        foreach (var popup in _socialPopups.ToArray())
        {
            popup.Age += delta;
            if (popup.Age >= 3 || !_world.TryGetEntity(popup.EntityId, out _))
            {
                popup.Label.QueueFree(); _socialPopups.Remove(popup); continue;
            }
            var view = targets.FirstOrDefault(target => target.Target == new WorldTarget(TargetKind.Entity, popup.EntityId));
            var player = popup.EntityId == _world.PlayerEntityId;
            popup.Label.Visible = GameplayActionsAllowed && camera is not null
                && (player || view is not null && TargetVisible(view, camera, targets));
            var position = player ? _playerCharacter.GlobalPosition + Vector3.Up * 2.5f
                : view is null ? Vector3.Zero : new Vector3(view.Center.X, view.Bounds.End.Y + 1, view.Center.Z);
            // Successive events rise separately rather than overwriting each other or the action acknowledgment.
            var stack = _socialPopups.TakeWhile(item => item != popup).Count(item => item.EntityId == popup.EntityId);
            popup.Label.GlobalPosition = position + Vector3.Up * ((float)popup.Age * 0.5f + stack * 1.2f);
            popup.Label.Modulate = new Color(1, 1, 1, (float)Math.Min(1, 3 - popup.Age));
        }
    }

    private bool GameplayActionsAllowed => _sessionActive && _menuState.ActiveMenu is null && !WorkstationPanelOpen && !FarmPanelOpen
        && !ConstructionPanelOpen && !PlacementPanelOpen && !_clock.IsPaused;

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
        foreach (var popup in _socialPopups) popup.Label.QueueFree();
        _socialPopups.Clear();
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
        UpdateSocialPopups(delta);
        _feedbackSeconds = Math.Max(0, _feedbackSeconds - delta);
        _actionHud.Visible = _sessionActive && _menuState.ActiveMenu is null;
        var collect = PlayerButtonBindings.Label(_buttonBindings.Get(ControllerActions.Collect).Desktop);
        var interact = PlayerButtonBindings.Label(_buttonBindings.Get(ControllerActions.Interact).Desktop);
        var greet = PlayerButtonBindings.Label(_buttonBindings.Get(ControllerActions.Greet).Desktop);
        var hint = $"Tap {collect}: collect · Hold {collect} 0.6s: use carried food · {interact}: interact/feed/pet · {greet}: greet (2 m)";
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

        if (_selectedTarget.Kind == TargetKind.Workstation)
        {
            OpenWorkstation(_selectedTarget.Id);
            return;
        }

        if (_selectedTarget.Kind == TargetKind.Farm)
        {
            OpenFarm(_selectedTarget.Id);
            return;
        }

        if (_selectedTarget.Kind == TargetKind.ConstructionSite)
        {
            OpenConstructionSite(_selectedTarget.Id);
            return;
        }

        // 1. Structures -> Toggle Door
        var structure = _selectedTarget.Kind == TargetKind.Structure
            ? _world.Structures.FirstOrDefault(s => s.Id == _selectedTarget.Id) : null;
        if (structure != null)
        {
            var result = _interactionResolver.Resolve(new InteractionRequest(
                playerId,
                _selectedTarget.Id,
                InteractionType.Physical,
                "toggle_door"));
            ShowActionFeedback(result.Reason);
            SyncConstructionPresentation();
            UpdateWorldSelection();
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

            if (foodItem.Key != null && target.Satiety < 100)
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
