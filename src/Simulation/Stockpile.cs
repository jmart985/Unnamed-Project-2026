namespace Simulation;

public sealed class Stockpile : ItemContainer
{
    public Stockpile(ItemCatalog catalog, int capacity)
        : base(catalog, capacity)
    {
    }
}
