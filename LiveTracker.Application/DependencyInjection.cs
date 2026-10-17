using LiveTracker.Application.Ingestion;
using LiveTracker.Application.Retention;
using Microsoft.Extensions.DependencyInjection;

namespace LiveTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PositionChangeFilter>();
        services.AddSingleton<PendingBatchBuffer>();
        services.AddSingleton<IngestionStatus>();
        services.AddSingleton<VehiclePositionIngestor>();
        services.AddSingleton<RetentionJob>();

        return services;
    }
}