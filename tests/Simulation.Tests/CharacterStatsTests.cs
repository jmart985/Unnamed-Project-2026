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
        Assert.Equal(CharacterStats.DefaultStrength, stats.Strength);
        Assert.Equal(CharacterStats.DefaultEndurance, stats.Endurance);
    }

    [Fact]
    public void StrengthScalesCapacityAndEnduranceReducesLoadFatigue()
    {
        var strong = new CharacterStats(strength: 2, endurance: 2);
        var weak = new CharacterStats(strength: 1, endurance: 1);

        Assert.Equal(20, strong.EffectiveCarryCapacity);
        Assert.Equal(20, strong.EffectiveCarryCapacityKg);
        Assert.Equal(0.0005, LoadFatigue.PerSecond(10, weak), precision: 8);
        Assert.Equal(0.000075, LoadFatigue.PerSecond(1.5, weak), precision: 8);
        Assert.True(LoadFatigue.PerSecond(10, strong) < LoadFatigue.PerSecond(10, weak));
        Assert.True(LoadFatigue.PerSecond(30, weak) > LoadFatigue.PerSecond(10, weak));
    }

    [Fact]
    public void CarriedWeightUsesItemDefinitionsAndDrivesFatigue()
    {
        var world = new World();
        var loaded = world.SpawnEntity("human", System.Numerics.Vector3.Zero);
        var unloaded = world.SpawnEntity("human", System.Numerics.Vector3.One);
        loaded.TryAddCarried(ItemIds.Wood, 999);
        loaded.TryAddCarried(ItemIds.Axe, 1);
        unloaded.TryAddCarried(ItemIds.Food, 999);

        Assert.Equal(1.5, loaded.CarriedWeightKg);
        Assert.Equal(0, unloaded.CarriedWeightKg);
        loaded.AdvanceNeeds(10);
        unloaded.AdvanceNeeds(10);

        Assert.True(loaded.Needs.Fatigue > unloaded.Needs.Fatigue);
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
