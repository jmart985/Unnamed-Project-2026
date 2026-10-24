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
    public void StarterHumansSpawnClearOfWorkbenchPlayerAndEachOther()
    {
        var catalog = GetSpeciesCatalog();
        foreach (var seed in Enumerable.Range(0, 7))
        {
            var session = NewGameSession.Create(catalog, PlayerCharacterProfile.Create("Tester"), seed);
            var starterHumans = session.World.Entities
                .Where(entity => entity.SpeciesId == "human" && entity.Id != session.PlayerEntityId)
                .OrderBy(entity => entity.Id, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(NewGameSession.HumanNpcCount, starterHumans.Length);
            Assert.Equal(new Vector3(10, 0, 10), session.World.Workstations.Single().Position);
            Assert.True(MathF.Abs(session.World.Workstations.Single().Position.X) > 5.5f);
            Assert.True(MathF.Abs(session.World.Workstations.Single().Position.Z) > 5.2f);
            for (var index = 0; index < starterHumans.Length; index++)
            {
                var npc = starterHumans[index];
                var expectedPosition = new Vector3(index - 4.5f, 0, 0);
                Assert.Equal(expectedPosition, npc.Position);
                // Test distance from the NPC capsule to the workbench's 2.0 m x 1.4 m footprint.
                var distanceX = MathF.Max(MathF.Abs(npc.Position.X - NewGameSession.TownWorkbenchPosition.X) - 1.0f, 0);
                var distanceZ = MathF.Max(MathF.Abs(npc.Position.Z - NewGameSession.TownWorkbenchPosition.Z) - 0.7f, 0);
                Assert.True(MathF.Sqrt(distanceX * distanceX + distanceZ * distanceZ) > 0.32f);
                Assert.True(HorizontalDistance(npc.Position, session.Player.Position) > 0.32f + 0.4f);
            }
            for (var first = 0; first < starterHumans.Length; first++)
            for (var second = first + 1; second < starterHumans.Length; second++)
                Assert.True(HorizontalDistance(starterHumans[first].Position, starterHumans[second].Position) >= 0.64f);
            Assert.Equal(9, session.World.WorkSchedules.Count); // One NPC keeps the implicit default schedule.
            Assert.Equal(10, session.World.HomePositions.Count);
            Assert.All(starterHumans, npc => Assert.True(session.World.GetWorkSchedule(npc.Id).IsValid));
            Assert.Equal(300, session.World.FoodSources.Sum(source => source.Contents.Counts.Values.Sum()));
            Assert.Equal(100, session.World.ResourcePiles.Single().Contents.Counts[ItemIds.Wood]);
            Assert.Empty(session.World.Stockpiles.Single().Contents.Counts);
        }
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
        => new Vector2(first.X - second.X, first.Z - second.Z).Length();

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
