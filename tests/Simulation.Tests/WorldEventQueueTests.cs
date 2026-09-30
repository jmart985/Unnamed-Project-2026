using Simulation;

namespace Simulation.Tests;

public sealed class WorldEventQueueTests
{
    [Fact]
    public void ProcessesDueEventsInStableTickThenIdOrderAndPreservesFutureEvents()
    {
        var queue = new WorldEventQueue();
        queue.Enqueue(new("b", 2, "weather", "rain"));
        queue.Enqueue(new("a", 2, "weather", "wind"));
        queue.Enqueue(new("future", 4, "raid", "small"));
        var processed = new List<string>();

        queue.ProcessDue(2, worldEvent => processed.Add(worldEvent.Id));

        Assert.Equal(new[] { "a", "b" }, processed);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void EmptyQueueHasNoWork()
    {
        var queue = new WorldEventQueue();
        Assert.Empty(queue.ProcessDue(10, _ => throw new InvalidOperationException()));
    }
}
