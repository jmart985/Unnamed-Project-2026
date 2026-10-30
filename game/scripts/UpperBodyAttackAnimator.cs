using Godot;

namespace UnnamedProject2026;

/// <summary>Plays imported attacks over locomotion without allowing attack leg tracks to stop the gait.</summary>
internal sealed class UpperBodyAttackAnimator
{
    private readonly AnimationPlayer _player;
    private readonly Skeleton3D _skeleton;
    private double _remainingSeconds;

    private UpperBodyAttackAnimator(AnimationPlayer player, Skeleton3D skeleton)
    {
        _player = player;
        _skeleton = skeleton;
    }

    public string? CurrentAnimation => _remainingSeconds > 0 && _player.IsPlaying()
        ? _player.CurrentAnimation.ToString()
        : null;

    public static UpperBodyAttackAnimator? Create(AnimationPlayer locomotionPlayer, Skeleton3D skeleton)
    {
        var spineIndex = skeleton.FindBone("spine_01");
        var parent = locomotionPlayer.GetParent();
        if (spineIndex < 0 || parent is null) return null;

        var attackLibrary = new AnimationLibrary();
        foreach (var animationName in new[] { "human_attack_l", "human_attack_r" })
        {
            if (!locomotionPlayer.HasAnimation(animationName)) continue;
            var source = locomotionPlayer.GetAnimation(animationName);
            if (source.Duplicate(true) is not Animation filteredAttack)
                throw new InvalidOperationException($"Could not copy imported attack clip '{animationName}'.");

            var retainedUpperBodyTracks = 0;
            for (var trackIndex = filteredAttack.GetTrackCount() - 1; trackIndex >= 0; trackIndex--)
            {
                if (!IsUpperBodyBoneTrack(filteredAttack, trackIndex, skeleton, spineIndex))
                {
                    // Non-skeletal tracks are retained for future face/prop effects. Skeletal
                    // tracks are kept only on the spine branch, excluding pelvis and both legs.
                    if (IsSkeletonTransformTrack(filteredAttack, trackIndex))
                        filteredAttack.RemoveTrack(trackIndex);
                    continue;
                }

                retainedUpperBodyTracks++;
            }

            if (retainedUpperBodyTracks == 0) continue;
            filteredAttack.LoopMode = Animation.LoopModeEnum.None;
            attackLibrary.AddAnimation(animationName, filteredAttack);
        }

        if (attackLibrary.GetAnimationList().Count == 0) return null;

        var attackPlayer = new AnimationPlayer
        {
            Name = "PlayerUpperBodyAttackPlayer",
            RootNode = locomotionPlayer.RootNode,
            CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual
        };
        parent.AddChild(attackPlayer);
        attackPlayer.AddAnimationLibrary("", attackLibrary);
        return new UpperBodyAttackAnimator(attackPlayer, skeleton);
    }

    public bool Play(string animationName, float durationSeconds)
    {
        if (!float.IsFinite(durationSeconds) || durationSeconds <= 0 || !_player.HasAnimation(animationName))
            return false;

        var animation = _player.GetAnimation(animationName);
        _player.SpeedScale = (float)animation.Length / durationSeconds;
        _player.Play(animationName, customBlend: 0.05f);
        _remainingSeconds = durationSeconds;
        return true;
    }

    public void Advance(double deltaSeconds)
    {
        if (_remainingSeconds <= 0 || !double.IsFinite(deltaSeconds) || deltaSeconds <= 0) return;

        _player.Advance(deltaSeconds);
        _remainingSeconds = Math.Max(0, _remainingSeconds - deltaSeconds);
        if (_remainingSeconds == 0)
            _player.Stop(keepState: true);
    }

    internal bool HasBoneTrackForTesting(string animationName, string boneName)
    {
        if (!_player.HasAnimation(animationName)) return false;
        var animation = _player.GetAnimation(animationName);
        for (var trackIndex = 0; trackIndex < animation.GetTrackCount(); trackIndex++)
        {
            if (!IsSkeletonTransformTrack(animation, trackIndex)) continue;
            var path = animation.TrackGetPath(trackIndex);
            if (path.GetSubNameCount() > 0
                && _skeleton.FindBone(path.GetSubName(path.GetSubNameCount() - 1)) >= 0
                && path.GetSubName(path.GetSubNameCount() - 1).ToString() == boneName)
                return true;
        }

        return false;
    }

    private static bool IsUpperBodyBoneTrack(Animation animation, int trackIndex, Skeleton3D skeleton, int spineIndex)
    {
        if (!IsSkeletonTransformTrack(animation, trackIndex)) return false;
        var path = animation.TrackGetPath(trackIndex);
        if (path.GetSubNameCount() == 0) return false;

        var boneIndex = skeleton.FindBone(path.GetSubName(path.GetSubNameCount() - 1));
        for (var currentIndex = boneIndex; currentIndex >= 0; currentIndex = skeleton.GetBoneParent(currentIndex))
        {
            if (currentIndex == spineIndex) return true;
        }

        return false;
    }

    private static bool IsSkeletonTransformTrack(Animation animation, int trackIndex)
        => animation.TrackGetType(trackIndex) is Animation.TrackType.Position3D
            or Animation.TrackType.Rotation3D
            or Animation.TrackType.Scale3D;
}
