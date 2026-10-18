using LiveTracker.Application.Ingestion;
using Xunit;

namespace LiveTracker.Tests;

public class PositionChangeFilterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(3);
    private static DateTime Now => TestData.Now;

    private static FilterResult Run(PositionChangeFilter filter, params LiveTracker.Domain.VehiclePosition[] items) =>
        filter.Select(items, Now, Window);

    [Fact]
    public void NewVehicle_IsFresh()
    {
        var result = Run(new PositionChangeFilter(), TestData.Position("a", Now));

        Assert.Single(result.Fresh);
    }

    [Fact]
    public void SameTimestamp_AfterMarkSeen_IsUnchanged()
    {
        var filter = new PositionChangeFilter();
        var p = TestData.Position("a", Now);
        filter.MarkSeen(Run(filter, p).Fresh);

        var result = Run(filter, p);

        Assert.Empty(result.Fresh);
        Assert.Equal(1, result.Unchanged);
    }

    [Fact]
    public void OlderTimestamp_IsOutOfOrder()
    {
        var filter = new PositionChangeFilter();
        filter.MarkSeen(new[] { TestData.Position("a", Now) });

        var result = Run(filter, TestData.Position("a", Now.AddSeconds(-30)));

        Assert.Empty(result.Fresh);
        Assert.Equal(1, result.OutOfOrder);
    }

    [Fact]
    public void NewerTimestamp_IsFresh()
    {
        var filter = new PositionChangeFilter();
        filter.MarkSeen(new[] { TestData.Position("a", Now) });

        var result = Run(filter, TestData.Position("a", Now.AddSeconds(3)));

        Assert.Single(result.Fresh);
    }

    [Fact]
    public void OlderThanWindow_IsTooOld()
    {
        var result = Run(new PositionChangeFilter(), TestData.Position("a", Now - Window - TimeSpan.FromMinutes(1)));

        Assert.Empty(result.Fresh);
        Assert.Equal(1, result.TooOld);
    }

    [Fact]
    public void FarFuture_IsRejected_ButSmallSkewIsAccepted()
    {
        var far = Run(new PositionChangeFilter(), TestData.Position("a", Now.AddMinutes(10)));
        var near = Run(new PositionChangeFilter(), TestData.Position("b", Now.AddMinutes(4)));

        Assert.Equal(1, far.FutureDated);
        Assert.Single(near.Fresh);
    }

    [Fact]
    public void Select_DoesNotRemember_UntilMarkSeenIsCalled()
    {
        // Seçmek hatırlamak değildir: kuyruğa alınınca MarkSeen çağrılmalı.
        var filter = new PositionChangeFilter();
        var p = TestData.Position("a", Now);

        Assert.Single(Run(filter, p).Fresh);
        Assert.Single(Run(filter, p).Fresh);
    }

    [Fact]
    public void Prune_ForgetsEntriesOlderThanCutoff()
    {
        var filter = new PositionChangeFilter();
        filter.MarkSeen(new[] { TestData.Position("a", Now.AddHours(-2)) });

        filter.Prune(Now.AddHours(-1));

        Assert.Equal(0, filter.TrackedCount);
    }
}