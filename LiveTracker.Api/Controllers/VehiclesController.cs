using LiveTracker.Api.Contracts;
using LiveTracker.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace LiveTracker.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly VehicleQueryService _service;

    public VehiclesController(VehicleQueryService service) => _service = service;

    // Her aracın son bilinen konumu. İsteğe bağlı: ?routeId=...
    [HttpGet("latest")]
    [ProducesResponseType(typeof(LatestVehiclesResponse), StatusCodes.Status200OK)]
    public IActionResult GetLatest([FromQuery] string? routeId)
    {
        var vehicles = _service.GetLatest(routeId).Select(VehiclePositionDto.From).ToList();
        return Ok(new LatestVehiclesResponse(vehicles.Count, vehicles));
    }

    // Bir aracın son 3 saatlik geçmişi, yeniden eskiye, sayfalı.
    // from (dahil) ve to (hariç) ile zaman aralığı seçilir; sonraki sayfa için nextBefore, before olarak verilir.
    [HttpGet("{vehicleId}/positions")]
    [ProducesResponseType(typeof(PositionHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHistory(
        string vehicleId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DateTimeOffset? before,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        if (from is not null && to is not null && from >= to)
            return BadRequest("'from' must be earlier than 'to'.");

        var page = await _service.GetHistoryAsync(
            vehicleId, from?.UtcDateTime, to?.UtcDateTime, before?.UtcDateTime, limit, ct);

        return Ok(new PositionHistoryResponse(
            page.VehicleId, page.From, page.To, page.Items.Count, page.NextBefore,
            page.Items.Select(VehiclePositionDto.From).ToList()));
    }
}