using LiveTracker.Domain;

namespace LiveTracker.Application.Streaming;

// İstemciye giden tek mesaj. Type: "snapshot" veya "update".
public sealed record StreamMessage(string Type, long Seq, IReadOnlyList<VehiclePosition> Positions);

// Kuyruğu dolan istemcinin kanalı bu hatayla kapatılır.
public sealed class SlowConsumerException : Exception
{
    public SlowConsumerException() : base("İstemci yeterince hızlı okumadı.") { }
}

// Bir abonelik: ilk mesajlar + sonraki canlı mesajlar. Dispose edilince abonelik biter.
public sealed class Subscription : IDisposable
{
    private readonly Action _onDispose;
    public IReadOnlyList<StreamMessage> Initial { get; }
    public System.Threading.Channels.ChannelReader<StreamMessage> Reader { get; }

    public Subscription(IReadOnlyList<StreamMessage> initial,
        System.Threading.Channels.ChannelReader<StreamMessage> reader, Action onDispose)
    {
        Initial = initial; Reader = reader; _onDispose = onDispose;
    }

    public void Dispose() => _onDispose();
}