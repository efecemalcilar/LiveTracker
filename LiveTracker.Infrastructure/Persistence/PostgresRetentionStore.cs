using LiveTracker.Application;
using Npgsql;
using NpgsqlTypes;

namespace LiveTracker.Infrastructure.Persistence;

public sealed class PostgresRetentionStore : IRetentionStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresRetentionStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task SetChunkIntervalAsync(TimeSpan interval, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT set_chunk_time_interval('vehicle_positions', @interval)");
        cmd.Parameters.AddWithValue("interval", interval);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> DropChunksOlderThanAsync(DateTime cutoffUtc, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT count(*) FROM drop_chunks('vehicle_positions', older_than => @cutoff)");
        cmd.Parameters.Add(new NpgsqlParameter("cutoff", NpgsqlDbType.TimestampTz) { Value = cutoffUtc });
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }
}