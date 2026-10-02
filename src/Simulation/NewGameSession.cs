using System.Numerics;

namespace Simulation;

public sealed class NewGameSession
{
    public const int DefaultSeed = AntFarmScenario.DefaultSeed;
    public const int HumanNpcCount = 10;
    public const int CreatureCount = 3;
    public const string CreatureSpeciesId = "creature";

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
        catalog.GetRequired(CreatureSpeciesId);

        var world = new World();
        AddContainer(
            (id, position, contents) => world.AddFoodSource(id, position, contents),
            "food-pile-1",
            new(-8, 0, -8),
            ItemIds.Food,
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

        for (var index = 0; index < HumanNpcCount; index++)
        {
            world.SpawnEntity(
                "human",
                new Vector3(index - 4.5f, 0, 0));
        }

        for (var index = 0; index < CreatureCount; index++)
        {
            world.SpawnEntity(
                CreatureSpeciesId,
                new Vector3(index - 1, 0, 4));
        }

        var player = world.SpawnEntity("human", GetPlayerSpawnPosition(seed));
        world.SetSessionPlayer(seed, player.Id, playerProfile);
        return new NewGameSession(world, seed, player.Id);
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
