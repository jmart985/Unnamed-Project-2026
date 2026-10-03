using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class NavigationRequestRegistryTests
{
    private sealed class Record(string id) : INavigationRequestRecord
    {
        public string Id { get; } = id;
        public NavigationOutcome Outcome { get; set; } = NavigationOutcome.InProgress;
    }

    // Mirrors the request lifecycle used by GodotNavigationAdapter without needing Godot nodes.
    private sealed class Harness
    {
        public NavigationRequestRegistry<Record> Registry { get; } = new("test-");
        public Record? Active { get; private set; }
        public Record? Last { get; private set; }

        public string Request()
        {
            if (Active is { } current)
            {
                Finish(current, NavigationOutcome.Cancelled);
            }

            var record = Registry.Add(id => new Record(id));
            Registry.PruneTerminal(null);
            Active = record;
            Last = record;
            return record.Id;
        }

        public NavigationOutcome Poll(string id)
        {
            if (!Registry.TryGet(id, out var record))
            {
                return NavigationOutcome.Removed;
            }

            Registry.RetireIfTerminal(record);
            return record.Outcome;
        }

        public bool Finish(string id, NavigationOutcome outcome)
        {
            if (!Registry.TryGet(id, out var record) || record.Outcome != NavigationOutcome.InProgress)
            {
                return false;
            }

            Finish(record, outcome);
            return true;
        }

        private void Finish(Record record, NavigationOutcome outcome)
        {
            record.Outcome = outcome;
            if (ReferenceEquals(record, Active))
            {
                Active = null;
            }

            Registry.PruneTerminal(record);
        }
    }

    [Theory]
    [InlineData(NavigationOutcome.Arrived)]
    [InlineData(NavigationOutcome.Unreachable)]
    [InlineData(NavigationOutcome.Removed)]
    [InlineData(NavigationOutcome.Cancelled)]
    [InlineData(NavigationOutcome.TimedOut)]
    public void TerminalOutcomeIsReportedOnceThenRetired(NavigationOutcome outcome)
    {
        var harness = new Harness();
        var id = harness.Request();
        Assert.True(harness.Finish(id, outcome));

        Assert.Equal(1, harness.Registry.Count);
        Assert.Equal(outcome, harness.Poll(id));
        Assert.Equal(0, harness.Registry.Count);
        Assert.Equal(outcome, harness.Last!.Outcome);
        Assert.Equal(NavigationOutcome.Removed, harness.Poll(id));
        Assert.False(harness.Finish(id, NavigationOutcome.Cancelled));
    }

    [Fact]
    public void ActiveRequestIsNeverRetired()
    {
        var harness = new Harness();
        var id = harness.Request();

        Assert.Equal(NavigationOutcome.InProgress, harness.Poll(id));
        Assert.Equal(NavigationOutcome.InProgress, harness.Poll(id));
        Assert.Equal(1, harness.Registry.Count);
    }

    [Fact]
    public void RepeatedRequestFinishCyclesStayBoundedWithUniqueIds()
    {
        var harness = new Harness();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 1000; i++)
        {
            var id = harness.Request();
            Assert.True(seen.Add(id));
            if (i % 2 == 0)
            {
                harness.Finish(id, NavigationOutcome.Arrived);
            }

            Assert.True(harness.Registry.Count <= 2);
        }

        Assert.Equal(1000, seen.Count);
    }

    [Fact]
    public void StaleIdsCannotAffectNewerRequest()
    {
        var harness = new Harness();
        var first = harness.Request();
        var second = harness.Request();

        Assert.Equal(NavigationOutcome.Removed, harness.Poll(first));
        Assert.False(harness.Finish(first, NavigationOutcome.Cancelled));
        Assert.NotEqual(first, second);
        Assert.Equal(NavigationOutcome.InProgress, harness.Poll(second));
        Assert.Same(harness.Active, harness.Last);
    }

    [Fact]
    public void LatestOutcomeSurvivesRetirement()
    {
        var harness = new Harness();
        var id = harness.Request();
        harness.Finish(id, NavigationOutcome.Unreachable);
        harness.Poll(id);

        Assert.Equal(NavigationOutcome.Unreachable, harness.Last!.Outcome);
        Assert.Null(harness.Active);
    }
}