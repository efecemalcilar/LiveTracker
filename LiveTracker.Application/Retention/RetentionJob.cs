using LiveTracker.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTracker.Application.Retention
{
    public sealed class RetentionJob
    {
        private readonly IRetentionStore _store;
        private readonly RetentionOptions _options;
        private readonly TimeProvider _time;
        private readonly ILogger<RetentionJob> _logger;

        public RetentionJob(IRetentionStore store, IOptions<RetentionOptions> options,
            TimeProvider time, ILogger<RetentionJob> logger)
        {
            _store = store;
            _options = options.Value;
            _time = time;
            _logger = logger;
        }

        public Task ApplyChunkIntervalAsync(CancellationToken ct) =>
            _store.SetChunkIntervalAsync(_options.ChunkInterval, ct);

        public async Task RunOnceAsync(CancellationToken ct)
        {
            var cutoff = _time.GetUtcNow().UtcDateTime - _options.Window;
            var dropped = await _store.DropChunksOlderThanAsync(cutoff, ct);
            if (dropped > 0)
                _logger.LogInformation("Silme: {Dropped} chunk dusuruldu (sinir {Cutoff:HH:mm:ss} UTC)", dropped, cutoff);
        }
    }
}
