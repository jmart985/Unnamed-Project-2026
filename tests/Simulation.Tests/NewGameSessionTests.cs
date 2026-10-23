using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class NewGameSessionTests
{
    private static readonly string[] ExpectedStarterCreatureSpeciesIds = ["ape", "cow", "wolf"];

    [Fact]
    public void SurvivalGatheringFixtureStartsWithTreesAndLooseRocksInsteadOfWoodBox()
    {
        var session = NewGameSession.Create(GetSpeciesCatalog(), PlayerCharacterProfile.Create("Survivor"), survivalGathering: true);

        Assert.DoesNotContain(session.World.ResourcePiles, pile => pile.Id == "wood-pile-1");
        Assert.Equal(4, session.World.ResourcePiles.Count(pile => pile.Id.StartsWith("tree-adult-", StringComparison.Ordinal)));
        Assert.Equal(4, session.World.ResourcePiles.Count(pile => pile.Id.StartsWith("rock-loose-", StringComparison.Ordinal)));
        Assert.All(session.World.ResourcePiles.Where(pile => pile.Id.StartsWith("tree-adult-", StringComparison.Ordinal)),
            tree => Assert.Equal(12, tree.Contents.Counts[ItemIds.Wood]));
        Assert.All(session.World.ResourcePiles.Where(pile => pile.Id.StartsWith("rock-loose-", StringComparison.Ordinal)),
            rock => Assert.Equal(1, rock.Contents.Counts[ItemIds.Rock]));
    }

    [Fact]
    public void CreateBuildsTheDocumentedWorldAndPlayerState()
    {
        var profile = PlayerCharacterProfile.Create("  Ada  ");
        var session = NewGameSession.Create(GetSpeciesCatalog(), profile);
        var player = session.Player;
        var createdProfile = session.World.PlayerProfile!;

        Assert.Equal(NewGameSession.DefaultSeed, session.Seed);
        Assert.Equal(session.Seed, session.World.SessionSeed);
        Assert.Equal(player.Id, session.World.PlayerEntityId);
        Assert.Same(profile, session.World.PlayerProfile);
        Assert.Equal("Ada", createdProfile.Name);
        Assert.Equal("human", player.SpeciesId);
        Assert.Equal(NewGameSession.GetPlayerSpawnPosition(session.Seed), player.Position);
        Assert.Empty(player.Inventory);
        Assert.Equal(60, player.Satiety);
        Assert.Equal(CharacterStats.Default, player.BaseStats);
        Assert.Equal(PlayerHeight.Medium, createdProfile.Height);
        Assert.Equal(PlayerBodyType.Average, createdProfile.BodyType);
        Assert.Equal(PlayerSex.Female, createdProfile.Sex);

        Assert.Equal(NewGameSession.HumanNpcCount,
            session.World.Entities.Count(entity =>
                entity.SpeciesId == "human" && entity.Id != player.Id));
        Assert.Equal(
            ExpectedStarterCreatureSpeciesIds,
            NewGameSession.StarterCreatureSpeciesIds.OrderBy(speciesId => speciesId, StringComparer.Ordinal));
        var starterCreatures = session.World.Entities
            .Where(entity => entity.SpeciesId != "human")
            .ToArray();
        Assert.Equal(
            ExpectedStarterCreatureSpeciesIds,
            starterCreatures
                .Select(entity => entity.SpeciesId)
                .OrderBy(speciesId => speciesId, StringComparer.Ordinal));
        Assert.Equal(NewGameSession.CreatureCount, starterCreatures.Length);
        Assert.Equal(NewGameSession.HumanNpcCount + NewGameSession.CreatureCount + 1,
            session.World.Entities.Count);
        Assert.Equal(3, session.World.FoodSources.Count);
        Assert.All(session.World.FoodSources, source => Assert.Equal(100, source.Contents.Counts.Values.Sum()));
        foreach (var speciesId in ExpectedStarterCreatureSpeciesIds)
        {
            var species = GetSpeciesCatalog().GetRequired(speciesId);
            var creature = starterCreatures.Single(entity => entity.SpeciesId == speciesId);
            Assert.Equal(species.MaxHealth, creature.BaseStats.MaxHealth);
            Assert.Equal(species.MovementSpeed, creature.BaseStats.MovementSpeed);
            Assert.Equal(species.Strength, creature.BaseStats.Strength);
            Assert.Equal(species.Endurance, creature.BaseStats.Endurance);
            if (species.Id == "wolf")
            {
                Assert.True(species.IsPredator);
                Assert.Contains("cow", species.PreySpeciesIds);
                Assert.True(creature.Needs.IsHungry);
                Assert.DoesNotContain(session.World.FoodSources, source =>
                    source.Contents.Counts.GetValueOrDefault(ItemIds.Meat) > 0);
            }
            else
            {
                Assert.Contains(session.World.FoodSources, source =>
                    source.Contents.Counts.Any(item => item.Value > 0 && species.Eats(item.Key))
                    && Vector3.DistanceSquared(creature.Position, source.Position)
                        <= species.SmellRange * species.SmellRange);
            }
        }
        Assert.Equal(100, session.World.FoodSources.Single(source =>
            source.Contents.Counts.ContainsKey(ItemIds.Food)).Contents.Counts[ItemIds.Food]);
        Assert.Equal(100, session.World.ResourcePiles.Single().Contents.Counts[ItemIds.Wood]);
        Assert.Equal(100, session.World.Stockpiles.Single().Contents.Capacity);
        Assert.Empty(session.World.Stockpiles.Single().Contents.Counts);
    }

    [Fact]
    public void SameSeedAndProfileProduceTheSameInitialSnapshot()
    {
        var catalog = GetSpeciesCatalog();
        var profile = PlayerCharacterProfile.Create(
            "Mara",
            PlayerHeight.Tall,
            PlayerBodyType.Athletic,
            PlayerSex.Male);

        var first = NewGameSession.Create(catalog, profile, seed: 2026);
        var second = NewGameSession.Create(catalog, profile, seed: 2026);
        var otherSeed = NewGameSession.Create(catalog, profile, seed: 2027);

        Assert.Equal(
            WorldSnapshotSerializer.Serialize(first.World, simulationTime: 0),
            WorldSnapshotSerializer.Serialize(second.World, simulationTime: 0));
        Assert.NotEqual(first.Player.Position, otherSeed.Player.Position);
    }

    [Fact]
    public void InvalidPlayerCreationInputsAreRejectedBeforeSessionCreation()
    {
        var catalog = GetSpeciesCatalog();

        Assert.Throws<ArgumentException>(() => PlayerCharacterProfile.Create(" \t "));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlayerCharacterProfile.Create("Ada", (PlayerHeight)99));
        Assert.Throws<ArgumentException>(() =>
            NewGameSession.Create(catalog, new PlayerCharacterProfile(" ")));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Ada"), seed: -1));
    }

    [Fact]
    public void RemovingThePlayerClearsItsIdentityAndProfileButKeepsTheWorldSeed()
    {
        var session = NewGameSession.Create(
            GetSpeciesCatalog(),
            PlayerCharacterProfile.Create("Ada"),
            seed: 12);

        Assert.True(session.World.TryRemoveEntity(session.PlayerEntityId));

        Assert.Null(session.World.PlayerEntityId);
        Assert.Null(session.World.PlayerProfile);
        Assert.Equal(12, session.World.SessionSeed);
        var serialized = WorldSnapshotSerializer.Serialize(session.World, 0);
        Assert.Equal(12, WorldSnapshotSerializer.Deserialize(serialized).SessionSeed);
    }

    private static SpeciesCatalog GetSpeciesCatalog()
    {
        return SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    }
}
