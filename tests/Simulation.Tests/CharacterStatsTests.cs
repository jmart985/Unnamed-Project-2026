namespace Simulation.Tests;

public sealed class CharacterStatsTests
{
    [Fact]
    public void DefaultsProvideAUsableBaseline()
    {
        var stats = CharacterStats.Default;

        Assert.Equal(CharacterStats.DefaultHealth, stats.MaxHealth);
        Assert.Equal(CharacterStats.DefaultStamina, stats.MaxStamina);
        Assert.Equal(CharacterStats.DefaultMovementSpeed, stats.MovementSpeed);
        Assert.Equal(CharacterStats.DefaultCarryCapacity, stats.CarryCapacity);
        Assert.Equal(CharacterStats.DefaultPerception, stats.Perception);
        Assert.Equal(CharacterStats.DefaultDefense, stats.Defense);
    }

    [Theory]
    [InlineData(0, 100, 3, 10, 1, 0)]
    [InlineData(100, 0, 3, 10, 1, 0)]
    [InlineData(100, 100, 0, 10, 1, 0)]
    [InlineData(100, 100, 3, -1, 1, 0)]
    [InlineData(100, 100, 3, 10, -1, 0)]
    [InlineData(100, 100, 3, 10, 1, -1)]
    public void RejectsInvalidValues(double health, double stamina, double movementSpeed, int carryCapacity, double perception, double defense)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CharacterStats(health, stamina, movementSpeed, carryCapacity, perception, defense));
    }

    [Fact]
    public void WorldEntitiesReceiveTheConfiguredSharedStats()
    {
        var configured = new CharacterStats(health: 125, stamina: 80, movementSpeed: 4, carryCapacity: 20, perception: 2, defense: 3);
        var world = new World(defaultCharacterStats: configured);

        var entity = world.SpawnEntity("human", new System.Numerics.Vector3());

        Assert.Same(configured, entity.BaseStats);
    }
}
