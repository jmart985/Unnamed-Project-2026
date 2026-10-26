using Godot;

namespace UnnamedProject2026;

public partial class Main
{
    private void RunNpcLocomotionValidation() => NpcLocomotionValidation.Run(this);
}

internal static class NpcLocomotionValidation
{
    private sealed record ModelCase(string Path, string SpeciesId, bool IsCreature, float Speed,
        string WalkClip, string MidSpeedClip, string FastClip);

    public static async void Run(Main main)
    {
        Node3D? holder = null;
        try
        {
            var cases = new[]
            {
                new ModelCase("res://assets/humanoids/human/human_humanoid_rig_v1.glb", "human", false, 3f,
                    "human_walk", "human_walk", "human_run"),
                new ModelCase("res://assets/humanoids/human/radagon_male_humanoid_rig_v1.glb", "human", false, 3f,
                    "human_walk", "human_walk", "human_run"),
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
            }

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
