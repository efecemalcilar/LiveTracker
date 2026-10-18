using LiveTracker.Domain;

namespace LiveTracker.Tests;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    // Tek bir örnek konum kartı: kimlik ve zaman bizden, geri kalanı sabit
    public static VehiclePosition Position(string entityId, DateTime recordedAt) => new()
    {
        EntityId = entityId,
        RecordedAt = recordedAt,
        VehicleId = "v-" + entityId,
        Latitude = 59.9,
        Longitude = 10.7,
        IngestedAt = Now
    };

    public static IReadOnlyList<VehiclePosition> Batch(int count) =>
        Enumerable.Range(0, count).Select(i => Position("e" + i, Now)).ToList();
}