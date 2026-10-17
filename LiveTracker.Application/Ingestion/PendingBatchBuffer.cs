using LiveTracker.Application.Options;
using LiveTracker.Domain;
using Microsoft.Extensions.Options;

namespace LiveTracker.Application.Ingestion;

/// Yazilmayi bekleyen topluluklar. Tek dongu kullanir, thread-safe degildir.
public sealed class PendingBatchBuffer
{
    private readonly Queue<IReadOnlyList<VehiclePosition>> _batches = new();
    private readonly int _maxRows;
    private int _rows;

    public PendingBatchBuffer(IOptions<IngestionOptions> options) =>
        _maxRows = options.Value.MaxBufferedRows;

    public int PendingRows => _rows;

    /// Sinir asilirsa en eski topluluklari atar, atilan satir sayisini doner.
    public int Add(IReadOnlyList<VehiclePosition> batch)
    {
        if (batch.Count == 0) return 0;

        _batches.Enqueue(batch);
        _rows += batch.Count;

        var dropped = 0;
        while (_rows > _maxRows && _batches.Count > 1)
        {
            var old = _batches.Dequeue();
            _rows -= old.Count;
            dropped += old.Count;
        }
        return dropped;
    }

    public IReadOnlyList<VehiclePosition>? PeekOldest() =>
        _batches.Count > 0 ? _batches.Peek() : null;

    public void RemoveOldest()
    {
        var done = _batches.Dequeue();
        _rows -= done.Count;
    }
}