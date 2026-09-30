using Simulation;
using Xunit;

namespace Simulation.Tests;

public sealed class SettlementWorkOrderTests
{
    [Fact]
    public void QueueReturnsPriorityOrderAndTracksClaimCompletion()
    {
        var queue = new SettlementWorkOrderQueue();
        var low = new SettlementWorkOrder("a", "haul", 1);
        var high = new SettlementWorkOrder("b", "craft", 5);
        Assert.True(queue.TryEnqueue(low)); Assert.True(queue.TryEnqueue(high));
        Assert.Same(high, queue.NextAvailable());
        Assert.True(high.TryClaim("worker")); Assert.False(high.TryClaim("other"));
        Assert.True(high.TryComplete("worker")); Assert.Same(low, queue.NextAvailable());
    }

    [Fact]
    public void DuplicateAndInvalidTransitionsAreRejected()
    {
        var queue = new SettlementWorkOrderQueue();
        Assert.True(queue.TryEnqueue(new SettlementWorkOrder("a", "haul", 1)));
        Assert.False(queue.TryEnqueue(new SettlementWorkOrder("a", "craft", 2)));
        Assert.False(queue.Orders.Single().TryComplete("worker"));
    }
}
