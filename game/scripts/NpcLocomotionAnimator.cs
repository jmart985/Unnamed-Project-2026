using Godot;

namespace UnnamedProject2026;

/// <summary>Chooses in-place locomotion clips for a rendered NPC from simulation movement speed.</summary>
internal sealed class NpcLocomotionAnimator
{
    private readonly List<(GodotObject Target, NodePath PropertyPath, Variant Value)> _manualBlendShapeBaseline = [];
    private readonly Dictionary<string, string> _animationNames = new(StringComparer.OrdinalIgnoreCase);
    private AnimationPlayer? _player;
    private string _speciesId = string.Empty;
    private string? _currentAnimation;
    private string? _manualAnimation;
    private bool _manualAnimationLoops;
    private float _movementSpeed;
    private float _lastMovementSpeed;
    private bool _isCreature;

    public bool HoldsPositionForManualAction => _manualAnimation is not null && !_manualAnimationLoops;

    public void Bind(AnimationPlayer? player, string speciesId, bool isCreature, float movementSpeed)
    {
        RestoreManualBlendShapeBaseline();
        _player = player;
        _speciesId = speciesId;
        _isCreature = isCreature;
        _movementSpeed = Mathf.Max(movementSpeed, 0.5f);
        _currentAnimation = null;
        _manualAnimation = null;
        _manualAnimationLoops = false;
        _animationNames.Clear();
        if (player is null) return;

        player.AnimationFinished += OnAnimationFinished;

        foreach (var animationName in player.GetAnimationList())
        {
            var name = animationName.ToString();
            _animationNames[name] = name;
        }

        Update(0);
    }

    public void Update(float movementSpeed)
    {
        var player = _player;
        if (player is null || _animationNames.Count == 0 || !float.IsFinite(movementSpeed)) return;
        _lastMovementSpeed = Mathf.Max(0, movementSpeed);
        if (_manualAnimation is not null)
        {
            if (!HasManualAnimationCompleted(player)) return;
            CompleteManualAnimation();
        }

        var speed = Mathf.Max(0, movementSpeed);
        var idle = Find("human_idle", "idle");
        var speedFraction = speed / _movementSpeed;
        string? selected;
        if (speed <= 0.12f)
        {
            selected = idle;
        }
        else
        {
            var walk = _isCreature && _speciesId.Equals("ape", StringComparison.OrdinalIgnoreCase)
                ? Find("knuckle_walk", "walk")
                : Find("human_walk", "walk");
            var trot = _isCreature ? Find("trot") : null;
            var run = Find("human_run", "run");

            // Creature rigs use the gait names their anatomy supports. Apes use
            // the authored knuckle walk; ungulates without a run clip top out at trot.
            if (_isCreature && speedFraction >= 0.78f && run is not null)
                selected = run;
            else if (_isCreature && speedFraction >= 0.40f && trot is not null)
                selected = trot;
            else if (!_isCreature && speedFraction >= 0.84f && run is not null)
                selected = run;
            else
                selected = walk ?? trot ?? run ?? idle;
        }

        if (selected is null) return;
        var referenceSpeed = selected.Contains("run", StringComparison.OrdinalIgnoreCase)
            ? _movementSpeed
            : selected.Equals("trot", StringComparison.OrdinalIgnoreCase)
                ? _movementSpeed * 0.68f
                : _movementSpeed * 0.45f;
        player.SpeedScale = selected == idle
            ? 1f
            : Mathf.Clamp(speed / Mathf.Max(referenceSpeed, 0.5f), 0.7f, 1.5f);

        if (selected == _currentAnimation) return;
        player.GetAnimation(selected).LoopMode = Animation.LoopModeEnum.Linear;
        player.Play(selected, customBlend: 0.18f);
        _currentAnimation = selected;
    }

    public IReadOnlyList<string> GetAnimationNames() => _animationNames.Keys
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();

    public bool PlayManual(string animationName, bool loop)
    {
        var player = _player;
        if (player is null || !_animationNames.TryGetValue(animationName, out var actualName))
            return false;

        if (_manualAnimation is not null)
        {
            player.Stop(keepState: true);
            RestoreManualBlendShapeBaseline();
        }

        var animation = player.GetAnimation(actualName);
        CaptureManualBlendShapeBaseline(player, animation);
        animation.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        player.Play(actualName, customBlend: 0.18f);
        _manualAnimation = actualName;
        _manualAnimationLoops = loop;
        _currentAnimation = actualName;
        return true;
    }

    public void StopManual(float movementSpeed)
    {
        if (_manualAnimation is null) return;
        _player?.Stop(keepState: true);
        RestoreManualBlendShapeBaseline();
        _manualAnimation = null;
        _manualAnimationLoops = false;
        _currentAnimation = null;
        Update(movementSpeed);
    }

    private void OnAnimationFinished(StringName animationName)
    {
        if (_manualAnimation is null || !string.Equals(_manualAnimation, animationName.ToString(), StringComparison.Ordinal))
            return;

        CompleteManualAnimation();
    }

    private bool HasManualAnimationCompleted(AnimationPlayer player)
    {
        var manualAnimation = _manualAnimation;
        if (manualAnimation is null) return true;
        if (!player.IsPlaying() || !string.Equals(player.CurrentAnimation.ToString(), manualAnimation, StringComparison.Ordinal))
            return true;
        if (_manualAnimationLoops || !_animationNames.TryGetValue(manualAnimation, out var actualName)) return false;

        // Some imported clips can remain marked as playing at their last sampled frame
        // without raising AnimationFinished immediately. Do not let that hold an NPC
        // in place forever after a finite Test Lab action has reached its end.
        var animation = player.GetAnimation(actualName);
        return player.CurrentAnimationPosition >= animation.Length - 0.001;
    }

    private void CompleteManualAnimation()
    {
        RestoreManualBlendShapeBaseline();
        _manualAnimation = null;
        _manualAnimationLoops = false;
        _currentAnimation = null;
        Update(_lastMovementSpeed);
    }

    private void CaptureManualBlendShapeBaseline(AnimationPlayer player, Animation animation)
    {
        _manualBlendShapeBaseline.Clear();
        var animationRoot = player.GetNodeOrNull(player.RootNode);
        if (animationRoot is null) return;

        for (var trackIndex = 0; trackIndex < animation.GetTrackCount(); trackIndex++)
        {
            if (animation.TrackGetType(trackIndex) != Animation.TrackType.Value) continue;

            var trackPath = animation.TrackGetPath(trackIndex).ToString();
            var separatorIndex = trackPath.IndexOf(':');
            if (separatorIndex < 0) continue;

            var propertyPath = new NodePath(trackPath[(separatorIndex + 1)..]);
            if (!propertyPath.ToString().StartsWith("blend_shapes/", StringComparison.Ordinal)) continue;

            var targetPath = new NodePath(trackPath[..separatorIndex]);
            var target = animationRoot.GetNodeOrNull(targetPath);
            if (target is null) continue;
            if (_manualBlendShapeBaseline.Any(entry => ReferenceEquals(entry.Target, target)
                && entry.PropertyPath == propertyPath)) continue;

            _manualBlendShapeBaseline.Add((target, propertyPath, target.GetIndexed(propertyPath)));
        }
    }

    private void RestoreManualBlendShapeBaseline()
    {
        foreach (var (target, propertyPath, value) in _manualBlendShapeBaseline)
            target.SetIndexed(propertyPath, value);
        _manualBlendShapeBaseline.Clear();
    }

    private string? Find(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (_animationNames.TryGetValue(candidate, out var actualName)) return actualName;
        }
        return null;
    }
}
