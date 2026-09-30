using System.Numerics;
using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class CharacterCoreIntegrationScenarioTests
{
    [Fact]
    public void HumanAndCreatureSpawnsShareCharacterDataAcrossWorldReset()
    {
        var world = new World();
        var catalog = SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        var creatures = new CreatureSpawner(world, catalog);

        var human = world.SpawnEntity("human", Vector3.Zero);
        var creature = creatures.Spawn(new CreatureSpawnRequest("creature", "grove", Vector3.One, 42));

        Assert.Equal(CreatureSpawnStatus.Spawned, creature.Status);
        AssertCharacterCoreData(human);
        AssertCharacterCoreData(creature.Entity!);

        world.Reset();
        var respawned = world.SpawnEntity("human", Vector3.UnitX);
        AssertCharacterCoreData(respawned);
    }

    private static void AssertCharacterCoreData(EntityRecord entity)
    {
        Assert.NotNull(entity.BaseStats);
        Assert.NotNull(entity.Health);
        Assert.NotEmpty(entity.Health.Parts);
        Assert.NotNull(entity.Skills);
        Assert.NotEmpty(entity.Skills.Skills);
        Assert.NotNull(entity.Needs);
        Assert.False(entity.Health.IsIncapacitated);
    }
}
