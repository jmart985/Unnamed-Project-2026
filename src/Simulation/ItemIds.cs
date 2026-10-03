namespace Simulation;

public static class ItemIds
{
    public const string Food = "food";
    public const string Plant = "plant";
    public const string Meat = "meat";
    public const string Fruit = "fruit";
    public const string Wood = "wood";
    public const string Coin = "coin";

    public static bool IsKnown(string? itemId)
    {
        return itemId is ItemIds.Food or ItemIds.Plant or ItemIds.Meat or ItemIds.Fruit or ItemIds.Wood or ItemIds.Coin;
    }

    public static bool IsPlantFood(string? itemId) => itemId is Food or Plant or Fruit;
    public static bool IsMeat(string? itemId) => itemId is Meat;
    public static bool IsEdibleFood(string? itemId) => IsPlantFood(itemId) || IsMeat(itemId);
}
