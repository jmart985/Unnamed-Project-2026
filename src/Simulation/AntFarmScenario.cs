using System.Numerics;

namespace Simulation;

public sealed record ScenarioStateSummary(
    int HumanCount,
    int CreatureCount,
    int FoodUnits,
    int WoodUnits,
    int StoredWoodUnits,
    long SimulationTicks);

/// <summary>A small deterministic domain scenario used by tests and tooling.</summary>
public sealed class AntFarmScenario
{
    public const int DefaultSeed = 2026;
    public const int HumanCount = 10;
    public const int CreatureCount = 3;

    private readonly SpeciesCatalog _catalog;
    private readonly int _seed;

    public AntFarmScenario(SpeciesCatalog catalog, int seed = DefaultSeed)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        if (seed < 0) throw new ArgumentOutOfRangeException(nameof(seed));
        _seed = seed;
        Reset();
    }

    public World World { get; private set; } = null!;
    public long SimulationTicks { get; private set; }

    public void Reset()
    {
        World = new World();
        SimulationTicks = 0;
        AddContainer(World.AddFoodSource, "food", new(-8, 0, -8), 100, ItemIds.Food, 100);
        AddContainer(World.AddResourcePile, "wood", new(8, 0, -8), 100, ItemIds.Wood, 100);
        AddContainer(World.AddStockpile, "storage", new(-8, 0, 8), 100, null, 0);

        for (var i = 0; i < HumanCount; i++)
        {
            var x = (i % 5) - 2 + ((_seed % 3) * 0.1f);
            var z = (i / 5) - 1;
            World.SpawnEntity(_catalog.GetRequired("human"), new(x, 0, z));
        }

        for (var i = 0; i < CreatureCount; i++)
        {
            World.SpawnEntity(_catalog.GetRequired("creature"), new(i - 1, 0, 4));
        }
    }

    public void Advance(int ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        for (var i = 0; i < ticks; i++)
        {
            foreach (var entity in World.Entities)
            {
                var species = _catalog.GetRequired(entity.SpeciesId);
                entity.Needs.Advance(0.1, species.HungerRate);
            }

            SimulationTicks++;
        }
    }

    public ScenarioStateSummary Summarize()
    {
        return new ScenarioStateSummary(
            World.Entities.Count(entity => entity.SpeciesId == "human"),
            World.Entities.Count(entity => entity.SpeciesId == "creature"),
            World.FoodSources.Sum(source => source.Contents.Counts.GetValueOrDefault(ItemIds.Food)),
            World.ResourcePiles.Sum(pile => pile.Contents.Counts.GetValueOrDefault(ItemIds.Wood)),
            World.Stockpiles.Sum(pile => pile.Contents.Counts.GetValueOrDefault(ItemIds.Wood)),
            SimulationTicks);
    }

    private static void AddContainer<T>(Func<string, Vector3, ResourceContainer, T> add, string id, Vector3 position, int capacity, string? itemId, int amount)
    {
        var contents = new ResourceContainer(id, capacity);
        if (itemId is not null) contents.TryAdd(itemId, amount);
        add(id, position, contents);
    }
}
