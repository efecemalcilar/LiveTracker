using LiveTracker.Application.Options;
using LiveTracker.Application.Retention;
using Microsoft.Extensions.Options;

namespace LiveTracker.Api.Workers;

public sealed class RetentionWorker : BackgroundService
{
    private readonly RetentionJob _job;
    private readonly RetentionOptions _options;
    private readonly ILogger<RetentionWorker> _logger;

    public RetentionWorker(RetentionJob job, IOptions<RetentionOptions> options,
        ILogger<RetentionWorker> logger)
    {
        _job = job;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await _job.ApplyChunkIntervalAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Chunk araligi ayarlanamadi");
        }

        using var timer = new PeriodicTimer(_options.CleanupInterval);
        try
        {
            do
            {
                try
                {
                    await _job.RunOnceAsync(ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Silme turu basarisiz, sonraki turda tekrar denenecek");
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