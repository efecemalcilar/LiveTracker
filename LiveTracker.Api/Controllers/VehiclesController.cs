using LiveTracker.Application.Abstraction;
using LiveTracker.Application.Options;
using LiveTracker.Application.Streaming;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
// IVehiclePositionQueries için gereken using'i de ekle (IVehiclePositionStore ile aynı)

namespace LiveTracker.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly PositionHub _hub;
    private readonly IVehiclePositionQueries _queries;
    private readonly RetentionOptions _retention;
    private readonly TimeProvider _time;

    public VehiclesController(PositionHub hub, IVehiclePositionQueries queries,
        IOptions<RetentionOptions> retention, TimeProvider time)
    {
        _hub = hub; _queries = queries; _retention = retention.Value; _time = time;
    }

    // Her aracın son bilinen konumu. İsteğe bağlı: ?routeId=...
    [HttpGet("latest")]
    public IActionResult GetLatest([FromQuery] string? routeId)
    {
        var notBefore = _time.GetUtcNow().UtcDateTime - _retention.Window;
        var items = _hub.GetLatest(notBefore).AsEnumerable();
        if (!string.IsNullOrEmpty(routeId))
            items = items.Where(p => p.RouteId == routeId);

        var list = items.ToList();
        return Ok(new { count = list.Count, vehicles = list });
    }

    // Bir aracın son 3 saatlik geçmişi, yeniden eskiye, sayfalı.
    // Sonraki sayfa: önceki cevaptaki nextBefore değeri ?before= olarak verilir.
    [HttpGet("{vehicleId}/positions")]
    public async Task<IActionResult> GetHistory(
        string vehicleId, [FromQuery] DateTimeOffset? before, [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var notBefore = _time.GetUtcNow().UtcDateTime - _retention.Window;

        // bir fazla isteriz: daha sayfa var mı diye anlamak için
        var rows = await _queries.GetHistoryAsync(
            vehicleId, notBefore, before?.UtcDateTime, limit + 1, ct);

        var hasMore = rows.Count > limit;
        var page = rows.Take(limit).ToList();
        DateTime? nextBefore = hasMore ? page[^1].RecordedAt : null;

        return Ok(new { vehicleId, count = page.Count, nextBefore, positions = page });
    }
}