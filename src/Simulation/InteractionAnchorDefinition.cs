using System.Collections.ObjectModel;

namespace Simulation;

/// <summary>
/// Stable IDs used to align simulation interactions with a creature or humanoid scene.
/// These are semantic sockets, not gameplay state and not required to be deforming bones.
/// </summary>
public static class InteractionAnchorIds
{
    public const string Mouth = "anchor_mouth";
    public const string Anus = "anchor_anus";
    public const string Genital = "anchor_genital";
    public const string Urethral = "anchor_urethral";
    public const string Navel = "anchor_navel";

    public const string Vaginal = "anchor_vaginal";
    public const string Penile = "anchor_penile";
    public const string Testicular = "anchor_testicular";
    public const string Udder = "anchor_udder";
    public const string TeatLeft = "anchor_teat_l";
    public const string TeatRight = "anchor_teat_r";
    public const string BreastLeft = "anchor_breast_l";
    public const string BreastRight = "anchor_breast_r";
    public const string Cloaca = "anchor_cloaca";
    public const string Ovipositor = "anchor_ovipositor";
    public const string Proboscis = "anchor_proboscis";
    public const string Beak = "anchor_beak";
    public const string Gill = "anchor_gill";
    public const string ScentGland = "anchor_scent_gland";
    public const string Pouch = "anchor_pouch";
    public const string EggLaying = "anchor_egg_laying";

    public const string Root = "anchor_root";
    public const string FootLeft = "anchor_foot_l";
    public const string FootRight = "anchor_foot_r";
    public const string HandLeft = "anchor_hand_l";
    public const string HandRight = "anchor_hand_r";
    public const string Carry = "anchor_carry";
}

/// <summary>
/// The data contract a species uses to expose anatomy and semantic interaction sockets.
/// Simulation systems consume these IDs instead of imported bone names or scene paths.
/// </summary>
public sealed class InteractionAnchorDefinition
{
    private static readonly ReadOnlyCollection<string> EmptyIds = Array.AsReadOnly(Array.Empty<string>());

    public InteractionAnchorDefinition(
        IEnumerable<string>? anatomyRegions = null,
        IEnumerable<string>? locomotionAnchors = null,
        IEnumerable<string>? equipmentAnchors = null,
        IEnumerable<string>? interactionAnchors = null,
        IEnumerable<string>? breedingAnchors = null)
    {
        AnatomyRegions = CopyAnatomy(anatomyRegions);
        LocomotionAnchors = Copy(locomotionAnchors);
        EquipmentAnchors = Copy(equipmentAnchors);
        InteractionAnchors = Copy(interactionAnchors);
        BreedingAnchors = Copy(breedingAnchors);

        var availableInteractions = new HashSet<string>(InteractionAnchors, StringComparer.Ordinal);
        foreach (var breedingAnchor in BreedingAnchors)
        {
            if (!availableInteractions.Contains(breedingAnchor))
            {
                throw new ArgumentException(
                    $"Breeding anchor '{breedingAnchor}' must also be declared as an interaction anchor.",
                    nameof(breedingAnchors));
            }
        }
    }

    public static InteractionAnchorDefinition Empty { get; } = new();

    public ReadOnlyCollection<string> AnatomyRegions { get; }
    public ReadOnlyCollection<string> LocomotionAnchors { get; }
    public ReadOnlyCollection<string> EquipmentAnchors { get; }
    public ReadOnlyCollection<string> InteractionAnchors { get; }
    public ReadOnlyCollection<string> BreedingAnchors { get; }

    public bool SupportsInteraction(string? anchorId)
    {
        return anchorId is not null && InteractionAnchors.Contains(anchorId, StringComparer.Ordinal);
    }

    public bool SupportsBreeding(string? anchorId)
    {
        return anchorId is not null && BreedingAnchors.Contains(anchorId, StringComparer.Ordinal);
    }

    private static ReadOnlyCollection<string> Copy(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return EmptyIds;
        }

        var result = values.ToArray();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in result)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !value.StartsWith("anchor_", StringComparison.Ordinal)
                || value.Any(character => character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '_'))
            {
                throw new ArgumentException(
                    $"Anchor ID '{value}' must use the lowercase anchor_ naming convention.",
                    nameof(values));
            }

            if (!unique.Add(value))
            {
                throw new ArgumentException($"Anchor ID '{value}' is declared more than once.", nameof(values));
            }
        }

        return Array.AsReadOnly(result);
    }

    private static ReadOnlyCollection<string> CopyAnatomy(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return EmptyIds;
        }

        var result = values.ToArray();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in result)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.Any(character => character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '_'))
            {
                throw new ArgumentException(
                    $"Anatomy region '{value}' must use lowercase letters, digits, and underscores.",
                    nameof(values));
            }

            if (!unique.Add(value))
            {
                throw new ArgumentException($"Anatomy region '{value}' is declared more than once.", nameof(values));
            }
        }

        return Array.AsReadOnly(result);
    }
}
