using LiveTracker.Application.Options;
using LiveTracker.Application.Streaming;
using Microsoft.Extensions.Options;
using LiveTracker.Domain;
using Xunit;

namespace LiveTracker.Tests;

public class PositionHubTests
{
    private static PositionHub Create(int replayBatches = 10, int clientQueue = 2) =>
        new(Options.Create(new StreamingOptions { ReplayBatches = replayBatches, ClientQueueBatches = clientQueue }),
            TimeProvider.System);

    private static IReadOnlyList<LiveTracker.Domain.VehiclePosition> One(string id) =>
        new[] { TestData.Position(id, TestData.Now) };

    // Belirli bir hatta ait örnek konum
    private static VehiclePosition OnRoute(string id, string route)
    {
        var p = TestData.Position(id, TestData.Now);
        p.RouteId = route;
        return p;
    }

    [Fact]
    public void Subscribe_WithRouteId_SnapshotContainsOnlyThatRoute()
    {
        var hub = Create();
        hub.Publish(new[] { OnRoute("a", "r1"), OnRoute("b", "r2") });

        using var sub = hub.Subscribe(null, "r1");

        var message = Assert.Single(sub.Initial);
        Assert.Equal("snapshot", message.Type);
        Assert.Equal("a", Assert.Single(message.Positions).EntityId);
    }

    [Fact]
    public void Publish_SendsRouteSubscriberOnlyItsOwnPositions()
    {
        var hub = Create();
        using var sub = hub.Subscribe(null, "r1");

        hub.Publish(new[] { OnRoute("a", "r1"), OnRoute("b", "r2") });

        Assert.True(sub.Reader.TryRead(out var message));
        Assert.Equal("a", Assert.Single(message.Positions).EntityId);
    }

    [Fact]
    public void Publish_WithNoMatchingRoute_SendsNothing()
    {
        var hub = Create();
        using var sub = hub.Subscribe(null, "r1");

        hub.Publish(new[] { OnRoute("b", "r2") });

        Assert.False(sub.Reader.TryRead(out _));
    }

    [Fact]
    public void Replay_WithRouteId_FiltersMissedBatches()
    {
        var hub = Create();
        hub.Publish(new[] { OnRoute("a", "r1") });
        var seqAfterFirst = hub.LastSeq;
        hub.Publish(new[] { OnRoute("b", "r2") });
        hub.Publish(new[] { OnRoute("c", "r1") });

        using var sub = hub.Subscribe(seqAfterFirst, "r1");

        var message = Assert.Single(sub.Initial);
        Assert.Equal("update", message.Type);
        Assert.Equal("c", Assert.Single(message.Positions).EntityId);
    }

    [Fact]
    public void Subscribe_WithoutLastSeq_GetsSnapshotOfLatestPositions()
    {
        var hub = Create();
        hub.Publish(One("a"));
        hub.Publish(One("b"));

        using var sub = hub.Subscribe(null);

        var message = Assert.Single(sub.Initial);
        Assert.Equal("snapshot", message.Type);
        Assert.Equal(2, message.Positions.Count);
    }

    [Fact]
    public void Subscribe_WithRecentLastSeq_ReplaysOnlyMissedBatches()
    {
        var hub = Create();
        hub.Publish(One("a"));
        var seqAfterFirst = hub.LastSeq;
        hub.Publish(One("b"));
        hub.Publish(One("c"));

        using var sub = hub.Subscribe(seqAfterFirst);

        Assert.Equal(2, sub.Initial.Count);
        Assert.All(sub.Initial, m => Assert.Equal("update", m.Type));
        Assert.Equal(seqAfterFirst + 1, sub.Initial[0].Seq);
    }

    [Fact]
    public void Subscribe_WithTooOldLastSeq_FallsBackToSnapshot()
    {
        var hub = Create(replayBatches: 2);
        for (var i = 0; i < 5; i++) hub.Publish(One("v" + i));

        using var sub = hub.Subscribe(hub.LastSeq - 4);

        Assert.Equal("snapshot", Assert.Single(sub.Initial).Type);
    }

    [Fact]
    public async Task SlowClient_IsDisconnected_WithoutBlockingPublisher()
    {
        var hub = Create(clientQueue: 2);
        using var slow = hub.Subscribe(null);   // hiç okumayan istemci

        for (var i = 0; i < 3; i++) hub.Publish(One("v" + i)); // kuyruk (2) taşar, Publish beklemez

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in slow.Reader.ReadAllAsync()) { }
        });

        Assert.True(ex is SlowConsumerException || ex.InnerException is SlowConsumerException);
        Assert.Equal(0, hub.ClientCount);
    }

    [Fact]
    public async Task SlowClient_DoesNotAffectHealthyClient()
    {
        var hub = Create(clientQueue: 2);
        using var slow = hub.Subscribe(null);
        using var healthy = hub.Subscribe(null);

        var received = 0;
        for (var i = 0; i < 5; i++)
        {
            hub.Publish(One("v" + i));
            // sağlıklı istemci her turu hemen okur
            Assert.True(healthy.Reader.TryRead(out _));
            received++;
        }

        Assert.Equal(5, received);
        Assert.Equal(1, hub.ClientCount); // yalnızca sağlıklı kaldı
        await Task.CompletedTask;
    }

    [Fact]
    public void GetLatest_HidesVehiclesOlderThanCutoff()
    {
        var hub = Create();
        hub.Publish(new[] { TestData.Position("old", TestData.Now.AddHours(-4)), TestData.Position("new", TestData.Now) });

        var latest = hub.GetLatest(TestData.Now.AddHours(-3));

        Assert.Equal("new", Assert.Single(latest).EntityId);
    }
}