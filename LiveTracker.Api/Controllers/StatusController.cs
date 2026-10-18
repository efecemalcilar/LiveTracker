using LiveTracker.Application.Abstraction;
using LiveTracker.Application.Ingestion;
using LiveTracker.Application.Streaming;
using Microsoft.AspNetCore.Mvc;
// IVehiclePositionQueries için gereken using'i de ekle

namespace LiveTracker.Api.Controllers;

[ApiController]
[Route("api/status")]
public sealed class StatusController : ControllerBase
{
    private readonly IngestionStatus _status;
    private readonly PendingBatchBuffer _buffer;
    private readonly PositionHub _hub;
    private readonly IVehiclePositionQueries _queries;
    private readonly TimeProvider _time;

    public StatusController(IngestionStatus status, PendingBatchBuffer buffer, PositionHub hub,
        IVehiclePositionQueries queries, TimeProvider time)
    {
        _status = status; _buffer = buffer; _hub = hub; _queries = queries; _time = time;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var s = _status.Snapshot();
        var writtenLastHour = await _queries.CountSinceAsync(now - TimeSpan.FromHours(1), ct);

        double? secondsSinceSuccess = s.LastSuccessAt is null ? null : (now - s.LastSuccessAt.Value).TotalSeconds;
        var healthy = secondsSinceSuccess is < 30;

        return Ok(new
        {
            status = healthy ? "healthy" : "degraded",
            lastSuccessAt = s.LastSuccessAt,
            secondsSinceLastSuccess = secondsSinceSuccess,
            failedAttempts = s.FailedAttempts,
            lastError = s.LastError,
            totalWrittenSinceStart = s.TotalWritten,
            writtenLastHour,
            pendingRows = _buffer.PendingRows,
            webSocketClients = _hub.ClientCount
        });
    }
}