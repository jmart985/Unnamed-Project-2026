using System.Numerics;
using Simulation;

namespace Simulation.Tests;

public sealed class NavigationTests
{
    [Fact]
    public void RequestUsesDocumentedArrivalTolerance()
    {
        var request = new NavigationRequest(Vector3.One);

        Assert.Equal(0.1, request.ArrivalTolerance, precision: 6);
    }

    [Fact]
    public void ReachableRequestReportsArrival()
    {
        var navigator = new FakeNavigationAdapter();
        var requestId = navigator.Request(new NavigationRequest(Vector3.One));

        Assert.Equal(NavigationOutcome.InProgress, navigator.Poll(requestId, 0.1));
        Assert.True(navigator.CompleteAsArrived(requestId));
        Assert.Equal(NavigationOutcome.Arrived, navigator.Poll(requestId, 0));
    }

    [Fact]
    public void UnreachableAndRemovedTargetsReportControlledFailures()
    {
        var navigator = new FakeNavigationAdapter();
        var unreachable = navigator.Request(new NavigationRequest(Vector3.One));
        var removed = navigator.Request(new NavigationRequest(Vector3.UnitX));

        Assert.True(navigator.MarkUnreachable(unreachable));
        Assert.True(navigator.RemoveTarget(removed));
        Assert.Equal(NavigationOutcome.Unreachable, navigator.Poll(unreachable, 0));
        Assert.Equal(NavigationOutcome.Removed, navigator.Poll(removed, 0));
    }

    [Fact]
    public void CancellationStopsAnActiveRequest()
    {
        var navigator = new FakeNavigationAdapter();
        var requestId = navigator.Request(new NavigationRequest(Vector3.One));

        Assert.True(navigator.Cancel(requestId));
        Assert.Equal(NavigationOutcome.Cancelled, navigator.Poll(requestId, 0));
        Assert.False(navigator.Cancel(requestId));
    }

    [Fact]
    public void RequestsTimeOutAfterTheirConfiguredDuration()
    {
        var navigator = new FakeNavigationAdapter();
        var requestId = navigator.Request(new NavigationRequest(Vector3.One, timeoutSeconds: 1));

        Assert.Equal(NavigationOutcome.InProgress, navigator.Poll(requestId, 0.9));
        Assert.Equal(NavigationOutcome.TimedOut, navigator.Poll(requestId, 0.1));
    }

    [Fact]
    public void InvalidRequestsAndElapsedTimeAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationRequest(new Vector3(float.NaN, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationRequest(Vector3.One, arrivalTolerance: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationRequest(Vector3.One, timeoutSeconds: -1));

        var navigator = new FakeNavigationAdapter();
        var requestId = navigator.Request(new NavigationRequest(Vector3.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => navigator.Poll(requestId, -1));
        Assert.Equal(NavigationOutcome.Removed, navigator.Poll("missing", 0));
    }
}
