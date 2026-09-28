using Simulation;

namespace Simulation.Tests;

public sealed class SimulationClockTests
{
    [Fact]
    public void EquivalentFrameChunksProduceEquivalentTicks()
    {
        var oneLargeFrame = new SimulationClock();
        var manySmallFrames = new SimulationClock();

        oneLargeFrame.Advance(0.5);
        for (var i = 0; i < 5; i++)
        {
            manySmallFrames.Advance(0.1);
        }

        Assert.Equal(5, oneLargeFrame.SimulationTime / SimulationClock.TickIntervalSeconds, precision: 6);
        Assert.Equal(oneLargeFrame.SimulationTime, manySmallFrames.SimulationTime, precision: 6);
    }

    [Fact]
    public void ThreeTenthsOfASecondProducesThreeTicksRegardlessOfFrameChunking()
    {
        var oneLargeFrame = new SimulationClock();
        var manySmallFrames = new SimulationClock();
        var variedFrameChunks = new SimulationClock();

        var largeFrameTicks = oneLargeFrame.Advance(0.3);
        var smallFrameTicks = Enumerable.Range(0, 3).Sum(_ => manySmallFrames.Advance(0.1));
        var variedChunkTicks = Enumerable.Range(0, 10).Sum(_ => variedFrameChunks.Advance(0.03));

        Assert.Equal(3, largeFrameTicks);
        Assert.Equal(3, smallFrameTicks);
        Assert.Equal(3, variedChunkTicks);
        Assert.Equal(oneLargeFrame.SimulationTime, manySmallFrames.SimulationTime, precision: 6);
        Assert.Equal(oneLargeFrame.SimulationTime, variedFrameChunks.SimulationTime, precision: 6);
    }

    [Fact]
    public void SupportedFrameRangeProducesEquivalentTicksRegardlessOfChunking()
    {
        var oneLargeFrame = new SimulationClock();
        var manySmallFrames = new SimulationClock();
        var unevenChunks = new SimulationClock();

        var largeFrameTicks = oneLargeFrame.Advance(0.8);
        var smallFrameTicks = Enumerable.Range(0, 8).Sum(_ => manySmallFrames.Advance(0.1));
        var unevenChunkTicks = unevenChunks.Advance(0.35)
            + unevenChunks.Advance(0.25)
            + unevenChunks.Advance(0.2);

        Assert.Equal(SimulationClock.MaximumTicksPerAdvance, largeFrameTicks);
        Assert.Equal(largeFrameTicks, smallFrameTicks);
        Assert.Equal(largeFrameTicks, unevenChunkTicks);
        Assert.Equal(oneLargeFrame.SimulationTime, manySmallFrames.SimulationTime, precision: 6);
        Assert.Equal(oneLargeFrame.SimulationTime, unevenChunks.SimulationTime, precision: 6);
    }

    [Fact]
    public void PauseFreezesTimeAndDoesNotCatchUp()
    {
        var clock = new SimulationClock();
        clock.SetPaused(true);

        Assert.Equal(0, clock.Advance(10));
        Assert.Equal(0, clock.SimulationTime);

        clock.SetPaused(false);
        Assert.Equal(1, clock.Advance(0.1));
        Assert.Equal(0.1, clock.SimulationTime, precision: 6);
    }

    [Fact]
    public void PausePreservesPrePausePartialTickButExcludesPausedWallTime()
    {
        var clock = new SimulationClock();

        Assert.Equal(0, clock.Advance(0.05));

        clock.SetPaused(true);
        Assert.Equal(0, clock.Advance(10));

        clock.SetPaused(false);

        Assert.Equal(1, clock.Advance(0.05));
        Assert.Equal(0.1, clock.SimulationTime, precision: 6);
    }

    [Fact]
    public void EachTickInvokesDomainUpdateCallback()
    {
        var clock = new SimulationClock();
        var tickDurations = new List<double>();

        var ticks = clock.Advance(0.5, tickDurations.Add);

        Assert.Equal(5, ticks);
        Assert.Equal(5, tickDurations.Count);
        Assert.All(tickDurations, duration => Assert.Equal(0.1, duration, precision: 6));
    }

    [Fact]
    public void FourXAdvancesFourTimesAsManyTicks()
    {
        var clock = new SimulationClock();
        clock.SetSpeed(SimulationSpeed.FourX);

        Assert.Equal(4, clock.Advance(0.1));
        Assert.Equal(0.4, clock.SimulationTime, precision: 6);
    }

    [Fact]
    public void ChangingSpeedAfterPauseDoesNotAdvanceUntilResumed()
    {
        var clock = new SimulationClock();
        clock.SetPaused(true);
        clock.SetSpeed(SimulationSpeed.FourX);

        Assert.Equal(0, clock.Advance(1));
        Assert.Equal(0, clock.SimulationTime);

        clock.SetPaused(false);

        Assert.Equal(4, clock.Advance(0.1));
        Assert.Equal(0.4, clock.SimulationTime, precision: 6);
    }

    [Fact]
    public void ResetRestoresInitialScenarioClockState()
    {
        var clock = new SimulationClock();
        clock.SetSpeed(SimulationSpeed.FourX);
        clock.Advance(0.5);
        clock.Advance(0.05);
        clock.SetPaused(true);

        clock.Reset();

        Assert.False(clock.IsPaused);
        Assert.Equal(SimulationSpeed.OneX, clock.Speed);
        Assert.Equal(0, clock.SimulationTime);
        Assert.Equal(0, clock.Advance(0.05));
        Assert.Equal(1, clock.Advance(0.05));
    }

    [Fact]
    public void ExcessiveFrameDeltaUsesBoundedCatchUp()
    {
        var clock = new SimulationClock();

        var ticks = clock.Advance(10);

        Assert.Equal(SimulationClock.MaximumTicksPerAdvance, ticks);
        Assert.Equal(0.8, clock.SimulationTime, precision: 6);
    }

    [Fact]
    public void OverflowBeyondTheCatchUpCapIsDiscarded()
    {
        var clock = new SimulationClock();

        Assert.Equal(SimulationClock.MaximumTicksPerAdvance, clock.Advance(1.0));

        var nextFrameTicks = clock.Advance(0.1);

        Assert.Equal(1, nextFrameTicks);
        Assert.Equal(0.9, clock.SimulationTime, precision: 6);
    }

    [Fact]
    public void UnsupportedSpeedsAreRejected()
    {
        var clock = new SimulationClock();

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetSpeed((SimulationSpeed)2));

        Assert.Equal("speed", error.ParamName);
        Assert.Equal(SimulationSpeed.OneX, clock.Speed);
    }
}
