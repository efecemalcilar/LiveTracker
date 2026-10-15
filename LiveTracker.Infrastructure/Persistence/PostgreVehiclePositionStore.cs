using LiveTracker.Application.Abstraction;
using LiveTracker.Domain;
using Npgsql;
using NpgsqlTypes;

namespace LiveTracker.Infrastructure.Persistence;

public sealed class PostgresVehiclePositionStore : IVehiclePositionStore
{
    private const string Sql = """
        INSERT INTO vehicle_positions
            (entity_id, recorded_at, vehicle_id, trip_id, route_id, latitude, longitude,
             bearing, current_status, stop_id, occupancy_status, ingested_at)
        SELECT * FROM unnest(
            @entity_id, @recorded_at, @vehicle_id, @trip_id, @route_id, @latitude, @longitude,
            @bearing, @current_status, @stop_id, @occupancy_status, @ingested_at)
        ON CONFLICT (entity_id, recorded_at) DO NOTHING
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresVehiclePositionStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<int> SaveAsync(IReadOnlyList<VehiclePosition> p, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(Sql);
        cmd.CommandTimeout = 30;

        Add(cmd, "entity_id", NpgsqlDbType.Text, p.Select(x => x.EntityId).ToArray());
        Add(cmd, "recorded_at", NpgsqlDbType.TimestampTz, p.Select(x => x.RecordedAt).ToArray());
        Add(cmd, "vehicle_id", NpgsqlDbType.Text, p.Select(x => x.VehicleId).ToArray());
        Add(cmd, "trip_id", NpgsqlDbType.Text, p.Select(x => x.TripId).ToArray());
        Add(cmd, "route_id", NpgsqlDbType.Text, p.Select(x => x.RouteId).ToArray());
        Add(cmd, "latitude", NpgsqlDbType.Double, p.Select(x => x.Latitude).ToArray());
        Add(cmd, "longitude", NpgsqlDbType.Double, p.Select(x => x.Longitude).ToArray());
        Add(cmd, "bearing", NpgsqlDbType.Real, p.Select(x => x.Bearing).ToArray());
        Add(cmd, "current_status", NpgsqlDbType.Smallint, p.Select(x => x.CurrentStatus).ToArray());
        Add(cmd, "stop_id", NpgsqlDbType.Text, p.Select(x => x.StopId).ToArray());
        Add(cmd, "occupancy_status", NpgsqlDbType.Smallint, p.Select(x => x.OccupancyStatus).ToArray());
        Add(cmd, "ingested_at", NpgsqlDbType.TimestampTz, p.Select(x => x.IngestedAt).ToArray());

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void Add(NpgsqlCommand cmd, string name, NpgsqlDbType type, Array values) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Array | type) { Value = values });
}