using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private void RunInteractionAnchorValidation()
    {
        try
        {
            var anchors = new InteractionAnchorDefinition(
                locomotionAnchors: [InteractionAnchorIds.FootLeft],
                interactionAnchors: [InteractionAnchorIds.Mouth, InteractionAnchorIds.HandLeft, InteractionAnchorIds.Testicular],
                breedingAnchors: [InteractionAnchorIds.Mouth, InteractionAnchorIds.Testicular],
                equipmentAnchors: [InteractionAnchorIds.HandLeft]);
            var root = new Node3D { Name = "AnchorValidationRoot", Position = new Vector3(10, 2, -4) };
            var parent = new Node3D { Name = "Model", Position = new Vector3(1, 3, 2) };
            var mouth = new Node3D { Name = InteractionAnchorIds.Mouth, Position = new Vector3(0.25f, 0.5f, -0.75f) };
            root.AddChild(parent);
            parent.AddChild(mouth);
            parent.AddChild(new Node3D { Name = InteractionAnchorIds.HandLeft, Position = new Vector3(-0.4f, 0.8f, 0) });
            parent.AddChild(new Node3D { Name = InteractionAnchorIds.FootLeft, Position = new Vector3(-0.2f, 0.1f, 0.1f) });
            AddChild(root);

            var resolved = SemanticAnchorResolver.Resolve(root, anchors, SemanticAnchorKind.Interaction, InteractionAnchorIds.Mouth);
            Require(resolved.IsResolved && resolved.Transform!.Value.Origin.IsEqualApprox(new Vector3(11.25f, 5.5f, -2.75f)),
                "declared anchor resolves its world transform through transformed parents");

            var equipment = SemanticAnchorResolver.Resolve(root, anchors, SemanticAnchorKind.Equipment, InteractionAnchorIds.HandLeft);
            Require(equipment.IsResolved && equipment.Transform!.Value.Origin.IsEqualApprox(new Vector3(10.6f, 5.8f, -2f)),
                "equipment anchor resolves from its category");

            var locomotion = SemanticAnchorResolver.Resolve(root, anchors, SemanticAnchorKind.Locomotion, InteractionAnchorIds.FootLeft);
            Require(locomotion.IsResolved && locomotion.Transform!.Value.Origin.IsEqualApprox(new Vector3(10.8f, 5.1f, -1.9f)),
                "locomotion anchor resolves from its category");

            var breeding = SemanticAnchorResolver.Resolve(root, anchors, SemanticAnchorKind.Breeding, InteractionAnchorIds.Mouth);
            Require(breeding.IsResolved, "breeding anchor resolves from the breeding subset");

            var undeclared = SemanticAnchorResolver.Resolve(root, anchors, SemanticAnchorKind.Equipment, InteractionAnchorIds.Mouth);
            Require(undeclared.Status == SemanticAnchorResolutionStatus.Undeclared && !undeclared.Transform.HasValue,
                "a socket in the model cannot bypass the species anchor category");

            var missing = SemanticAnchorResolver.Resolve(root, anchors, SemanticAnchorKind.Breeding, InteractionAnchorIds.Testicular);
            Require(missing.Status == SemanticAnchorResolutionStatus.Missing && !missing.Transform.HasValue,
                "missing declared anchor does not fall back to the model origin");

            var invalidRoot = new Node3D { Name = "InvalidAnchorValidationRoot" };
            invalidRoot.AddChild(new Node { Name = InteractionAnchorIds.HandLeft });
            AddChild(invalidRoot);
            var invalidNode = SemanticAnchorResolver.Resolve(invalidRoot, anchors, SemanticAnchorKind.Equipment,
                InteractionAnchorIds.HandLeft);
            Require(invalidNode.Status == SemanticAnchorResolutionStatus.InvalidNode && !invalidNode.Transform.HasValue,
                "semantic anchors must be Node3D transforms");
            invalidRoot.QueueFree();

            var secondBranch = new Node3D { Name = "SecondModelPart" };
            secondBranch.AddChild(new Node3D { Name = InteractionAnchorIds.Mouth });
            root.AddChild(secondBranch);
            var duplicate = SemanticAnchorResolver.Resolve(root, anchors, SemanticAnchorKind.Interaction, InteractionAnchorIds.Mouth);
            Require(duplicate.Status == SemanticAnchorResolutionStatus.Duplicate && !duplicate.Transform.HasValue,
                "duplicate semantic locator is rejected");

            root.QueueFree();
            GD.Print("INTERACTION ANCHOR VALIDATION PASSED: declarations, world transforms, missing, undeclared and duplicate sockets.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"INTERACTION ANCHOR VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }
}
