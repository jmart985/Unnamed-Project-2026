using System.Numerics;

namespace Simulation;

public sealed class NewGameSession
{
    public const int DefaultSeed = AntFarmScenario.DefaultSeed;
    public const int HumanNpcCount = 10;
    public static Vector3 TownWorkbenchPosition => Vector3.Zero;
    private static readonly (string SpeciesId, Vector3 Position)[] StarterCreatureSpawns =
    [
        ("ape", new Vector3(90, 0, 60)),
        ("wolf", new Vector3(150, 0, 40)),
        ("cow", new Vector3(-60, 0, 150))
    ];
    public static int CreatureCount => StarterCreatureSpawns.Length;
    public static IReadOnlyList<string> StarterCreatureSpeciesIds { get; } =
        Array.AsReadOnly(StarterCreatureSpawns.Select(spawn => spawn.SpeciesId).ToArray());

    private static readonly Vector3[] PlayerSpawnPositions =
    [
        new(-3, 0, -3),
        new(0, 0, -3),
        new(3, 0, -3),
        new(-5, 0, 3),
        new(5, 0, 3),
        new(-5, 0, 5),
        new(5, 0, 5)
    ];

    private NewGameSession(World world, int seed, string playerEntityId)
    {
        World = world;
        Seed = seed;
        PlayerEntityId = playerEntityId;
    }

    public World World { get; }
    public int Seed { get; }
    public string PlayerEntityId { get; }
    public EntityRecord Player =>
        World.TryGetEntity(PlayerEntityId, out var player) && player is not null
            ? player
            : throw new InvalidOperationException("The session player is missing from its world.");

    public static NewGameSession Create(
        SpeciesCatalog catalog,
        PlayerCharacterProfile playerProfile,
        int seed = DefaultSeed)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(playerProfile);
        if (!playerProfile.IsValid)
        {
            throw new ArgumentException("The player profile is invalid.", nameof(playerProfile));
        }

        if (seed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seed), "The session seed cannot be negative.");
        }

        catalog.GetRequired("human");
        foreach (var speciesId in StarterCreatureSpeciesIds)
        {
            catalog.GetRequired(speciesId);
        }

        var world = new World { SpeciesCatalog = catalog };
        AddContainer(
            (id, position, contents) => world.AddFoodSource(id, position, contents),
            "food-pile-1",
            new(-8, 0, -8),
            ItemIds.Food,
            100);
        AddContainer(
            (id, position, contents) => world.AddFoodSource(id, position, contents),
            "forage-pile-ape",
            new(90, 0, 61),
            ItemIds.Plant,
            100);
        AddContainer(
            (id, position, contents) => world.AddFoodSource(id, position, contents),
            "forage-pile-cow",
            new(-59, 0, 150),
            ItemIds.Plant,
            100);
        AddContainer(
            (id, position, contents) => world.AddFoodSource(id, position, contents),
            "prey-pile-wolf",
            new(152, 0, 40),
            ItemIds.Meat,
            100);
        AddContainer(
            (id, position, contents) => world.AddResourcePile(id, position, contents),
            "wood-pile-1",
            new(8, 0, -8),
            ItemIds.Wood,
            100);
        world.AddStockpile(
            "stockpile-1",
            new(-8, 0, 8),
            new ResourceContainer("stockpile-1", 100));

        var workbench = world.AddWorkstation("workbench-1", TownWorkbenchPosition);
        workbench.SetRecipe(SettlementRecipes.CreateBasicMeal());
        var farm = world.AddFarm("farm-1", new(5, 0, 5));
        farm.AddPlot(new FarmPlot("farm-1-plot-1", capacity: 1));

        var humanNpcs = new List<EntityRecord>();
        for (var index = 0; index < HumanNpcCount; index++)
        {
            humanNpcs.Add(world.SpawnEntity(
                catalog.GetRequired("human"),
                new Vector3(index - 4.5f, 0, 0)));
        }

        foreach (var creature in StarterCreatureSpawns)
        {
            world.SpawnEntity(catalog.GetRequired(creature.SpeciesId), creature.Position);
        }

        var player = world.SpawnEntity(catalog.GetRequired("human"), GetPlayerSpawnPosition(seed));
        world.SetSessionPlayer(seed, player.Id, playerProfile);
        AssignStarterNpcSchedulesAndHomes(world, humanNpcs);
        return new NewGameSession(world, seed, player.Id);
    }

    private static void AssignStarterNpcSchedulesAndHomes(World world, IReadOnlyList<EntityRecord> humanNpcs)
    {
        var schedules = new[]
        {
            NpcWorkSchedule.Default,
            new NpcWorkSchedule(7, 15),
            new NpcWorkSchedule(8, 16),
            new NpcWorkSchedule(9, 17),
            new NpcWorkSchedule(10, 18),
            new NpcWorkSchedule(12, 20),
            new NpcWorkSchedule(14, 22),
            new NpcWorkSchedule(18, 2),
            new NpcWorkSchedule(20, 4),
            new NpcWorkSchedule(22, 6)
        };
        for (var index = 0; index < humanNpcs.Count; index++)
        {
            var npc = humanNpcs[index];
            var schedule = schedules[index % schedules.Length];
            world.SetWorkSchedule(npc.Id, schedule.StartHour, schedule.EndHour);
            var row = index / 5;
            var column = index % 5;
            world.SetHomePosition(npc.Id, new Vector3(-18 + column * 3.0f, 0, -18 + row * 3.0f));
        }
    }

    public static Vector3 GetPlayerSpawnPosition(int seed)
    {
        if (seed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seed), "The session seed cannot be negative.");
        }

        return PlayerSpawnPositions[seed % PlayerSpawnPositions.Length];
    }

    private static void AddContainer<T>(
        Func<string, Vector3, ResourceContainer, T> add,
        string id,
        Vector3 position,
        string itemId,
        int amount)
    {
        var contents = new ResourceContainer(id, amount);
        if (contents.TryAdd(itemId, amount) != ResourceMutationResult.Success)
        {
            throw new InvalidOperationException($"Unable to seed initial {itemId} resources.");
        }

        add(id, position, contents);
    }
}
