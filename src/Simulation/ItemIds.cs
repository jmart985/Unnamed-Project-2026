namespace Simulation;

public static class ItemIds
{
    public const string Food = "food";
    public const string Plant = "plant";
    public const string Meat = "meat";
    public const string Fruit = "fruit";
    public const string Wood = "wood";
    public const string Rock = "rock";
    public const string Axe = "axe";
    public const string Coin = "coin";
    public const string WorkShirt = "work_shirt";
    public const string WorkTrousers = "work_trousers";
    public const string DeluxeBunnySuitTop = "deluxe_bunny_suit_top";
    public const string DeluxeBunnySuitLegs = "deluxe_bunny_suit_legs";

    public static bool IsKnown(string? itemId) => ItemCatalog.Contains(itemId);

    public static bool IsPlantFood(string? itemId) => itemId is Food or Plant or Fruit;
    public static bool IsMeat(string? itemId) => itemId is Meat;
    public static bool IsEdibleFood(string? itemId) => IsPlantFood(itemId) || IsMeat(itemId);
}
