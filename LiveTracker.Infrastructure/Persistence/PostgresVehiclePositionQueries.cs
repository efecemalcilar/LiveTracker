using LiveTracker.Application;
using LiveTracker.Application.Abstraction;
using LiveTracker.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LiveTracker.Infrastructure.Persistence
{
    public sealed class PostgresVehiclePositionQueries : IVehiclePositionQueries
    {
        private readonly LiveTrackerDbContext _db;

        public PostgresVehiclePositionQueries(LiveTrackerDbContext db) => _db = db;

        public async Task<IReadOnlyList<VehiclePosition>> GetHistoryAsync(
            string vehicleId, DateTime notBefore, DateTime? before, int limit, CancellationToken ct)
        {
            var query = _db.VehiclePosition.AsNoTracking()
                .Where(p => p.VehicleId == vehicleId && p.RecordedAt >= notBefore);

            if (before is not null)
                query = query.Where(p => p.RecordedAt < before.Value);

            return await query.OrderByDescending(p => p.RecordedAt).Take(limit).ToListAsync(ct);
        }

        public Task<long> CountSinceAsync(DateTime since, CancellationToken ct) =>
            _db.VehiclePosition.AsNoTracking().LongCountAsync(p => p.RecordedAt >= since, ct);
    }
}
