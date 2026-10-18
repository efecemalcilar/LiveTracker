using System.Threading.Channels;
using LiveTracker.Application.Options;
using LiveTracker.Domain;
using Microsoft.Extensions.Options;

namespace LiveTracker.Application.Streaming;

public sealed class PositionHub
{
    private readonly object _gate = new();
    private readonly Dictionary<string, VehiclePosition> _latest = new();
    private readonly LinkedList<StreamMessage> _replay = new();
    private readonly Dictionary<Guid, Channel<StreamMessage>> _subscribers = new();
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

    // Yazılmış bir turu herkese yayınlar. Hiçbir zaman beklemez.
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
            foreach (var (id, channel) in _subscribers)
            {
                if (channel.Writer.TryWrite(message)) continue;
                channel.Writer.TryComplete(new SlowConsumerException());
                (slow ??= new()).Add(id);
            }
            if (slow != null) foreach (var id in slow) _subscribers.Remove(id);
        }
    }

    // lastSeq verilmişse ve aradaki turlar hâlâ bellekteyse onları, yoksa snapshot'ı döner.
    public Subscription Subscribe(long? lastSeq)
    {
        lock (_gate)
        {
            var initial = new List<StreamMessage>();
            var canReplay = lastSeq is long last && last <= _seq && _seq - last <= _replay.Count;
            if (canReplay)
                foreach (var m in _replay) { if (m.Seq > lastSeq!.Value) initial.Add(m); }
            else
                initial.Add(new StreamMessage("snapshot", _seq, _latest.Values.ToList()));

            var channel = Channel.CreateBounded<StreamMessage>(new BoundedChannelOptions(_options.ClientQueueBatches)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            var id = Guid.NewGuid();
            _subscribers[id] = channel;
            return new Subscription(initial, channel.Reader, () => Remove(id));
        }
    }

    private void Remove(Guid id)
    {
        lock (_gate)
            if (_subscribers.Remove(id, out var channel)) channel.Writer.TryComplete();
    }
}