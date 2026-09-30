using System.Numerics;
using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class MeleeCombatServiceTests
{
    [Fact]
    public void AttackAppliesClampedDamageAndReportsIncapacitation()
    {
        var world = new World(); var attacker = world.SpawnEntity("human", Vector3.Zero); var defender = world.SpawnEntity("human", Vector3.One);
        var result = MeleeCombatService.Resolve(world, new(attacker.Id, defender.Id, "Head", 999));
        Assert.Equal(MeleeAttackResult.Success, result.Result); Assert.Equal(25, result.AppliedDamage); Assert.True(result.DefenderIncapacitated);
    }

    [Fact]
    public void InvalidRequestsDoNotMutateDefender()
    {
        var world = new World(); var attacker = world.SpawnEntity("human", Vector3.Zero); var defender = world.SpawnEntity("human", Vector3.One);
        Assert.Equal(MeleeAttackResult.InvalidRequest, MeleeCombatService.Resolve(world, new(attacker.Id, defender.Id, "Head", 0)).Result);
        Assert.Equal(MeleeAttackResult.UnknownBodyPart, MeleeCombatService.Resolve(world, new(attacker.Id, defender.Id, "Tail", 1)).Result);
        Assert.Equal(25, defender.Health.Parts.Single(part => part.Name == "Head").CurrentHealth);
    }

    [Fact]
    public void OutOfRangeAttackDoesNotDealDamage()
    {
        var world = new World(); var attacker = world.SpawnEntity("human", Vector3.Zero); var defender = world.SpawnEntity("human", new Vector3(10, 0, 0));
        var result = MeleeCombatService.Resolve(world, new(attacker.Id, defender.Id, "Head", 1), 1);
        Assert.Equal(MeleeAttackResult.OutOfRange, result.Result); Assert.Equal(25, defender.Health.Parts.Single(part => part.Name == "Head").CurrentHealth);
    }
}
