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
    private readonly PendingBatchBuffer _buffer;
    private readonly IngestionStatus _status;
    private readonly RetentionOptions _retention;
    private readonly TimeProvider _time;
    private readonly ILogger<VehiclePositionIngestor> _logger;

    public VehiclePositionIngestor(
        IVehiclePositionSource source,
        IVehiclePositionStore store,
        PositionChangeFilter filter,
        PendingBatchBuffer buffer,
        IngestionStatus status,
        IOptions<RetentionOptions> retention,
        TimeProvider time,
        ILogger<VehiclePositionIngestor> logger)
    {
        _source = source;
        _store = store;
        _filter = filter;
        _buffer = buffer;
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

        // Once kuyruga koyuyoruz. Yazma hata verse bile kayitlar kuyrukta bekler
        // ve filtre ayni kayitlari bir sonraki turda yeniden secmez.
        _filter.MarkSeen(result.Fresh);
        _filter.Prune(now - _retention.Window);

        var dropped = _buffer.Add(result.Fresh);
        if (dropped > 0)
            _logger.LogError("Kuyruk doldu, {Dropped} kayit atildi (veri kaybi)", dropped);

        int inserted;
        try
        {
            inserted = await FlushAsync(ct);
        }
        catch
        {
            _logger.LogWarning("Yazma basarisiz, {Pending} kayit kuyrukta bekliyor", _buffer.PendingRows);
            throw;
        }

        _status.RecordSuccess(now, inserted);

        var level = result.Fresh.Count > 0 || inserted > 0 ? LogLevel.Information : LogLevel.Debug;
        _logger.Log(level,
            "Okuma tamam: {Total} kayit, yeni {Fresh}, yazilan {Inserted}, ayni {Unchanged}, sirasiz {OutOfOrder}, eski {TooOld}, gelecek {Future}, gecersiz {Invalid}, kuyrukta {Pending}, {Elapsed} ms",
            snapshot.Positions.Count, result.Fresh.Count, inserted, result.Unchanged,
            result.OutOfOrder, result.TooOld, result.FutureDated, snapshot.InvalidCount,
            _buffer.PendingRows, sw.ElapsedMilliseconds);
    }

    // En eskiden baslayarak yazar. Biri basarisiz olursa kalanlar kuyrukta durur.
    private async Task<int> FlushAsync(CancellationToken ct)
    {
        var inserted = 0;
        while (_buffer.PeekOldest() is { } batch)
        {
            inserted += await _store.SaveAsync(batch, ct);
            _buffer.RemoveOldest();
        }
        return inserted;
    }
}