namespace Simulation;

public static class SettlementCrops
{
    public const string FieldCropId = "field-crop";
    public const int GrowthSteps = 5;
    public const int HarvestFood = 3;

    public static CropDefinition CreateFieldCrop() => new(FieldCropId, GrowthSteps, HarvestFood);

    public static CropDefinition GetRequired(string id) => id == FieldCropId
        ? CreateFieldCrop()
        : throw new ArgumentException($"Unknown settlement crop '{id}'.", nameof(id));
}
