using LiveTracker.Application;
using LiveTracker.Application.Abstraction;
using LiveTracker.Application.Options;
using LiveTracker.Infrastructure.Feed;
using LiveTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Net;

namespace LiveTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")!;

        services.AddDbContext<LiveTrackerDbContext>(o => o.UseNpgsql(connectionString));
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IVehiclePositionStore, PostgresVehiclePositionStore>();
        services.AddSingleton<IRetentionStore,PostgresRetentionStore>();

        services.AddHttpClient<IVehiclePositionSource, GtfsRealtimeVehicleSource>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<IngestionOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(o.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.Add("ET-Client-Name", o.ClientName);
        })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)   // uzun omurlu istemcide DNS yenilensin
            });

        return services;
    }
}