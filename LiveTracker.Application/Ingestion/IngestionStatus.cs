using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTracker.Application.Ingestion
{
    public sealed class IngestionStatus
    {
        private readonly object _gate = new();
        private DateTime? _lastSuccessAt;
        private long _failedAttempts;
        private long _totalWritten;
        private string? _lastError;

        public void RecordSuccess(DateTime nowUtc, int written)
        {
            lock (_gate) { _lastSuccessAt = nowUtc; _totalWritten += written; }
        }

        public void RecordFailure(Exception ex)
        {
            lock (_gate) { _failedAttempts++; _lastError = $"{ex.GetType().Name}: {ex.Message}"; }
        }

        public (DateTime? LastSuccessAt, long FailedAttempts, long TotalWritten, string? LastError) Snapshot()
        {
            lock (_gate) return (_lastSuccessAt, _failedAttempts, _totalWritten, _lastError);
        }
    }
}
