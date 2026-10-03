using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class WildCreatureDecisionPolicyTests
{
    [Fact]
    public void ThreatTakesPriorityOverFoodAndOtherGoals()
    {
        var entity = CreateWolf(satiety: 20);
        var food = Target("food", 1);
        var threat = Target("threat", 2);
        var pursuitTarget = Target("prey", 3);
        var returnTarget = Target("home", 4);
        var context = new WildCreatureDecisionContext(food, threat, pursuitTarget, true, returnTarget);

        var result = WildCreatureDecisionPolicy.Decide(entity, context);

        Assert.Equal(WildCreatureDecision.Flee, result.Decision);
        Assert.Same(threat, result.Target);
    }

    [Fact]
    public void HungryCreatureSeeksCompatibleFoodBeforeReturningOrPursuing()
    {
        var entity = CreateWolf(satiety: 20);
        var food = Target("meat", 1);
        var context = new WildCreatureDecisionContext(
            compatibleFood: food,
            pursuitTarget: Target("prey", 3),
            isOutsideArea: true,
            returnAreaTarget: Target("home", 4));

        var result = WildCreatureDecisionPolicy.Decide(entity, context);

        Assert.Equal(WildCreatureDecision.SeekFood, result.Decision);
        Assert.Same(food, result.Target);
    }

    [Fact]
    public void CreatureOutsideAreaReturnsBeforePursuing()
    {
        var entity = CreateWolf(satiety: 70);
        var home = Target("home", 4);
        var context = new WildCreatureDecisionContext(
            pursuitTarget: Target("prey", 3),
            isOutsideArea: true,
            returnAreaTarget: home);

        var result = WildCreatureDecisionPolicy.Decide(entity, context);

        Assert.Equal(WildCreatureDecision.ReturnToArea, result.Decision);
        Assert.Same(home, result.Target);
    }

    [Fact]
    public void CreaturePursuesAvailableTargetOrIdlesWhenThereIsNoStimulus()
    {
        var entity = CreateWolf(satiety: 70);
        var pursuitTarget = Target("prey", 3);

        var pursuit = WildCreatureDecisionPolicy.Decide(
            entity,
            new WildCreatureDecisionContext(pursuitTarget: pursuitTarget));
        var idle = WildCreatureDecisionPolicy.Decide(entity, new WildCreatureDecisionContext());

        Assert.Equal(WildCreatureDecision.Pursue, pursuit.Decision);
        Assert.Same(pursuitTarget, pursuit.Target);
        Assert.Equal(new WildCreatureDecisionResult(WildCreatureDecision.Idle, null), idle);
    }

    [Fact]
    public void HungerWithoutAvailableFoodDoesNotInventFoodTarget()
    {
        var entity = CreateWolf(satiety: 20);
        var pursuitTarget = Target("prey", 3);

        var result = WildCreatureDecisionPolicy.Decide(
            entity,
            new WildCreatureDecisionContext(pursuitTarget: pursuitTarget));

        Assert.Equal(WildCreatureDecision.Pursue, result.Decision);
        Assert.Same(pursuitTarget, result.Target);
    }

    [Fact]
    public void RepeatedDecisionsAreDeterministicAndDoNotMutateEntity()
    {
        var entity = CreateWolf(satiety: 20);
        var context = new WildCreatureDecisionContext(
            compatibleFood: Target("meat", 1),
            pursuitTarget: Target("prey", 3));
        var initialPosition = entity.Position;
        var initialSatiety = entity.Satiety;
        var initialActivity = entity.Activity;

        var first = WildCreatureDecisionPolicy.Decide(entity, context);
        var repeated = WildCreatureDecisionPolicy.Decide(entity, context);

        Assert.Equal(first, repeated);
        Assert.Equal(initialPosition, entity.Position);
        Assert.Equal(initialSatiety, entity.Satiety);
        Assert.Equal(initialActivity, entity.Activity);
        Assert.Null(entity.Actions.Current);
    }

    [Fact]
    public void InvalidTargetsAndMissingReturnDestinationAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new WildCreatureDecisionTarget(" ", Vector3.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WildCreatureDecisionTarget("food", new Vector3(float.NaN, 0, 0)));
        Assert.Throws<ArgumentException>(() =>
            new WildCreatureDecisionContext(isOutsideArea: true));
        Assert.Throws<ArgumentException>(() =>
            new WildCreatureDecisionResult(WildCreatureDecision.SeekFood, null));
        Assert.Throws<ArgumentException>(() =>
            new WildCreatureDecisionResult(WildCreatureDecision.Idle, Target("food", 1)));
    }

    private static EntityRecord CreateWolf(double satiety)
    {
        return new World().SpawnEntity("wolf", Vector3.Zero, satiety);
    }

    private static WildCreatureDecisionTarget Target(string id, float x)
    {
        return new WildCreatureDecisionTarget(id, new Vector3(x, 0, 0));
    }
}
