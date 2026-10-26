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
                await ValidateManualPlayback(player, animator, modelCase);
                ValidateTickMovement(holder, modelCase);
            }

            await ValidateTestPanel(holder);

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
        player.Advance(player.GetAnimation(modelCase.WalkClip).Length + 0.05);
        await player.ToSignal(player.GetTree(), SceneTree.SignalName.ProcessFrame);
        animator.Update(modelCase.Speed * 0.95f);
        RequireClip(player, modelCase.FastClip, modelCase.Path);
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
        buttons["Play clip"].EmitSignal(Button.SignalName.Pressed);
        if (player.CurrentAnimation.ToString() != firstClip)
            throw new InvalidOperationException("NPC Test Lab play button did not start the selected clip.");

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
