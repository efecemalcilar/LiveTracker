using LiveTracker.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTracker.Application.Abstraction
{
    public interface IVehiclePositionStore
    {
        Task<int> SaveAsync(IReadOnlyList<VehiclePosition> positions, CancellationToken ct);
    }
}
