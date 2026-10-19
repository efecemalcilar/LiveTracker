using LiveTracker.Domain;

namespace LiveTracker.Api.Contracts;

/// API'nin dışarıya verdiği konum şekli. Veritabanı entity'si (VehiclePosition) doğrudan verilmez.
public sealed record VehiclePositionDto(
    string EntityId,
    string VehicleId,
    string? TripId,
    string? RouteId,
    double Latitude,
    double Longitude,
    float? Bearing,
    short? CurrentStatus,
    string? StopId,
    short? OccupancyStatus,
    DateTime RecordedAt)
{
    // Entity'den DTO'ya çeviren tek yer. IngestedAt bilerek dışarıda bırakıldı (iç bilgi).
    public static VehiclePositionDto From(VehiclePosition p) => new(
        p.EntityId, p.VehicleId, p.TripId, p.RouteId, p.Latitude, p.Longitude,
        p.Bearing, p.CurrentStatus, p.StopId, p.OccupancyStatus, p.RecordedAt);
}