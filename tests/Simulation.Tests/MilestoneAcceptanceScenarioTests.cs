using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class MilestoneAcceptanceScenarioTests
{
    [Fact]
    public void AntFarmRuns600SecondsAndPreservesResourcesAcrossLoad()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var nav = new Dictionary<string, InstantNavigation>(StringComparer.Ordinal);
        Container(world.AddFoodSource, "food", new(-8, 0, -8), 100, ItemIds.Food, 100);
        Container(world.AddResourcePile, "wood", new(8, 0, -8), 100, ItemIds.Wood, 100);
        Container(world.AddStockpile, "storage", new(-8, 0, 8), 100, null, 0);
        for (var i = 0; i < 10; i++) Spawn("human", new(i - 4.5f, 0, 0));
        for (var i = 0; i < 3; i++) Spawn("creature", new(i - 1, 0, 4));
        var eating = new FoodConsumptionService(world, GetNav);
        var hauling = new WoodHaulingService(world, GetNav);
        var wandering = new WanderingService(world, GetNav, [new(-5, 0, -3), new(4, 0, 3)]);
        var eaten = new HashSet<string>(StringComparer.Ordinal);
        var meals = 0;
        var loaded = false;
        var carriedAtSave = 0;

        for (var tick = 1; tick <= 6_000; tick++)
        {
            foreach (var entity in world.Entities)
            {
                var definition = catalog.GetRequired(entity.SpeciesId);
                entity.Needs.Advance(0.1, satietyOverride: definition.HungerRate);
                nav[entity.Id].Arrive(world, entity.Id);
            }
            foreach (var entity in world.Entities)
            {
                var definition = catalog.GetRequired(entity.SpeciesId);
                if (eating.Advance(entity, definition, tick).Status == FoodConsumptionStatus.Ate) { eaten.Add(entity.Id); meals++; }
                hauling.Advance(entity, definition, tick);
                wandering.Advance(entity, definition);
            }
            if (!loaded && (carriedAtSave = world.Entities.Sum(e => e.Inventory.GetValueOrDefault(ItemIds.Wood))) > 0)
            {
                var time = tick * 0.1;
                var json = WorldSnapshotSerializer.Serialize(world, time);
                Assert.Equal(time, WorldSnapshotSerializer.Load(world, json, catalog, eating, hauling, wandering), precision: 6);
                Assert.Equal(json, WorldSnapshotSerializer.Serialize(world, time));
                loaded = true;
            }
            Assert.Equal(100, TotalWood(world));
            AssertResourceBounds(world);
        }

        Assert.True(loaded);
        Assert.True(carriedAtSave > 0);
        Assert.Equal(13, eaten.Count);
        Assert.Equal(26, meals);
        Assert.Equal(74, world.FoodSources.Single().Contents.Counts[ItemIds.Food]);
        Assert.Equal(100, world.Stockpiles.Single().Contents.Counts[ItemIds.Wood]);
        Assert.Equal(100, TotalWood(world));

        void Spawn(string species, Vector3 position) { var entity = world.SpawnEntity(species, position); nav.Add(entity.Id, new InstantNavigation()); }
        INavigationAdapter GetNav(EntityRecord entity) => nav[entity.Id];
    }

    [Fact]
    public void AcceptanceClockChecksPauseAndFourTimesSpeed()
    {
        var clock = new SimulationClock();
        var ticks = 0;
        clock.SetPaused(true);
        Assert.Equal(0, clock.Advance(5, _ => ticks++));
        clock.SetPaused(false);
        Assert.Equal(1, clock.Advance(0.1, _ => ticks++));
        clock.SetSpeed(SimulationSpeed.FourX);
        Assert.Equal(4, clock.Advance(0.1, _ => ticks++));
        Assert.Equal(0.5, clock.SimulationTime, precision: 6);
        Assert.Equal(5, ticks);
    }

    private static ResourceLocation Container(Func<string, Vector3, ResourceContainer, ResourceLocation> add, string id, Vector3 p, int capacity, string? item, int amount)
    {
        var contents = new ResourceContainer(id, capacity);
        if (item is not null) Assert.Equal(ResourceMutationResult.Success, contents.TryAdd(item, amount));
        return add(id, p, contents);
    }

    private static FoodSource Container(Func<string, Vector3, ResourceContainer, FoodSource> add, string id, Vector3 p, int capacity, string item, int amount)
    {
        var contents = new ResourceContainer(id, capacity);
        Assert.Equal(ResourceMutationResult.Success, contents.TryAdd(item, amount));
        return add(id, p, contents);
    }

    private static int TotalWood(World world) => world.ResourcePiles.Sum(x => x.Contents.Counts.GetValueOrDefault(ItemIds.Wood)) + world.Stockpiles.Sum(x => x.Contents.Counts.GetValueOrDefault(ItemIds.Wood)) + world.Entities.Sum(x => x.Inventory.GetValueOrDefault(ItemIds.Wood));

    private static void AssertResourceBounds(World world)
    {
        foreach (var container in world.FoodSources.Select(x => x.Contents).Concat(world.ResourcePiles.Select(x => x.Contents)).Concat(world.Stockpiles.Select(x => x.Contents))) Assert.InRange(container.Counts.Values.Sum(), 0, container.Capacity);
        Assert.All(world.Entities, entity => Assert.All(entity.Inventory.Values,
            count => Assert.InRange(count, 0, EntityRecord.MaxCarriedCountPerItem)));
    }

    private sealed class InstantNavigation : INavigationAdapter
    {
        private readonly Dictionary<string, (Vector3 Target, NavigationOutcome Outcome)> requests = new(StringComparer.Ordinal);
        private int next;
        public string Request(NavigationRequest request) { var id = $"acceptance-{++next}"; requests[id] = (request.Target, NavigationOutcome.InProgress); return id; }
        public NavigationOutcome Poll(string id, double _) => requests.TryGetValue(id, out var state) ? state.Outcome : NavigationOutcome.Removed;
        public bool Cancel(string id) => Set(id, NavigationOutcome.Cancelled);
        public bool RemoveTarget(string id) => Set(id, NavigationOutcome.Removed);
        public void Arrive(World world, string entityId)
        {
            foreach (var id in requests.Keys.ToArray()) if (requests[id].Outcome == NavigationOutcome.InProgress) { Assert.True(world.TrySetEntityPosition(entityId, requests[id].Target)); Set(id, NavigationOutcome.Arrived); }
        }
        private bool Set(string id, NavigationOutcome outcome) { if (!requests.TryGetValue(id, out var state) || state.Outcome != NavigationOutcome.InProgress) return false; requests[id] = (state.Target, outcome); return true; }
    }
}
