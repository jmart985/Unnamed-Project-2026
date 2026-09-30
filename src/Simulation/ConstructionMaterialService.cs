namespace Simulation;

public enum ConstructionMaterialResult { Success, MissingMaterial, InvalidInput }

public static class ConstructionMaterialService
{
    public static ConstructionMaterialResult TryConsume(SettlementStorage? storage, ConstructionBlueprint? blueprint)
    {
        if (storage is null || blueprint is null) return ConstructionMaterialResult.InvalidInput;
        foreach (var requirement in blueprint.RequiredMaterials)
        {
            var needed = blueprint.Remaining(requirement.Key);
            if (needed > 0 && (!storage.Contents.TryGetCount(requirement.Key, out var count) || count < needed)) return ConstructionMaterialResult.MissingMaterial;
        }
        foreach (var requirement in blueprint.RequiredMaterials)
        {
            var needed = blueprint.Remaining(requirement.Key);
            if (needed > 0) { storage.Contents.TryRemove(requirement.Key, needed); blueprint.ApplyProgress(requirement.Key, needed); }
        }
        return ConstructionMaterialResult.Success;
    }
}
