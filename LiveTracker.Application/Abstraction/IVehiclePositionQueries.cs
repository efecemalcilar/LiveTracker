using LiveTracker.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTracker.Application.Abstraction
{
    public interface IVehiclePositionQueries
    {
        // Bir aracın geçmişi: en yeniden eskiye, notBefore'dan eski olanlar hiç gelmez.
        Task<IReadOnlyList<VehiclePosition>> GetHistoryAsync(
            string vehicleId, DateTime notBefore, DateTime? before, int limit, CancellationToken ct);

        // since'ten bu yana kayıt sayısı (sistem durumu için)
        Task<long> CountSinceAsync(DateTime since, CancellationToken ct);
    }
}
