using Godot;

namespace UnnamedProject2026;

/// <summary>Chooses in-place locomotion clips for a rendered NPC from simulation movement speed.</summary>
internal sealed class NpcLocomotionAnimator
{
    private readonly List<(MeshInstance3D Mesh, int BlendShapeIndex, float Value)> _manualBlendShapeBaseline = [];
    private readonly Dictionary<string, string> _animationNames = new(StringComparer.OrdinalIgnoreCase);
    private AnimationPlayer? _player;
    private string _speciesId = string.Empty;
    private string? _currentAnimation;
    private string? _manualAnimation;
    private bool _manualAnimationLoops;
    private double _manualAnimationElapsedSeconds;
    private double _manualAnimationDurationSeconds;
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
        _manualAnimationElapsedSeconds = 0;
        _manualAnimationDurationSeconds = 0;
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

    public void Update(float movementSpeed, double deltaSeconds = 0)
    {
        var player = _player;
        if (player is null || _animationNames.Count == 0 || !float.IsFinite(movementSpeed)) return;
        _lastMovementSpeed = Mathf.Max(0, movementSpeed);
        if (_manualAnimation is not null)
        {
            if (double.IsFinite(deltaSeconds) && deltaSeconds > 0)
                _manualAnimationElapsedSeconds += deltaSeconds;
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
        player.SpeedScale = 1f;
        player.Play(actualName, customBlend: 0.18f);
        _manualAnimation = actualName;
        _manualAnimationLoops = loop;
        _manualAnimationElapsedSeconds = 0;
        _manualAnimationDurationSeconds = loop ? double.PositiveInfinity : Math.Max(0, animation.Length);
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
        _manualAnimationElapsedSeconds = 0;
        _manualAnimationDurationSeconds = 0;
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

        // Imported clips can fail to raise AnimationFinished on some runtimes. Track
        // their finite playback duration as a fallback so the NPC cannot stay held.
        var animation = player.GetAnimation(actualName);
        return player.CurrentAnimationPosition >= animation.Length - 0.001
            || _manualAnimationElapsedSeconds >= _manualAnimationDurationSeconds;
    }

    private void CompleteManualAnimation()
    {
        RestoreManualBlendShapeBaseline();
        _manualAnimation = null;
        _manualAnimationLoops = false;
        _manualAnimationElapsedSeconds = 0;
        _manualAnimationDurationSeconds = 0;
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
            var trackType = animation.TrackGetType(trackIndex);
            if (trackType is not (Animation.TrackType.Value or Animation.TrackType.BlendShape)) continue;

            var trackPath = animation.TrackGetPath(trackIndex).ToString();
            var separatorIndex = trackPath.IndexOf(':');
            if (separatorIndex < 0) continue;

            var propertyName = trackPath[(separatorIndex + 1)..];
            const string blendShapePrefix = "blend_shapes/";
            if (trackType == Animation.TrackType.Value)
            {
                if (!propertyName.StartsWith(blendShapePrefix, StringComparison.Ordinal)) continue;
                propertyName = propertyName[blendShapePrefix.Length..];
            }

            if (animationRoot.GetNodeOrNull(new NodePath(trackPath[..separatorIndex])) is not MeshInstance3D mesh
                || mesh.Mesh is not ArrayMesh arrayMesh) continue;
            var blendShapeIndex = FindBlendShapeIndex(arrayMesh, propertyName);
            if (blendShapeIndex < 0 || _manualBlendShapeBaseline.Any(entry => ReferenceEquals(entry.Mesh, mesh)
                && entry.BlendShapeIndex == blendShapeIndex)) continue;

            _manualBlendShapeBaseline.Add((mesh, blendShapeIndex, mesh.GetBlendShapeValue(blendShapeIndex)));
        }
    }

    private void RestoreManualBlendShapeBaseline()
    {
        foreach (var (mesh, blendShapeIndex, value) in _manualBlendShapeBaseline)
            mesh.SetBlendShapeValue(blendShapeIndex, value);
        _manualBlendShapeBaseline.Clear();
    }

    private static int FindBlendShapeIndex(ArrayMesh mesh, string blendShapeName)
    {
        for (var index = 0; index < mesh.GetBlendShapeCount(); index++)
        {
            if (mesh.GetBlendShapeName(index).ToString() == blendShapeName) return index;
        }

        return -1;
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
