using System.ComponentModel.DataAnnotations;

namespace LiveTracker.Application.Options;

public sealed class StreamingOptions
{
    public const string SectionName = "Streaming";

    // Yeniden bağlanana tekrar gönderilebilecek son tur sayısı (1200 tur ≈ 1 saat)
    [Range(10, 100_000)] public int ReplayBatches { get; set; } = 1200;

    // Her istemcinin bekleyen mesaj sınırı; dolarsa o istemci kapatılır
    [Range(1, 10_000)] public int ClientQueueBatches { get; set; } = 100;
}