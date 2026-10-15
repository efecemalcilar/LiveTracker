using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveTracker.Domain;

namespace LiveTracker.Application.Ingestion
{
    public sealed record FilterResult(
    IReadOnlyList<VehiclePosition> Fresh,
    int Unchanged,
    int OutOfOrder,
    int TooOld,
    int FutureDated);

    /// Tek bir döngüden çağrılır, thread-safe değildir (bilerek).
    public sealed class PositionChangeFilter
    {
        private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(5);
        private readonly Dictionary<string, DateTime> _lastSeen = new();

        public int TrackedCount => _lastSeen.Count;

        public FilterResult Select(IReadOnlyList<VehiclePosition> positions, DateTime nowUtc, TimeSpan window)
        {
            var cutoff = nowUtc - window;
            var latest = nowUtc + MaxFutureSkew;
            var fresh = new List<VehiclePosition>();
            int unchanged = 0, outOfOrder = 0, tooOld = 0, future = 0;

            foreach (var p in positions)
            {
                if (p.RecordedAt < cutoff) { tooOld++; continue; }
                if (p.RecordedAt > latest) { future++; continue; }

                if (_lastSeen.TryGetValue(p.EntityId, out var prev))
                {
                    if (p.RecordedAt == prev) { unchanged++; continue; }
                    if (p.RecordedAt < prev) { outOfOrder++; continue; }
                }
                fresh.Add(p);
            }

            return new FilterResult(fresh, unchanged, outOfOrder, tooOld, future);
        }

        /// Yalnızca veritabanına başarıyla yazılan kayıtlar için çağrılır.
        public void MarkWritten(IEnumerable<VehiclePosition> written)
        {
            foreach (var p in written) _lastSeen[p.EntityId] = p.RecordedAt;
        }

        /// Bellek sınırsız büyümesin diye pencerenin dışına çıkanları unut.
        public void Prune(DateTime cutoffUtc)
        {
            foreach (var kv in _lastSeen)
                if (kv.Value < cutoffUtc) _lastSeen.Remove(kv.Key);
        }
    }
}
