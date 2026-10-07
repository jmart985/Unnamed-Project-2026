using Simulation;

namespace Simulation.Tests;

public sealed class PlayerBodyAndSwingTests
{
    [Fact]
    public void HeightAndBodyTypeChangeProportions()
    {
        var small = PlayerBodyProportions.From(PlayerCharacterProfile.Create("A", PlayerHeight.Small));
        var tall = PlayerBodyProportions.From(PlayerCharacterProfile.Create("A", PlayerHeight.Tall));
        var thick = PlayerBodyProportions.From(PlayerCharacterProfile.Create("A", bodyType: PlayerBodyType.Thick));
        var lean = PlayerBodyProportions.From(PlayerCharacterProfile.Create("A", bodyType: PlayerBodyType.Lean));

        Assert.True(small.Height < tall.Height);
        Assert.True(small.LegLength < tall.LegLength);
        Assert.True(thick.TorsoWidth > lean.TorsoWidth);
        Assert.True(thick.LimbThickness > lean.LimbThickness);
    }

    [Fact]
    public void SexShiftsShoulderHipBalanceAndChest()
    {
        var female = PlayerBodyProportions.From(PlayerCharacterProfile.Create("A", sex: PlayerSex.Female));
        var male = PlayerBodyProportions.From(PlayerCharacterProfile.Create("A", sex: PlayerSex.Male));

        Assert.True(male.ShoulderSpan > female.ShoulderSpan);
        Assert.True(female.HipSpan > male.HipSpan);
        Assert.True(female.ChestSize > male.ChestSize);
    }

    [Fact]
    public void BodySlidersChangeDerivedProportionsDeterministically()
    {
        var light = PlayerBodyProportions.From(PlayerCharacterProfile.Create("A", weight: 0, muscle: 0));
        var strong = PlayerBodyProportions.From(PlayerCharacterProfile.Create("A", weight: 1, muscle: 1));

        Assert.True(strong.TorsoWidth > light.TorsoWidth);
        Assert.True(strong.TorsoDepth > light.TorsoDepth);
        Assert.True(strong.LimbThickness > light.LimbThickness);
        Assert.Equal(strong, PlayerBodyProportions.From(PlayerCharacterProfile.Create("A", weight: 1, muscle: 1)));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void BodySlidersRejectValuesOutsideBounds(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerCharacterProfile.Create("A", weight: value));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerCharacterProfile.Create("A", muscle: value));
    }

    [Fact]
    public void BodyPartAdjustmentsAreBoundedAndMirrorSymmetricParts()
    {
        var left = PlayerBodyPartAdjustment.Create("LeftShoulder", 0.2, -0.1, 0.05);
        var right = PlayerBodyCustomization.Mirror(left);

        Assert.Equal("RightShoulder", right.PartName);
        Assert.Equal(-0.2, right.X);
        Assert.Equal(-0.1, right.Y);
        Assert.Equal(0.05, right.Z);
        Assert.Equal("Head", PlayerBodyCustomization.Reset("Head").PartName);
    }

    [Fact]
    public void BodyPartAdjustmentsRejectUnsupportedOrUnboundedValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerBodyPartAdjustment.Create("Head", 0.26));
        Assert.Throws<ArgumentException>(() => PlayerBodyCustomization.Reset("Tail"));
    }

    [Fact]
    public void InvalidProfileIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            PlayerBodyProportions.From(new PlayerCharacterProfile("", PlayerHeight.Medium)));
    }

    [Fact]
    public void SwingProgressesThroughPhasesAndReportsStrikeOnce()
    {
        var swing = new AttackSwing(0.1f, 0.1f, 0.1f);
        Assert.Equal(AttackSwingPhase.Idle, swing.Phase);
        Assert.True(swing.Begin());
        Assert.False(swing.Begin());

        var strikes = 0;
        var seen = new HashSet<AttackSwingPhase>();
        for (var i = 0; i < 40 && swing.IsActive; i++)
        {
            if (swing.Advance(0.01f)) strikes++;
            seen.Add(swing.Phase);
        }

        Assert.Equal(1, strikes);
        Assert.Contains(AttackSwingPhase.WindUp, seen);
        Assert.Contains(AttackSwingPhase.Strike, seen);
        Assert.Contains(AttackSwingPhase.Recovery, seen);
        Assert.False(swing.IsActive);
        Assert.Equal(0f, swing.Pose);
        Assert.True(swing.Begin());
    }

    [Fact]
    public void LargeStepStillReportsStrikeAndFinishes()
    {
        var swing = new AttackSwing();
        swing.Begin();
        Assert.True(swing.Advance(5f));
        Assert.False(swing.IsActive);
    }

    [Fact]
    public void TotalDurationFactoryPreservesPhaseRatios()
    {
        var swing = AttackSwing.ForTotalDuration(0.22f);

        Assert.Equal(0.22f, swing.TotalSeconds, 5);
        Assert.Equal(0.22f * (0.12f / 0.38f), swing.WindUpSeconds, 5);
        Assert.Equal(0.22f * (0.08f / 0.38f), swing.StrikeSeconds, 5);
    }

    [Theory]
    [InlineData(0.5f, true, "Head")]
    [InlineData(0f, false, "Torso")]
    [InlineData(-0.5f, true, "LeftLeg")]
    [InlineData(-0.5f, false, "RightLeg")]
    public void PitchSelectsAimedBodyPart(float pitch, bool left, string expected)
    {
        Assert.Equal(expected, CombatAim.PartForPitch(pitch, left));
    }
}
