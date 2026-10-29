using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private void RunNpcLocomotionValidation() => NpcLocomotionValidation.Run(this);
}

internal static class NpcLocomotionValidation
{
    private sealed record ModelCase(string Path, string SpeciesId, bool IsCreature, float Speed,
        string WalkClip, string MidSpeedClip, string FastClip,
        string EntityId = "locomotion-validation", BiologicalSex Sex = BiologicalSex.Unspecified,
        string? ExpectedEntityViewPath = null);

    public static async void Run(Main main)
    {
        Node3D? holder = null;
        try
        {
            var cases = new List<ModelCase>
            {
                new ModelCase("res://assets/humanoids/human/human_humanoid_rig_v1.glb", "human", false, 3f,
                    "human_walk", "human_walk", "human_run", Sex: BiologicalSex.Female),
                new ModelCase("res://assets/humanoids/human/radagon_male_humanoid_rig_v1.glb", "human", false, 3f,
                    "human_walk", "human_walk", "human_run", Sex: BiologicalSex.Male),
                new ModelCase("res://assets/creatures/ape/ape_primate_rig_v1.glb", "ape", true, 2f,
                    "knuckle_walk", "knuckle_walk", "run"),
                new ModelCase("res://assets/creatures/cat/cat_felid_rig_v1.glb", "cat", true, 2.2f,
                    "walk", "trot", "run"),
                new ModelCase("res://assets/creatures/cow/cow_ungulate_rig_v1.glb", "cow", true, 2.4f,
                    "walk", "trot", "trot"),
                new ModelCase("res://assets/creatures/fox/fox_vulpine_rig_v1.glb", "fox", true, 1.8f,
                    "walk", "trot", "run"),
                new ModelCase("res://assets/creatures/pig/pig_suiform_rig_v1.glb", "pig", true, 2.1f,
                    "walk", "trot", "run"),
                new ModelCase("res://assets/creatures/wolf/wolf_canid_rig_v1.glb", "wolf", true, 3.4f,
                    "walk", "trot", "run"),
            };
            foreach (var presentation in StarterNpcPresentationCatalog.All)
            {
                cases.Add(new ModelCase(presentation.AssetPath, "human", false, 3f,
                    "human_walk", "human_walk", "human_run", presentation.EntityId,
                    presentation.Sex, presentation.AssetPath));
            }

            holder = new Node3D { Name = "NpcLocomotionValidationModels" };
            main.AddChild(holder);
            var facialBlendShapeResetValidated = false;
            var skeletalPoseResetValidated = false;
            var facialBlendShapeDiagnostics = new List<string>();
            foreach (var modelCase in cases)
            {
                var scene = GD.Load<PackedScene>(modelCase.Path)
                    ?? throw new InvalidOperationException($"NPC model failed to load: {modelCase.Path}");
                var model = scene.Instantiate<Node3D>();
                holder.AddChild(model);
                var player = model.FindChild("AnimationPlayer", recursive: true, owned: false) as AnimationPlayer
                    ?? throw new InvalidOperationException($"AnimationPlayer missing from {modelCase.Path}");
                var animator = new NpcLocomotionAnimator();
                animator.Bind(player, modelCase.SpeciesId, modelCase.IsCreature, modelCase.Speed);
                var idleClip = modelCase.IsCreature ? "idle" : "human_idle";
                RequireClip(player, idleClip, modelCase.Path);
                RequireClip(player, modelCase.WalkClip, modelCase.Path, modelCase.Speed * 0.2f, animator);
                RequireClip(player, modelCase.MidSpeedClip, modelCase.Path, modelCase.Speed * 0.55f, animator);
                RequireClip(player, modelCase.FastClip, modelCase.Path, modelCase.Speed * 0.9f, animator);
                RequireClip(player, idleClip, modelCase.Path, 0, animator);
                var facialResetPassed = ValidateFacialBlendShapeReset(player, animator, modelCase);
                facialBlendShapeResetValidated |= facialResetPassed;
                if (!facialResetPassed)
                    facialBlendShapeDiagnostics.Add(DescribeFacialBlendShapeCandidates(player, animator, modelCase));
                await ValidateManualPlayback(player, animator, modelCase);
                skeletalPoseResetValidated |= await ValidateManualSkeletalPoseReset(player, animator, modelCase);
                ValidateTickMovement(holder, modelCase);
            }

            if (!facialBlendShapeResetValidated)
                throw new InvalidOperationException("NPC locomotion validation found no imported facial blend-shape clip to verify reset behavior. "
                    + string.Join(" | ", facialBlendShapeDiagnostics));
            if (!skeletalPoseResetValidated)
                throw new InvalidOperationException("NPC locomotion validation found no humanoid finger rig with a manual clip to verify skeletal-pose restoration.");

            await ValidateTestPanel(holder);
            main.ValidateNpcAnimationTestClosesGameplayOverlays();
            main.ValidateNpcAnimationTestRouteFailureRecovery();
            main.ValidateNpcAnimationTestRouteResumesStayOrder();
            main.ValidateNpcAnimationTestRouteResumesSquadOrder();

            holder.QueueFree();
            await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
            await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("NPC LOCOMOTION VALIDATION PASSED");
            main.GetTree().Quit();
        }
        catch (Exception exception)
        {
            holder?.QueueFree();
            await main.ToSignal(main.GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.PushError($"NPC LOCOMOTION VALIDATION FAILED: {exception}");
            main.GetTree().Quit(1);
        }
    }

    private static async Task ValidateManualPlayback(AnimationPlayer player, NpcLocomotionAnimator animator,
        ModelCase modelCase)
    {
        if (!animator.PlayManual(modelCase.WalkClip, loop: true))
            throw new InvalidOperationException($"Test panel could not start {modelCase.WalkClip} on {modelCase.Path}.");

        animator.Update(modelCase.Speed * 0.95f);
        RequireClip(player, modelCase.WalkClip, modelCase.Path);

        animator.StopManual(modelCase.Speed * 0.95f);
        RequireClip(player, modelCase.FastClip, modelCase.Path);

        if (!animator.PlayManual(modelCase.WalkClip, loop: false))
            throw new InvalidOperationException($"Test panel could not start one-shot playback on {modelCase.Path}.");
        if (!animator.HoldsPositionForManualAction)
            throw new InvalidOperationException($"Finite Test Lab clip did not request a movement hold on {modelCase.Path}.");
        var clipDuration = player.GetAnimation(modelCase.WalkClip).Length;
        player.Advance(clipDuration + 0.05);
        await player.ToSignal(player.GetTree(), SceneTree.SignalName.ProcessFrame);
        // Advance the fallback clock too, so the test covers runtimes where an imported
        // clip remains marked as playing instead of reporting completion.
        animator.Update(modelCase.Speed * 0.95f, clipDuration + 0.05);
        if (animator.HoldsPositionForManualAction)
            throw new InvalidOperationException($"Completed Test Lab clip kept movement held on {modelCase.Path}.");
        RequireClip(player, modelCase.FastClip, modelCase.Path);
    }

    private sealed record FingerPose(Skeleton3D Skeleton, int BoneIndex, string BoneName,
        Vector3 Position, Quaternion Rotation, Vector3 Scale);

    private static async Task<bool> ValidateManualSkeletalPoseReset(AnimationPlayer player,
        NpcLocomotionAnimator animator, ModelCase modelCase)
    {
        var root = player.GetNodeOrNull(player.RootNode);
        if (root is null) return false;

        var fingerPoses = CaptureFingerPoses(root);
        if (fingerPoses.Count == 0) return false;

        var locomotionNames = new HashSet<string>(
            [modelCase.WalkClip, modelCase.MidSpeedClip, modelCase.FastClip,
                modelCase.IsCreature ? "idle" : "human_idle"],
            StringComparer.OrdinalIgnoreCase);
        var clipName = animator.GetAnimationNames().FirstOrDefault(name =>
            !locomotionNames.Contains(name)
            && !name.Contains("npc_face_", StringComparison.OrdinalIgnoreCase)
            && player.GetAnimation(name).Length > 0);
        if (clipName is null) return false;

        var testPoses = fingerPoses.ToArray();
        if (!animator.PlayManual(clipName, loop: true))
            throw new InvalidOperationException($"Could not start manual clip {clipName} on {modelCase.Path}.");
        ApplyTemporaryFingerPose(testPoses);
        animator.StopManual(modelCase.Speed * 0.95f);
        RequireFingerPosesRestored(testPoses, clipName, modelCase.Path, "stop");

        if (!animator.PlayManual(clipName, loop: true))
            throw new InvalidOperationException($"Could not restart manual clip {clipName} on {modelCase.Path}.");
        ApplyTemporaryFingerPose(testPoses);
        if (!animator.PlayManual(modelCase.IsCreature ? "idle" : "human_idle", loop: true))
            throw new InvalidOperationException($"Could not switch to idle while testing {clipName} on {modelCase.Path}.");
        RequireFingerPosesRestored(testPoses, clipName, modelCase.Path, "clip switch");
        animator.StopManual(modelCase.Speed * 0.95f);

        var completionBaseline = CaptureFingerPoses(root);
        if (!animator.PlayManual(clipName, loop: false))
            throw new InvalidOperationException($"Could not start one-shot clip {clipName} on {modelCase.Path}.");
        ApplyTemporaryFingerPose(completionBaseline);
        var duration = player.GetAnimation(clipName).Length;
        animator.Update(modelCase.Speed * 0.95f, duration + 0.05);
        await player.ToSignal(player.GetTree(), SceneTree.SignalName.ProcessFrame);
        RequireFingerPosesRestored(completionBaseline, clipName, modelCase.Path, "natural completion");
        return true;
    }

    private static List<FingerPose> CaptureFingerPoses(Node root)
    {
        var skeletons = root.FindChildren("*", "Skeleton3D", recursive: true, owned: false)
            .OfType<Skeleton3D>();
        if (root is Skeleton3D rootSkeleton)
            skeletons = skeletons.Prepend(rootSkeleton);

        return skeletons.Distinct()
            .SelectMany(skeleton => Enumerable.Range(0, skeleton.GetBoneCount())
                .Where(index => IsFingerBone(skeleton.GetBoneName(index).ToString()))
                .Select(index => new FingerPose(
                    skeleton, index, skeleton.GetBoneName(index).ToString(),
                    skeleton.GetBonePosePosition(index), skeleton.GetBonePoseRotation(index),
                    skeleton.GetBonePoseScale(index))))
            .ToList();
    }

    private static bool IsFingerBone(string name)
    {
        if (name.Contains("_ik", StringComparison.OrdinalIgnoreCase)
            || name.Contains("pole", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var marker in new[] { "thumb", "index", "middle", "ring", "pinky", "finger" })
        {
            var markerIndex = name.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0) continue;
            var suffix = name[(markerIndex + marker.Length)..];
            if (suffix.Length <= 4 && suffix.Any(char.IsDigit)) return true;
        }

        return false;
    }

    private static void ApplyTemporaryFingerPose(IEnumerable<FingerPose> poses)
    {
        var changed = 0;
        foreach (var pose in poses)
        {
            if (!GodotObject.IsInstanceValid(pose.Skeleton) || pose.BoneIndex >= pose.Skeleton.GetBoneCount()) continue;
            pose.Skeleton.SetBonePoseRotation(pose.BoneIndex,
                pose.Rotation * Quaternion.FromEuler(new Vector3(0.21f, -0.13f, 0.17f)));
            changed++;
        }

        if (changed == 0) throw new InvalidOperationException("No finger poses were available for restoration validation.");
    }

    private static void RequireFingerPosesRestored(IEnumerable<FingerPose> expectedPoses,
        string clipName, string modelPath, string transition)
    {
        foreach (var pose in expectedPoses)
        {
            if (!GodotObject.IsInstanceValid(pose.Skeleton)
                || pose.BoneIndex >= pose.Skeleton.GetBoneCount())
                throw new InvalidOperationException($"Skeleton disappeared while validating {clipName} on {modelPath}.");

            var restored = pose.Skeleton.GetBonePosePosition(pose.BoneIndex).DistanceTo(pose.Position) <= 0.001f
                && pose.Skeleton.GetBonePoseRotation(pose.BoneIndex).AngleTo(pose.Rotation) <= 0.01f
                && pose.Skeleton.GetBonePoseScale(pose.BoneIndex).DistanceTo(pose.Scale) <= 0.001f;
            if (!restored)
                throw new InvalidOperationException(
                    $"Finger bone {pose.BoneName} remained animated after {transition} from {clipName} on {modelPath}.");
        }
    }

    private static bool ValidateFacialBlendShapeReset(AnimationPlayer player, NpcLocomotionAnimator animator,
        ModelCase modelCase)
    {
        var root = player.GetNodeOrNull(player.RootNode);
        if (root is null) return false;

        foreach (var clipName in animator.GetAnimationNames()
            .Where(name => name.Contains("npc_face_", StringComparison.OrdinalIgnoreCase)))
        {
            var animation = player.GetAnimation(clipName);
            var baselines = new List<(MeshInstance3D Mesh, int BlendShapeIndex, float Weight)>();
            var animatedProperties = new List<(MeshInstance3D Mesh, int BlendShapeIndex, int TrackIndex)>();
            for (var trackIndex = 0; trackIndex < animation.GetTrackCount(); trackIndex++)
            {
                var trackType = animation.TrackGetType(trackIndex);
                if (trackType is not (Animation.TrackType.Value or Animation.TrackType.BlendShape)) continue;
                var trackPath = animation.TrackGetPath(trackIndex).ToString();
                var separatorIndex = trackPath.IndexOf(':');
                if (separatorIndex < 0) continue;

                var shapeName = trackPath[(separatorIndex + 1)..];
                const string blendShapePrefix = "blend_shapes/";
                if (trackType == Animation.TrackType.Value)
                {
                    if (!shapeName.StartsWith(blendShapePrefix, StringComparison.Ordinal)) continue;
                    shapeName = shapeName[blendShapePrefix.Length..];
                }
                else if (shapeName.StartsWith(blendShapePrefix, StringComparison.Ordinal))
                {
                    shapeName = shapeName[blendShapePrefix.Length..];
                }

                var targetPath = new NodePath(trackPath[..separatorIndex]);
                if (root.GetNodeOrNull(targetPath) is not MeshInstance3D mesh || mesh.Mesh is not ArrayMesh arrayMesh)
                    continue;
                var blendShapeIndex = FindBlendShapeIndex(arrayMesh, shapeName);
                if (blendShapeIndex < 0) continue;

                if (baselines.All(entry => !ReferenceEquals(entry.Mesh, mesh) || entry.BlendShapeIndex != blendShapeIndex))
                    baselines.Add((mesh, blendShapeIndex, mesh.GetBlendShapeValue(blendShapeIndex)));
                animatedProperties.Add((mesh, blendShapeIndex, trackIndex));
            }

            if (baselines.Count == 0 || !animator.PlayManual(clipName, loop: true)) continue;

            // Let the manual clip finish blending over the current locomotion clip before
            // sampling its keys. A zero-time Seek can otherwise observe only the blend's
            // initial frame on fast headless runners.
            player.Advance(0.25);

            var expressionApplied = false;
            foreach (var (mesh, blendShapeIndex, trackIndex) in animatedProperties)
            {
                var baseline = baselines.Single(entry => ReferenceEquals(entry.Mesh, mesh)
                    && entry.BlendShapeIndex == blendShapeIndex).Weight;
                for (var keyIndex = 0; keyIndex < animation.TrackGetKeyCount(trackIndex); keyIndex++)
                {
                    player.Seek(animation.TrackGetKeyTime(trackIndex, keyIndex), update: true);
                    if (Mathf.Abs(mesh.GetBlendShapeValue(blendShapeIndex) - baseline) <= 0.001f) continue;
                    expressionApplied = true;
                    break;
                }
                if (expressionApplied) break;
            }

            animator.StopManual(modelCase.Speed * 0.95f);
            if (!expressionApplied) continue;

            foreach (var (mesh, blendShapeIndex, baseline) in baselines)
            {
                if (Mathf.Abs(mesh.GetBlendShapeValue(blendShapeIndex) - baseline) > 0.001f)
                    throw new InvalidOperationException(
                        $"Stopping facial clip {clipName} left blend-shape {((ArrayMesh)mesh.Mesh!).GetBlendShapeName(blendShapeIndex)} changed on {modelCase.Path}.");
            }
            return true;
        }

        return false;
    }

    private static string DescribeFacialBlendShapeCandidates(AnimationPlayer player,
        NpcLocomotionAnimator animator, ModelCase modelCase)
    {
        var root = player.GetNodeOrNull(player.RootNode);
        var details = new List<string>();
        foreach (var clipName in animator.GetAnimationNames()
            .Where(name => name.Contains("npc_face_", StringComparison.OrdinalIgnoreCase)))
        {
            var animation = player.GetAnimation(clipName);
            var tracks = new List<string>();
            for (var trackIndex = 0; trackIndex < animation.GetTrackCount(); trackIndex++)
            {
                var trackType = animation.TrackGetType(trackIndex);
                if (trackType is not (Animation.TrackType.Value or Animation.TrackType.BlendShape)) continue;
                var path = animation.TrackGetPath(trackIndex).ToString();
                var separatorIndex = path.IndexOf(':');
                var target = separatorIndex >= 0 && root is not null
                    ? root.GetNodeOrNull(new NodePath(path[..separatorIndex]))
                    : null;
                var meshDescription = target is MeshInstance3D mesh
                    ? $"{mesh.Mesh?.GetClass() ?? "no-mesh"}/{(mesh.Mesh is ArrayMesh arrayMesh ? arrayMesh.GetBlendShapeCount() : 0)}-shapes"
                    : target?.GetClass() ?? "missing-target";
                tracks.Add($"{trackType}:{path}=>{meshDescription}");
            }
            details.Add($"{clipName}({animation.GetTrackCount()} tracks: {string.Join(",", tracks)})");
        }

        return $"{modelCase.Path}: animations=[{string.Join(",", animator.GetAnimationNames())}], "
            + $"facial=[{string.Join(";", details)}]";
    }

    private static int FindBlendShapeIndex(ArrayMesh mesh, string blendShapeName)
    {
        for (var index = 0; index < mesh.GetBlendShapeCount(); index++)
        {
            if (mesh.GetBlendShapeName(index).ToString() == blendShapeName) return index;
        }

        return -1;
    }

    private static async Task ValidateTestPanel(Node3D holder)
    {
        var view = new EntityView();
        view.Bind("npc-test-panel-validation", 3f, isCreature: false, "human", sex: BiologicalSex.Female);
        holder.AddChild(view);

        var panel = new NpcAnimationTestPanel();
        holder.AddChild(panel);
        await panel.ToSignal(panel.GetTree(), SceneTree.SignalName.ProcessFrame);
        var actorId = "npc-test-panel-validation";
        panel.Open([new NpcAnimationTestActor(actorId, "Test NPC")], id => id == actorId ? view : null);
        if (!panel.IsOpen || panel.ActorCount != 1 || panel.AnimationCount == 0)
            throw new InvalidOperationException("NPC Test Lab did not populate its actor and imported clip selectors.");

        var buttons = panel.FindChildren("*", "Button", recursive: true, owned: false)
            .OfType<Button>().ToDictionary(button => button.Text.ToString(), StringComparer.Ordinal);
        var requiredButtons = new[]
        {
            "Play clip", "Stop / locomotion", "Walk 4 m", "Run 4 m", "Stop move", "Set target", "Greet",
            "Interact / feed / pet", "Left strike", "Right strike", "Follow player", "Stay here"
        };
        if (requiredButtons.Any(name => !buttons.ContainsKey(name)))
            throw new InvalidOperationException("NPC Test Lab is missing one or more movement/action controls.");

        var player = view.FindChild("AnimationPlayer", recursive: true, owned: false) as AnimationPlayer
            ?? throw new InvalidOperationException("NPC Test Lab validation view has no AnimationPlayer.");
        var firstClip = view.PresentationAnimationNames[0];
        var loopChoice = panel.FindChildren("*", "CheckButton", recursive: true, owned: false)
            .OfType<CheckButton>().SingleOrDefault(button => button.Text == "Loop")
            ?? throw new InvalidOperationException("NPC Test Lab validation could not find its loop toggle.");
        loopChoice.ButtonPressed = false;
        buttons["Play clip"].EmitSignal(Button.SignalName.Pressed);
        if (player.CurrentAnimation.ToString() != firstClip || !view.IsNavigationMovementPaused)
            throw new InvalidOperationException("NPC Test Lab play button did not start the selected clip.");
        buttons["Stop / locomotion"].EmitSignal(Button.SignalName.Pressed);
        if (view.IsNavigationMovementPaused)
            throw new InvalidOperationException("NPC Test Lab stop button did not release the one-shot movement hold.");

        NpcAnimationTestMovement? requestedMovement = null;
        NpcAnimationTestAction? requestedAction = null;
        panel.MovementRequested += (_, movement) => requestedMovement = movement;
        panel.ActionRequested += (_, action) => requestedAction = action;
        buttons["Run 4 m"].EmitSignal(Button.SignalName.Pressed);
        buttons["Interact / feed / pet"].EmitSignal(Button.SignalName.Pressed);
        if (requestedMovement != NpcAnimationTestMovement.Run || requestedAction != NpcAnimationTestAction.Interact)
            throw new InvalidOperationException("NPC Test Lab did not dispatch movement and interaction button requests.");

        panel.Close();
    }

    private static void ValidateTickMovement(Node3D holder, ModelCase modelCase)
    {
        var view = new EntityView();
        view.Bind(modelCase.EntityId, modelCase.Speed, modelCase.IsCreature, modelCase.SpeciesId,
            sex: modelCase.Sex == BiologicalSex.Unspecified
                ? (modelCase.Path.Contains("radagon") ? BiologicalSex.Male : BiologicalSex.Female)
                : modelCase.Sex);
        holder.AddChild(view);
        view.SetProcess(false);
        view.SetPhysicsProcess(false);
        if (modelCase.ExpectedEntityViewPath is { } expectedPath
            && view.PresentationModelAssetPath != expectedPath)
            throw new InvalidOperationException(
                $"Starter NPC {modelCase.EntityId} selected '{view.PresentationModelAssetPath}', expected '{expectedPath}'.");
        var player = view.FindChild("AnimationPlayer", recursive: true, owned: false) as AnimationPlayer
            ?? throw new InvalidOperationException($"EntityView model missing for {modelCase.Path}");
        foreach (var fps in new[] { 30, 60, 144 })
        foreach (var simulationSpeed in new[] { SimulationSpeed.OneX, SimulationSpeed.FourX })
        {
            var clock = new SimulationClock();
            clock.SetSpeed(simulationSpeed);
            foreach (var (fraction, expected) in new[] {
                (1f, modelCase.FastClip), (0.2f, modelCase.WalkClip),
                (0f, modelCase.IsCreature ? "idle" : "human_idle") })
            {
                var starts = 0;
                void OnStarted(StringName name) => starts++;
                player.AnimationStarted += OnStarted;
                try
                {
                    for (var frame = 0; frame < fps * 3; frame++)
                    {
                        clock.Advance(1d / fps, seconds =>
                        {
                            var previous = view.GlobalPosition;
                            view.GlobalPosition += Vector3.Right * modelCase.Speed * fraction * (float)seconds;
                            view.SampleLocomotionTick(previous, seconds);
                        });
                        view._Process(1d / fps);
                        player.Advance(1d / fps);
                        if (frame == fps) starts = 0;
                        if (frame > fps) RequireClip(player, expected, modelCase.Path);
                    }
                    if (starts != 0)
                        throw new InvalidOperationException($"{modelCase.Path} restarted a steady gait at {fps} FPS/{simulationSpeed}.");
                }
                finally { player.AnimationStarted -= OnStarted; }
            }
            // Resume movement, then stop delivering ticks as when the simulation is paused.
            for (var frame = 0; frame < fps; frame++)
            {
                clock.Advance(1d / fps, seconds =>
                {
                    var before = view.GlobalPosition;
                    view.GlobalPosition += Vector3.Right * modelCase.Speed * (float)seconds;
                    view.SampleLocomotionTick(before, seconds);
                });
                view._Process(1d / fps);
            }
            RequireClip(player, modelCase.FastClip, modelCase.Path);
            for (var frame = 0; frame < fps; frame++) view._Process(1d / fps);
            RequireClip(player, modelCase.IsCreature ? "idle" : "human_idle", modelCase.Path);
        }
        view.QueueFree();
    }

    private static void RequireClip(AnimationPlayer player, string expected, string assetPath,
        float? speed = null, NpcLocomotionAnimator? animator = null)
    {
        if (speed is { } movementSpeed) animator!.Update(movementSpeed);
        if (!string.Equals(player.CurrentAnimation.ToString(), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"{assetPath} selected '{player.CurrentAnimation}', expected '{expected}'.");
        if (player.GetAnimation(expected).LoopMode != Animation.LoopModeEnum.Linear)
            throw new InvalidOperationException($"NPC clip '{expected}' from {assetPath} is not configured to loop.");
    }
}
