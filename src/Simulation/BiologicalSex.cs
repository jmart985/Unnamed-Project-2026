namespace Simulation;

/// <summary>
/// Biological sex stored on every individual entity. Species-specific reproduction rules
/// decide which values can reproduce together; this field alone does not imply compatibility.
/// </summary>
public enum BiologicalSex
{
    Unspecified = 0,
    Female = 1,
    Male = 2,
    Hermaphrodite = 3,
    Asexual = 4
}

internal static class BiologicalSexAssignment
{
    public static BiologicalSex ForEntity(string entityId, string speciesId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(speciesId);

        // Stable across runs and save/load; assigning sex must not consume simulation RNG.
        var hash = 2166136261u;
        foreach (var character in entityId)
            hash = (hash ^ character) * 16777619u;
        hash = (hash ^ (uint)'|') * 16777619u;
        foreach (var character in speciesId)
            hash = (hash ^ character) * 16777619u;
        return (hash & 1u) == 0 ? BiologicalSex.Female : BiologicalSex.Male;
    }
}
