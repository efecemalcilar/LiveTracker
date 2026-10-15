using LiveTracker.Application.Abstraction;
using LiveTracker.Application.Options;
using LiveTracker.Domain;
using Microsoft.Extensions.Options;
using Proto = TransitRealtime;

namespace LiveTracker.Infrastructure.Feed;

public sealed class GtfsRealtimeVehicleSource : IVehiclePositionSource
{
    private readonly HttpClient _http;
    private readonly IngestionOptions _options;
    private readonly TimeProvider _time;

    public GtfsRealtimeVehicleSource(HttpClient http, IOptions<IngestionOptions> options, TimeProvider time)
    {
        _http = http;
        _options = options.Value;
        _time = time;
    }

    public async Task<FeedSnapshot> FetchAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync(_options.FeedUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var feed = Proto.FeedMessage.Parser.ParseFrom(stream);   // bozuk govde burada exception firlatir

        var ingestedAt = _time.GetUtcNow().UtcDateTime;
        var positions = new List<VehiclePosition>(feed.Entity.Count);
        var invalid = 0;

        foreach (var e in feed.Entity)
        {
            var v = e.Vehicle;
            if (v == null || v.Position == null || !v.HasTimestamp) { invalid++; continue; }

            var lat = v.Position.Latitude;
            var lon = v.Position.Longitude;
            if ((lat == 0 && lon == 0) || lat is < -90 or > 90 || lon is < -180 or > 180)
            {
                invalid++;
                continue;
            }

            var vehicleId = string.IsNullOrEmpty(v.Vehicle?.Id) ? e.Id : v.Vehicle.Id;

            positions.Add(new VehiclePosition
            {
                EntityId = e.Id,
                RecordedAt = DateTimeOffset.FromUnixTimeSeconds((long)v.Timestamp).UtcDateTime,
                VehicleId = vehicleId,
                TripId = NullIfEmpty(v.Trip?.TripId),
                RouteId = NullIfEmpty(v.Trip?.RouteId),
                Latitude = lat,
                Longitude = lon,
                Bearing = v.Position.HasBearing ? v.Position.Bearing : null,
                CurrentStatus = v.HasCurrentStatus ? (short)v.CurrentStatus : null,
                StopId = NullIfEmpty(v.StopId),
                OccupancyStatus = v.HasOccupancyStatus ? (short)v.OccupancyStatus : null,
                IngestedAt = ingestedAt
            });
        }

        var headerTs = DateTimeOffset.FromUnixTimeSeconds((long)feed.Header.Timestamp).UtcDateTime;
        return new FeedSnapshot(headerTs, positions, invalid);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}