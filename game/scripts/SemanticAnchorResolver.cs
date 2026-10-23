using Godot;
using Simulation;

namespace UnnamedProject2026;

public enum SemanticAnchorKind
{
    Locomotion,
    Equipment,
    Interaction,
    Breeding
}

public enum SemanticAnchorResolutionStatus
{
    Resolved,
    Undeclared,
    Missing,
    Duplicate,
    InvalidNode
}

public sealed record SemanticAnchorResolution(
    SemanticAnchorResolutionStatus Status,
    Transform3D? Transform,
    string Message)
{
    public bool IsResolved => Status == SemanticAnchorResolutionStatus.Resolved && Transform.HasValue;
}

/// <summary>
/// Maps a species-declared semantic ID to a model scene's named Node3D locator.
/// Imported bone names and node paths stay inside this presentation adapter.
/// </summary>
public static class SemanticAnchorResolver
{
    public static SemanticAnchorResolution Resolve(
        Node3D modelRoot,
        InteractionAnchorDefinition declaration,
        SemanticAnchorKind kind,
        string anchorId)
    {
        ArgumentNullException.ThrowIfNull(modelRoot);
        ArgumentNullException.ThrowIfNull(declaration);

        if (string.IsNullOrWhiteSpace(anchorId))
        {
            return Unavailable(SemanticAnchorResolutionStatus.Missing, "An anchor ID is required.");
        }

        if (!IsDeclared(declaration, kind, anchorId))
        {
            return Unavailable(SemanticAnchorResolutionStatus.Undeclared,
                $"Species does not declare {kind.ToString().ToLowerInvariant()} anchor '{anchorId}'.");
        }

        var matchingNodes = new List<Node>();
        CollectNamedNodes(modelRoot, anchorId, matchingNodes);
        if (matchingNodes.Count == 0)
        {
            return Unavailable(SemanticAnchorResolutionStatus.Missing,
                $"Model scene does not expose declared anchor '{anchorId}'.");
        }

        if (matchingNodes.Count > 1)
        {
            return Unavailable(SemanticAnchorResolutionStatus.Duplicate,
                $"Model scene exposes anchor '{anchorId}' {matchingNodes.Count} times; semantic IDs must be unique.");
        }

        if (matchingNodes[0] is not Node3D locator)
        {
            return Unavailable(SemanticAnchorResolutionStatus.InvalidNode,
                $"Model node '{anchorId}' must be a Node3D transform.");
        }

        return new SemanticAnchorResolution(
            SemanticAnchorResolutionStatus.Resolved,
            locator.GlobalTransform,
            $"Resolved anchor '{anchorId}'.");
    }

    private static bool IsDeclared(InteractionAnchorDefinition definition, SemanticAnchorKind kind, string anchorId)
    {
        var collection = kind switch
        {
            SemanticAnchorKind.Locomotion => definition.LocomotionAnchors,
            SemanticAnchorKind.Equipment => definition.EquipmentAnchors,
            SemanticAnchorKind.Interaction => definition.InteractionAnchors,
            SemanticAnchorKind.Breeding => definition.BreedingAnchors,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown semantic anchor collection.")
        };

        return collection.Contains(anchorId, StringComparer.Ordinal);
    }

    private static void CollectNamedNodes(Node node, string anchorId, ICollection<Node> matchingNodes)
    {
        if (string.Equals(node.Name.ToString(), anchorId, StringComparison.Ordinal))
        {
            matchingNodes.Add(node);
        }

        foreach (var child in node.GetChildren())
        {
            CollectNamedNodes(child, anchorId, matchingNodes);
        }
    }

    private static SemanticAnchorResolution Unavailable(SemanticAnchorResolutionStatus status, string message)
        => new(status, null, message);
}
