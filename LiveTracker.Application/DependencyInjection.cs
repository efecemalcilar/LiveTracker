using LiveTracker.Application.Ingestion;
using Microsoft.Extensions.DependencyInjection;

namespace LiveTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PositionChangeFilter>();
        services.AddSingleton<IngestionStatus>();
        services.AddSingleton<VehiclePositionIngestor>();
        return services;
    }
}