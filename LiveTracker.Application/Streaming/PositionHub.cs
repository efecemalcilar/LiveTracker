using System.Threading.Channels;
using LiveTracker.Application.Options;
using LiveTracker.Domain;
using Microsoft.Extensions.Options;

namespace LiveTracker.Application.Streaming;

public sealed class PositionHub
{
    // Bağlı bir istemci: mesaj kanalı ve (varsa) abone olduğu hat.
    private sealed record Client(Channel<StreamMessage> Channel, string? RouteId);

    private readonly object _gate = new();
    private readonly Dictionary<string, VehiclePosition> _latest = new();
    private readonly LinkedList<StreamMessage> _replay = new();
    private readonly Dictionary<Guid, Client> _subscribers = new();
    private readonly StreamingOptions _options;
    private long _seq;

    public PositionHub(IOptions<StreamingOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _seq = clock.GetUtcNow().ToUnixTimeMilliseconds(); // yeniden başlatmada eski lastSeq'ler geçersiz kalsın
    }

    public long LastSeq { get { lock (_gate) return _seq; } }
    public int ClientCount { get { lock (_gate) return _subscribers.Count; } }

    // Her aracın bilinen son konumu; notBefore'dan eski olanlar (artık görünmeyen araçlar) dışarıda kalır.
    public IReadOnlyList<VehiclePosition> GetLatest(DateTime notBefore)
    {
        lock (_gate) return _latest.Values.Where(p => p.RecordedAt >= notBefore).ToList();
    }

    // Yazılmış bir turu abonelere yayınlar. Hiçbir zaman beklemez.
    public void Publish(IReadOnlyList<VehiclePosition> batch)
    {
        if (batch.Count == 0) return;
        lock (_gate)
        {
            foreach (var p in batch)
                if (!_latest.TryGetValue(p.EntityId, out var cur) || p.RecordedAt >= cur.RecordedAt)
                    _latest[p.EntityId] = p;

            var message = new StreamMessage("update", ++_seq, batch);
            _replay.AddLast(message);
            while (_replay.Count > _options.ReplayBatches) _replay.RemoveFirst();

            List<Guid>? slow = null;
            foreach (var (id, client) in _subscribers)
            {
                var toSend = ForRoute(message, client.RouteId);
                if (toSend is null) continue; // bu istemcinin hattında yeni kayıt yok, boş mesaj gitmez

                if (client.Channel.Writer.TryWrite(toSend)) continue;
                client.Channel.Writer.TryComplete(new SlowConsumerException());
                (slow ??= new()).Add(id);
            }
            if (slow != null) foreach (var id in slow) _subscribers.Remove(id);
        }
    }

    // lastSeq verilmişse ve aradaki turlar hâlâ bellekteyse onları, yoksa snapshot'ı döner.
    // routeId verilmişse hepsi sadece o hatla sınırlanır.
    public Subscription Subscribe(long? lastSeq, string? routeId = null)
    {
        lock (_gate)
        {
            var initial = new List<StreamMessage>();
            var canReplay = lastSeq is long last && last <= _seq && _seq - last <= _replay.Count;
            if (canReplay)
            {
                foreach (var m in _replay)
                {
                    if (m.Seq <= lastSeq!.Value) continue;
                    var filtered = ForRoute(m, routeId);
                    if (filtered is not null) initial.Add(filtered);
                }
            }
            else
            {
                var latest = _latest.Values.Where(p => routeId is null || p.RouteId == routeId).ToList();
                initial.Add(new StreamMessage("snapshot", _seq, latest));
            }

            var channel = Channel.CreateBounded<StreamMessage>(new BoundedChannelOptions(_options.ClientQueueBatches)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            var id = Guid.NewGuid();
            _subscribers[id] = new Client(channel, routeId);
            return new Subscription(initial, channel.Reader, () => Remove(id));
        }
    }

    // Bir mesajı tek hatla sınırlar. routeId yoksa mesaj aynen döner;
    // o hatta hiç kayıt yoksa null döner (istemciye boş mesaj gitmesin).
    private static StreamMessage? ForRoute(StreamMessage message, string? routeId)
    {
        if (routeId is null) return message;
        var items = message.Positions.Where(p => p.RouteId == routeId).ToList();
        return items.Count == 0 ? null : message with { Positions = items };
    }

    private void Remove(Guid id)
    {
        lock (_gate)
            if (_subscribers.Remove(id, out var client)) client.Channel.Writer.TryComplete();
    }
}