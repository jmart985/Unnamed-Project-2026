using Simulation;

namespace Simulation.Tests;

public sealed class ActionTests
{
    [Fact]
    public void SelectChoosesHighestPriorityAndUsesStableIdTieBreak()
    {
        var selected = ActionLifecycle.Select(
            new[]
            {
                new ActionCandidate("z-haul", ActionKind.Haul),
                new ActionCandidate("b-eat", ActionKind.Eat),
                new ActionCandidate("a-haul", ActionKind.Haul)
            });

        Assert.NotNull(selected);
        Assert.Equal("b-eat", selected.Id);

        var tie = ActionLifecycle.Select(
            new[]
            {
                new ActionCandidate("z-haul", ActionKind.Haul),
                new ActionCandidate("a-haul", ActionKind.Haul)
            });

        Assert.NotNull(tie);
        Assert.Equal("a-haul", tie.Id);
    }

    [Fact]
    public void OneEntityHasOneActionAndEatingPreemptsWithoutChangingInventory()
    {
        var world = new World();
        var entity = world.SpawnEntity("human", System.Numerics.Vector3.Zero);
        var cleanupCount = 0;

        Assert.Equal(
            ActionStartResult.Started,
            entity.Actions.TryStart(new ActionCandidate("haul-1", ActionKind.Haul), () => cleanupCount++));
        Assert.Equal(ActionStartResult.Preempted, entity.Actions.TryStart(new ActionCandidate("eat-1", ActionKind.Eat)));
        Assert.Equal("eat-1", entity.Actions.Current?.Id);
        Assert.Empty(entity.Inventory);
        Assert.Equal(1, cleanupCount);
    }

    [Fact]
    public void CompletionAndCancellationRunCleanupExactlyOnce()
    {
        var lifecycle = new ActionLifecycle();
        var cleanupCount = 0;

        lifecycle.TryStart(new ActionCandidate("eat-1", ActionKind.Eat), () => cleanupCount++);
        Assert.True(lifecycle.Complete());
        Assert.False(lifecycle.Complete());
        Assert.Equal(1, cleanupCount);

        lifecycle.TryStart(new ActionCandidate("haul-1", ActionKind.Haul), () => cleanupCount++);
        Assert.True(lifecycle.Cancel());
        Assert.False(lifecycle.Cancel());
        Assert.Equal(2, cleanupCount);
    }

    [Fact]
    public void ProgressCompletesAnActionAtOne()
    {
        var lifecycle = new ActionLifecycle();

        lifecycle.TryStart(new ActionCandidate("haul-1", ActionKind.Haul));

        Assert.True(lifecycle.AdvanceProgress(0.4));
        Assert.Equal(0.4, lifecycle.Current?.Progress);
        Assert.True(lifecycle.AdvanceProgress(0.6));
        Assert.Null(lifecycle.Current);
    }

    [Fact]
    public void LowerPriorityActionCannotReplaceActiveAction()
    {
        var lifecycle = new ActionLifecycle();

        lifecycle.TryStart(new ActionCandidate("eat-1", ActionKind.Eat));

        Assert.Equal(
            ActionStartResult.Rejected,
            lifecycle.TryStart(new ActionCandidate("haul-1", ActionKind.Haul)));
        Assert.Equal("eat-1", lifecycle.Current?.Id);
    }

    [Fact]
    public void AttentionActionsCanPauseMovementOrdersButMovementCannotReplaceThem()
    {
        var lifecycle = new ActionLifecycle();
        Assert.Equal(ActionStartResult.Started,
            lifecycle.TryStart(new ActionCandidate("squad-move", ActionKind.SquadMove)));
        Assert.Equal(ActionStartResult.Rejected,
            lifecycle.TryStart(new ActionCandidate("locomotion-compatible", ActionKind.Haul)));
        Assert.Equal("squad-move", lifecycle.Current?.Id);

        Assert.Equal(ActionStartResult.Preempted,
            lifecycle.TryStart(new ActionCandidate("focused-action", ActionKind.Haul, interruptsMovementOrders: true)));
        Assert.True(lifecycle.Current?.InterruptsMovementOrders);
        Assert.Equal(ActionStartResult.Rejected,
            lifecycle.TryStart(new ActionCandidate("replacement-move", ActionKind.SquadMove)));

        Assert.Equal(ActionStartResult.Preempted,
            lifecycle.TryStart(new ActionCandidate("urgent-flee", ActionKind.Flee)));
        Assert.True(lifecycle.Current?.InterruptsMovementOrders);
    }
}
