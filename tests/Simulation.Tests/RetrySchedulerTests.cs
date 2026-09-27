using Simulation;

namespace Simulation.Tests;

public sealed class RetrySchedulerTests
{
    [Fact]
    public void KeyIsNotWaitingUntilARetryHasBeenScheduled()
    {
        var scheduler = new RetryScheduler();

        Assert.False(scheduler.IsWaiting("entity-1", 0));
    }

    [Fact]
    public void ScheduledRetryWaitsForTheFullDelayThenClearsItself()
    {
        var scheduler = new RetryScheduler(delayTicks: 10);

        scheduler.ScheduleRetry("entity-1", simulationTick: 0);

        Assert.True(scheduler.IsWaiting("entity-1", 9));
        Assert.False(scheduler.IsWaiting("entity-1", 10));
        // The retry window is consumed by the delay elapsing, not by repeated polling.
        Assert.False(scheduler.IsWaiting("entity-1", 10));
    }

    [Fact]
    public void ClearRemovesAPendingRetryImmediately()
    {
        var scheduler = new RetryScheduler(delayTicks: 10);
        scheduler.ScheduleRetry("entity-1", simulationTick: 0);

        scheduler.Clear("entity-1");

        Assert.False(scheduler.IsWaiting("entity-1", 1));
    }

    [Fact]
    public void ResetClearsEveryScheduledKey()
    {
        var scheduler = new RetryScheduler(delayTicks: 10);
        scheduler.ScheduleRetry("entity-1", simulationTick: 0);
        scheduler.ScheduleRetry("entity-2", simulationTick: 0);

        scheduler.Reset();

        Assert.False(scheduler.IsWaiting("entity-1", 1));
        Assert.False(scheduler.IsWaiting("entity-2", 1));
    }

    [Fact]
    public void KeysAreIndependentOfEachOther()
    {
        var scheduler = new RetryScheduler(delayTicks: 10);
        scheduler.ScheduleRetry("entity-1", simulationTick: 0);

        Assert.True(scheduler.IsWaiting("entity-1", 5));
        Assert.False(scheduler.IsWaiting("entity-2", 5));
    }

    [Fact]
    public void InvalidInputsAreRejected()
    {
        var scheduler = new RetryScheduler();

        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryScheduler(delayTicks: 0));
        Assert.Throws<ArgumentException>(() => scheduler.IsWaiting(" ", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.IsWaiting("entity-1", -1));
        Assert.Throws<ArgumentException>(() => scheduler.ScheduleRetry(" ", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleRetry("entity-1", -1));
    }
}
