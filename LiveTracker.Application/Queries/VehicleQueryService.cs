using LiveTracker.Application.Abstraction;
using LiveTracker.Application.Options;
using LiveTracker.Application.Streaming;
using LiveTracker.Domain;
using Microsoft.Extensions.Options;

namespace LiveTracker.Application.Queries;

/// Bir sayfa araç geçmişi. From ve To, gerçekte uygulanan sınırlardır.
public sealed record PositionHistoryPage(
    string VehicleId,
    DateTime From,
    DateTime? To,
    IReadOnlyList<VehiclePosition> Items,
    DateTime? NextBefore);

/// REST sorgularının iş kuralları. Controller sadece HTTP'yi çevirir, kurallar burada.
public sealed class VehicleQueryService
{
    private readonly PositionHub _hub;
    private readonly IVehiclePositionQueries _queries;
    private readonly RetentionOptions _retention;
    private readonly TimeProvider _time;

    public VehicleQueryService(PositionHub hub, IVehiclePositionQueries queries,
        IOptions<RetentionOptions> retention, TimeProvider time)
    {
        _hub = hub;
        _queries = queries;
        _retention = retention.Value;
        _time = time;
    }

    // Her aracın son konumu, saklama penceresi içinde. routeId verilirse o hatla sınırlı.
    public IReadOnlyList<VehiclePosition> GetLatest(string? routeId)
    {
        var items = _hub.GetLatest(WindowStart());
        if (string.IsNullOrEmpty(routeId)) return items;
        return items.Where(p => p.RouteId == routeId).ToList();
    }

    // Bir aracın geçmişi, yeniden eskiye. Zaman sınırları saklama penceresiyle kesiştirilir.
    public async Task<PositionHistoryPage> GetHistoryAsync(
        string vehicleId, DateTime? from, DateTime? to, DateTime? before, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var floor = WindowStart();

        // alt sınır: istenen 'from', ama saklama penceresinin dışına çıkamaz
        var lower = from is not null && from.Value > floor ? from.Value : floor;

        // üst sınır: 'to' ve 'before' değerlerinden küçük olanı
        var upper = before;
        if (to is not null && (upper is null || to.Value < upper)) upper = to;

        // bir fazla isteriz: sonraki sayfa var mı diye anlamak için
        var rows = await _queries.GetHistoryAsync(vehicleId, lower, upper, limit + 1, ct);

        var page = rows.Take(limit).ToList();
        DateTime? nextBefore = rows.Count > limit ? page[^1].RecordedAt : null;
        return new PositionHistoryPage(vehicleId, lower, upper, page, nextBefore);
    }

    private DateTime WindowStart() => _time.GetUtcNow().UtcDateTime - _retention.Window;
}