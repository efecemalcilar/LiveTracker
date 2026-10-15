using LiveTracker.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTracker.Application.Abstraction
{
    public sealed record FeedSnapshot(
        DateTime HeaderTimestamp,
        IReadOnlyList<VehiclePosition> Positions,
        int InvalidCount
        );
    public interface IVehiclePositionSource
    {
        Task<FeedSnapshot> FetchAsync(CancellationToken ct);
    }


}
