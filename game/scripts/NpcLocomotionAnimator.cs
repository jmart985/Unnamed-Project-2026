using Godot;

namespace UnnamedProject2026;

/// <summary>Chooses in-place locomotion clips for a rendered NPC from simulation movement speed.</summary>
internal sealed class NpcLocomotionAnimator
{
    private readonly Dictionary<string, string> _animationNames = new(StringComparer.OrdinalIgnoreCase);
    private AnimationPlayer? _player;
    private string _locomotionFamily = string.Empty;
    private string? _currentAnimation;
    private float _movementSpeed;
    private bool _isCreature;

    public void Bind(AnimationPlayer? player, string locomotionFamily, bool isCreature, float movementSpeed)
    {
        _player = player;
        _locomotionFamily = locomotionFamily;
        _isCreature = isCreature;
        _movementSpeed = Mathf.Max(movementSpeed, 0.5f);
        _currentAnimation = null;
        _animationNames.Clear();
        if (player is null) return;

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
            var walk = _isCreature && _locomotionFamily.Equals("primate", StringComparison.OrdinalIgnoreCase)
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

    private string? Find(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (_animationNames.TryGetValue(candidate, out var actualName)) return actualName;
        }
        return null;
    }
}
