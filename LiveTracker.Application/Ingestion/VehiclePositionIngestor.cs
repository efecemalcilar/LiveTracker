using LiveTracker.Application.Abstraction;
using LiveTracker.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace LiveTracker.Application.Ingestion;

public sealed class VehiclePositionIngestor
{
    private readonly IVehiclePositionSource _source;
    private readonly IVehiclePositionStore _store;
    private readonly PositionChangeFilter _filter;
    private readonly IngestionStatus _status;
    private readonly RetentionOptions _retention;
    private readonly TimeProvider _time;
    private readonly ILogger<VehiclePositionIngestor> _logger;

    public VehiclePositionIngestor(
        IVehiclePositionSource source,
        IVehiclePositionStore store,
        PositionChangeFilter filter,
        IngestionStatus status,
        IOptions<RetentionOptions> retention,
        TimeProvider time,
        ILogger<VehiclePositionIngestor> logger)
    {
        _source = source;
        _store = store;
        _filter = filter;
        _status = status;
        _retention = retention.Value;
        _time = time;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        var snapshot = await _source.FetchAsync(ct);
        var now = _time.GetUtcNow().UtcDateTime;
        var result = _filter.Select(snapshot.Positions, now, _retention.Window);

        var inserted = result.Fresh.Count > 0
            ? await _store.SaveAsync(result.Fresh, ct)
            : 0;

        // Buraya yalnızca yazma basariliysa gelinir; hata olursa filtre guncellenmez
        // ve ayni kayitlar bir sonraki turda yeniden denenir.
        _filter.MarkWritten(result.Fresh);
        _filter.Prune(now - _retention.Window);
        _status.RecordSuccess(now, inserted);

        var level = result.Fresh.Count > 0 ? LogLevel.Information : LogLevel.Debug;
        _logger.Log(level,
            "Okuma tamam: {Total} kayit, yeni {Fresh}, yazilan {Inserted}, ayni {Unchanged}, sirasiz {OutOfOrder}, eski {TooOld}, gelecek {Future}, gecersiz {Invalid}, {Elapsed} ms",
            snapshot.Positions.Count, result.Fresh.Count, inserted, result.Unchanged,
            result.OutOfOrder, result.TooOld, result.FutureDated, snapshot.InvalidCount,
            sw.ElapsedMilliseconds);
    }
}