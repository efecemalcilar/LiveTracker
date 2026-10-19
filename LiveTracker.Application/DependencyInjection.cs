using LiveTracker.Application.Ingestion;
using LiveTracker.Application.Retention;
using LiveTracker.Application.Streaming;
using Microsoft.Extensions.DependencyInjection;
using LiveTracker.Application.Queries;

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
        services.AddSingleton<PositionHub>();
        services.AddScoped<VehicleQueryService>(); // IVehiclepositionqueries e bağlı ve dbcontext kullandığı için istek başına yaşıyor. Singleton gibi tek örnekli bire servis istek başına yaşayan bir şeyi tutamaz.

        return services;
    }
}