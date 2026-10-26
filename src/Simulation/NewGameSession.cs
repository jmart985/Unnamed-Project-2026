using System.Numerics;

namespace Simulation;

public sealed class NewGameSession
{
    public const int DefaultSeed = AntFarmScenario.DefaultSeed;
    public const int HumanNpcCount = 10;
    public const float StarterCreatureTerritoryRadius = 35f;
    private static readonly BiologicalSex[] StarterNpcSexAssignments =
    [
        BiologicalSex.Male,
        BiologicalSex.Female,
        BiologicalSex.Male,
        BiologicalSex.Female,
        BiologicalSex.Male,
        BiologicalSex.Female,
        BiologicalSex.Male,
        BiologicalSex.Female,
        BiologicalSex.Female,
        BiologicalSex.Female
    ];
    public static IReadOnlyList<BiologicalSex> StarterNpcSexes { get; } =
        Array.AsReadOnly(StarterNpcSexAssignments);
    // Keep production work off the two paved road strips and the central crossroads.
    public static Vector3 TownWorkbenchPosition => new(10, 0, 10);
    private static readonly (string SpeciesId, Vector3 Position, double Satiety)[] StarterCreatureSpawns =
    [
        ("ape", new Vector3(90, 0, 60), 60),
        ("wolf", new Vector3(150, 0, 40), 25),
        ("cow", new Vector3(174, 0, 40), 60)
    ];
    public static int CreatureCount => StarterCreatureSpawns.Length;
    public static IReadOnlyList<string> StarterCreatureSpeciesIds { get; } =
        Array.AsReadOnly(StarterCreatureSpawns.Select(spawn => spawn.SpeciesId).ToArray());

    public static Vector3 StarterCowForagePosition => new(174, 0, 41);

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
        int seed = DefaultSeed,
        bool survivalGathering = false,
        WildlifeRegionCatalog? wildlifeRegions = null,
        bool startStarterWildlifeAsFar = false)
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
            StarterCowForagePosition,
            ItemIds.Plant,
            100);
        if (survivalGathering)
        {
            // Deterministic adult trees replace the old free wood box in the
            // player-facing survival test. They remain resource locations until
            // the dedicated punch/chop interactions are wired in.
            var trees = new[] { new Vector3(18, 0, -14), new Vector3(24, 0, -20), new Vector3(-22, 0, -18), new Vector3(30, 0, 12) };
            for (var index = 0; index < trees.Length; index++)
                AddContainer((id, position, contents) => world.AddResourcePile(id, position, contents), $"tree-adult-{index + 1}", trees[index], ItemIds.Wood, 12);
            var rocks = new[] { new Vector3(12, 0, -12), new Vector3(15, 0, -10), new Vector3(-12, 0, -14), new Vector3(-16, 0, -12) };
            for (var index = 0; index < rocks.Length; index++)
                AddContainer((id, position, contents) => world.AddResourcePile(id, position, contents), $"rock-loose-{index + 1}", rocks[index], ItemIds.Rock, 1);
        }
        else
        {
            AddContainer(
                (id, position, contents) => world.AddResourcePile(id, position, contents),
                "wood-pile-1",
                new(8, 0, -8),
                ItemIds.Wood,
                100);
        }
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
                new Vector3(index - 4.5f, 0, 0),
                sex: StarterNpcSexAssignments[index]));
        }

        if (startStarterWildlifeAsFar)
        {
            if (wildlifeRegions is null)
                throw new ArgumentNullException(nameof(wildlifeRegions), "A validated wildlife catalog is required for far-start sessions.");
            foreach (var region in wildlifeRegions.Regions)
            foreach (var species in region.Species)
                world.SetFarPopulation(new FarEntityRecord(region.Id, species.SpeciesId,
                    species.SpawnPositions.Count, species.AverageSatiety, new Dictionary<string, int>()));
        }
        else
        {
            foreach (var creature in StarterCreatureSpawns)
            {
                var entity = world.SpawnEntity(catalog.GetRequired(creature.SpeciesId), creature.Position, creature.Satiety);
                if (world.SetWildTerritory(entity.Id, creature.Position, StarterCreatureTerritoryRadius)
                    != WildTerritoryResult.Accepted)
                    throw new InvalidOperationException($"Unable to assign starter territory to '{entity.Id}'.");
            }
        }

        var player = world.SpawnEntity(catalog.GetRequired("human"), GetPlayerSpawnPosition(seed),
            sex: playerProfile.EntitySex);
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
