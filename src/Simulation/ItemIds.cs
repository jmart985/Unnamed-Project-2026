namespace Simulation;

public static class ItemIds
{
    public const string Food = "food";
    public const string Wood = "wood";

    public static bool IsKnown(string? itemId)
    {
        return itemId is ItemIds.Food or ItemIds.Wood;
    }
}
