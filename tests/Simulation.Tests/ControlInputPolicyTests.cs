using Simulation;

namespace Simulation.Tests;

public sealed class ControlInputPolicyTests
{
    [Theory]
    [InlineData(0.10, HoldTapSignal.Tap)]
    [InlineData(0.299, HoldTapSignal.Tap)]
    [InlineData(0.30, HoldTapSignal.HoldEnded)]
    [InlineData(0.31, HoldTapSignal.HoldEnded)]
    [InlineData(2.0, HoldTapSignal.HoldEnded)]
    public void ReleaseCannotEmitBothDodgeAndCrouch(double duration, HoldTapSignal expected)
    {
        var gesture = new HoldTapGesture();
        gesture.Press(0);
        Assert.Equal(expected, gesture.Release(duration));
        Assert.False(gesture.IsActive);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void HoldThresholdDoesNotDependOnFrameRate(int framesPerSecond)
    {
        var gesture = new HoldTapGesture();
        gesture.Press(0);
        var starts = 0;
        for (var i = 1; i <= framesPerSecond; i++)
            if (gesture.Advance((double)i / framesPerSecond) == HoldTapSignal.HoldStarted) starts++;
        Assert.Equal(1, starts);
        Assert.True(gesture.IsActive);
        Assert.Equal(HoldTapSignal.HoldEnded, gesture.Release(1));
        Assert.False(gesture.IsActive);
    }

    [Fact]
    public void ToggleHoldsRetainStanceAndShortTapsRemainDodge()
    {
        var gesture = new HoldTapGesture();
        gesture.Press(0, toggle: true);
        gesture.Advance(0.3);
        Assert.Equal(HoldTapSignal.None, gesture.Release(0.4));
        Assert.True(gesture.IsActive);
        gesture.Press(1, toggle: true);
        Assert.Equal(HoldTapSignal.Tap, gesture.Release(1.1));
        Assert.True(gesture.IsActive);
        gesture.Press(2, toggle: true);
        gesture.Advance(2.3);
        gesture.Release(2.4);
        Assert.False(gesture.IsActive);
    }

    [Fact]
    public void CancellationAndRepeatedPressesNeverCreateATap()
    {
        var gesture = new HoldTapGesture();
        gesture.Press(0);
        gesture.Press(0.2);
        Assert.Equal(HoldTapSignal.HoldStarted, gesture.Advance(0.3));
        gesture.Cancel();
        Assert.False(gesture.IsActive);
        Assert.Equal(HoldTapSignal.None, gesture.Release(0.4));
        Assert.Throws<ArgumentOutOfRangeException>(() => gesture.Advance(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => gesture.Advance(0.1));
    }

    [Fact]
    public void HigherContextsOwnInputAndNeverFallThroughToGameplay()
    {
        Assert.Equal(ControlContext.Rebinding, ControlInputPolicy.Resolve(true, true, true, true, true));
        Assert.Equal(ControlContext.TextEntry, ControlInputPolicy.Resolve(false, true, true, true, true));
        Assert.Equal(ControlContext.Modal, ControlInputPolicy.Resolve(false, false, true, true, true));
        Assert.Equal(ControlContext.Wheel, ControlInputPolicy.Resolve(false, false, false, true, true));
        Assert.False(ControlInputPolicy.Accepts(ControlContext.Modal, ControlContext.Play));
        Assert.True(ControlInputPolicy.Accepts(ControlContext.Build, ControlContext.Play));
    }

    [Fact]
    public void MenuOwnersCannotReleaseEachOtherOrManualPause()
    {
        var pause = new ControlPauseState();
        pause.SetManual(true);
        pause.SetOwner("inventory", true);
        pause.SetOwner("inventory", false);
        Assert.True(pause.IsPaused);
        pause.SetManual(false);
        pause.SetOwner("workstation", true);
        pause.SetOwner("settings", true);
        pause.SetOwner("settings", false);
        Assert.True(pause.IsPaused);
        pause.SetOwner("workstation", false);
        Assert.False(pause.IsPaused);
    }
}

