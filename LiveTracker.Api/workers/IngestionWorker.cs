using LiveTracker.Application.Ingestion;
using LiveTracker.Application.Options;
using Microsoft.Extensions.Options;

namespace LiveTracker.Api.Workers;

public sealed class IngestionWorker : BackgroundService
{
    private readonly VehiclePositionIngestor _ingestor;
    private readonly IngestionStatus _status;
    private readonly IngestionOptions _options;
    private readonly ILogger<IngestionWorker> _logger;

    public IngestionWorker(
        VehiclePositionIngestor ingestor,
        IngestionStatus status,
        IOptions<IngestionOptions> options,
        ILogger<IngestionWorker> logger)
    {
        _ingestor = ingestor;
        _status = status;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));
        _logger.LogInformation("Veri toplama basladi: {Url}, aralik {Seconds} sn",
            _options.FeedUrl, _options.PollIntervalSeconds);

        try
        {
            do
            {
                try
                {
                    await _ingestor.RunOnceAsync(ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _status.RecordFailure(ex);
                    _logger.LogWarning(ex, "Okuma turu basarisiz, sonraki turda tekrar denenecek");
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
            // uygulama kapanirken beklenen durum
        }
    }
}