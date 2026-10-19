using LiveTracker.Application.Abstraction;
using LiveTracker.Application.Options;
using LiveTracker.Application.Queries;
using LiveTracker.Application.Streaming;
using LiveTracker.Domain;
using Microsoft.Extensions.Options;

namespace LiveTracker.Tests;

public class VehicleQueryServiceTests
{
    // Sabit saat: test "şimdi" ne olduğundan etkilenmesin.
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }

    // Sahte (fake) veritabanı: gerçek sorgu yapmaz, servisin ona ne verdiğini not eder.
    private sealed class FakeQueries : IVehiclePositionQueries
    {
        public IReadOnlyList<VehiclePosition> Rows { get; set; } = Array.Empty<VehiclePosition>();
        public DateTime? LastNotBefore { get; private set; }
        public DateTime? LastBefore { get; private set; }
        public int LastLimit { get; private set; }

        public Task<IReadOnlyList<VehiclePosition>> GetHistoryAsync(
            string vehicleId, DateTime notBefore, DateTime? before, int limit, CancellationToken ct)
        {
            LastNotBefore = notBefore;
            LastBefore = before;
            LastLimit = limit;
            return Task.FromResult(Rows);
        }

        public Task<long> CountSinceAsync(DateTime since, CancellationToken ct) => Task.FromResult(0L);
    }

    private static (VehicleQueryService Service, FakeQueries Queries, PositionHub Hub) Create()
    {
        var time = new FixedTimeProvider(TestData.Now);
        var hub = new PositionHub(Options.Create(new StreamingOptions()), time);
        var queries = new FakeQueries();
        var service = new VehicleQueryService(hub, queries,
            Options.Create(new RetentionOptions { Window = TimeSpan.FromHours(3) }), time);
        return (service, queries, hub);
    }

    [Fact]
    public async Task From_OlderThanWindow_IsClampedToWindowStart()
    {
        var (service, queries, _) = Create();
        var windowStart = TestData.Now.AddHours(-3);

        var page = await service.GetHistoryAsync("v1", TestData.Now.AddHours(-10), null, null, 100, default);

        Assert.Equal(windowStart, page.From);
        Assert.Equal(windowStart, queries.LastNotBefore);
    }

    [Fact]
    public async Task From_InsideWindow_IsKept()
    {
        var (service, _, _) = Create();
        var from = TestData.Now.AddHours(-1);

        var page = await service.GetHistoryAsync("v1", from, null, null, 100, default);

        Assert.Equal(from, page.From);
    }

    [Fact]
    public async Task ToAndBefore_SmallerOneBecomesUpperBound()
    {
        var (service, _, _) = Create();
        var earlier = TestData.Now.AddMinutes(-30);
        var later = TestData.Now.AddMinutes(-10);

        var a = await service.GetHistoryAsync("v1", null, earlier, later, 100, default);
        var b = await service.GetHistoryAsync("v1", null, later, earlier, 100, default);

        Assert.Equal(earlier, a.To);
        Assert.Equal(earlier, b.To);
    }

    [Fact]
    public async Task ExtraRow_MeansNextPageExists_AndSetsNextBefore()
    {
        var (service, queries, _) = Create();
        queries.Rows = new[]
        {
            TestData.Position("e1", TestData.Now.AddMinutes(-1)),
            TestData.Position("e2", TestData.Now.AddMinutes(-2)),
            TestData.Position("e3", TestData.Now.AddMinutes(-3)),
        };

        var page = await service.GetHistoryAsync("v1", null, null, null, 2, default);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(TestData.Now.AddMinutes(-2), page.NextBefore);
        Assert.Equal(3, queries.LastLimit);   // bir fazla istedi
    }

    [Fact]
    public async Task NoExtraRow_MeansLastPage()
    {
        var (service, queries, _) = Create();
        queries.Rows = new[]
        {
            TestData.Position("e1", TestData.Now.AddMinutes(-1)),
            TestData.Position("e2", TestData.Now.AddMinutes(-2)),
        };

        var page = await service.GetHistoryAsync("v1", null, null, null, 2, default);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextBefore);
    }

    [Fact]
    public async Task Limit_IsClampedToAllowedRange()
    {
        var (service, queries, _) = Create();

        await service.GetHistoryAsync("v1", null, null, null, 5000, default);
        Assert.Equal(1001, queries.LastLimit);   // 1000 + 1

        await service.GetHistoryAsync("v1", null, null, null, 0, default);
        Assert.Equal(2, queries.LastLimit);      // 1 + 1
    }

    [Fact]
    public void GetLatest_WithRouteId_ReturnsOnlyThatRoute()
    {
        var (service, _, hub) = Create();
        var a = TestData.Position("a", TestData.Now);
        a.RouteId = "r1";
        var b = TestData.Position("b", TestData.Now);
        b.RouteId = "r2";
        hub.Publish(new[] { a, b });

        var result = service.GetLatest("r1");

        Assert.Equal("a", Assert.Single(result).EntityId);
    }

    [Fact]
    public void GetLatest_DropsVehiclesOutsideWindow()
    {
        var (service, _, hub) = Create();
        hub.Publish(new[]
        {
            TestData.Position("old", TestData.Now.AddHours(-4)),
            TestData.Position("fresh", TestData.Now)
        });

        var result = service.GetLatest(null);

        Assert.Equal("fresh", Assert.Single(result).EntityId);
    }
}