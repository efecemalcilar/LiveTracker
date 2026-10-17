using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTracker.Application
{
    public interface IRetentionStore
    {
        Task SetChunkIntervalAsync(TimeSpan interval, CancellationToken ct);
        Task<int> DropChunksOlderThanAsync(DateTime cutoffUtc, CancellationToken ct);
    }
}
