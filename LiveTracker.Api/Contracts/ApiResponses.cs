namespace LiveTracker.Api.Contracts;

/// GET /api/vehicles/latest yanıtı.
public sealed record LatestVehiclesResponse(int Count, IReadOnlyList<VehiclePositionDto> Vehicles);

/// GET /api/vehicles/{id}/positions yanıtı.
public sealed record PositionHistoryResponse(
    string VehicleId,
    DateTime From,
    DateTime? To,
    int Count,
    DateTime? NextBefore,
    IReadOnlyList<VehiclePositionDto> Positions);

/// WebSocket'ten giden mesaj. Application'daki StreamMessage'ın dışarıya açılan hali.
public sealed record StreamMessageDto(string Type, long Seq, IReadOnlyList<VehiclePositionDto> Positions);