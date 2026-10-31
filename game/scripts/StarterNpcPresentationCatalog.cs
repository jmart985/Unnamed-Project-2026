using System.Text.Json;
using Godot;
using Simulation;

namespace UnnamedProject2026;

internal sealed record StarterNpcPresentation(
    string EntityId,
    string DisplayName,
    BiologicalSex Sex,
    string AssetPath,
    float ForwardCorrectionDegrees);

/// <summary>Presentation-only mapping from stable starter NPC IDs to imported models.</summary>
internal static class StarterNpcPresentationCatalog
{
    private const string CatalogPath = "res://data/starter_npc_presentations.json";
    private static readonly Lazy<IReadOnlyDictionary<string, StarterNpcPresentation>> Entries =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<StarterNpcPresentation> All => Entries.Value.Values
        .OrderBy(entry => entry.EntityId, StringComparer.Ordinal)
        .ToArray();

    public static bool TryGet(string entityId, BiologicalSex sex,
        out StarterNpcPresentation? presentation)
    {
        if (Entries.Value.TryGetValue(entityId, out var entry) && entry.Sex == sex)
        {
            presentation = entry;
            return true;
        }

        presentation = null;
        return false;
    }

    private static IReadOnlyDictionary<string, StarterNpcPresentation> Load()
    {
        if (!Godot.FileAccess.FileExists(CatalogPath))
            throw new InvalidOperationException($"Starter NPC presentation catalog is missing: {CatalogPath}");

        using var document = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(CatalogPath));
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidOperationException("Unsupported starter NPC presentation catalog schema.");

        var entries = new Dictionary<string, StarterNpcPresentation>(StringComparer.Ordinal);
        foreach (var model in root.GetProperty("models").EnumerateArray())
        {
            var entityId = model.GetProperty("entityId").GetString()
                ?? throw new InvalidOperationException("Starter model entityId cannot be null.");
            var displayName = model.GetProperty("displayName").GetString()
                ?? throw new InvalidOperationException($"Starter model {entityId} displayName cannot be null.");
            var sexName = model.GetProperty("sex").GetString();
            if (!Enum.TryParse<BiologicalSex>(sexName, ignoreCase: true, out var sex)
                || sex is not (BiologicalSex.Male or BiologicalSex.Female))
                throw new InvalidOperationException($"Starter model {entityId} has invalid sex '{sexName}'.");
            var assetPath = model.GetProperty("assetPath").GetString()
                ?? throw new InvalidOperationException($"Starter model {entityId} assetPath cannot be null.");
            var forwardCorrectionDegrees = model.GetProperty("forwardCorrectionDegrees").GetSingle();
            if (!float.IsFinite(forwardCorrectionDegrees) || Math.Abs(forwardCorrectionDegrees) > 360f)
                throw new InvalidOperationException($"Starter model {entityId} has an invalid forward correction.");
            if (!assetPath.StartsWith("res://assets/humanoids/human/npc/", StringComparison.Ordinal)
                || !assetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Starter model {entityId} has an invalid asset path.");

            if (!entries.TryAdd(entityId,
                    new StarterNpcPresentation(entityId, displayName, sex, assetPath, forwardCorrectionDegrees)))
                throw new InvalidOperationException($"Starter NPC {entityId} has multiple presentation models.");
        }

        if (entries.Count != NewGameSession.HumanNpcCount)
            throw new InvalidOperationException(
                $"Starter presentation catalog has {entries.Count} models for {NewGameSession.HumanNpcCount} NPCs.");
        if (entries.Values.Select(entry => entry.AssetPath).Distinct(StringComparer.Ordinal).Count() != entries.Count)
            throw new InvalidOperationException("Every starter NPC must use a distinct model asset.");

        return entries;
    }
}
