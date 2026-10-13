using System.Diagnostics;
using TransitRealtime;

var url = args.Length > 0
    ? args[0]
    : "https://api.entur.io/realtime/v1/gtfs-rt/vehicle-positions";
var minutes = args.Length > 1 ? double.Parse(args[1]) : 5;
const int intervalSeconds = 3;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
http.DefaultRequestHeaders.Add("ET-Client-Name", "efe-feedobserver");

var lastSeen = new Dictionary<string, ulong>();
var ages = new List<double>();
var durationsMs = new List<long>();
var sizesKb = new List<double>();
var changedPerPoll = new List<int>();

int polls = 0, failures = 0;
long dupInFeed = 0, headerRegress = 0, over1h = 0, over3h = 0;
var vehicles = new HashSet<string>();
long badRange = 0, zeroCoord = 0;
ulong prevHeader = 0;
var feedVersions = new HashSet<ulong>();
var sampleShown = 0;
long entities = 0, changedTotal = 0, unchanged = 0, outOfOrder = 0;
long noVehicle = 0, noId = 0, noPosition = 0, noTimestamp = 0, badCoord = 0, futureTs = 0;

var end = DateTime.UtcNow.AddMinutes(minutes);
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

Console.WriteLine($"Okuma basliyor: {url}");
Console.WriteLine($"Sure: {minutes} dk, aralik: {intervalSeconds} sn\n");

do
{
    polls++;
    var sw = Stopwatch.StartNew();
    try
    {
        var bytes = await http.GetByteArrayAsync(url);
        var feed = FeedMessage.Parser.ParseFrom(bytes);
        sw.Stop();

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int changed = 0;
        var byId = new Dictionary<string, List<string>>();
        if (prevHeader != 0 && feed.Header.Timestamp < prevHeader) headerRegress++;
        prevHeader = feed.Header.Timestamp;
        feedVersions.Add(feed.Header.Timestamp);

        foreach (var e in feed.Entity)
        {
            entities++;
            if (e.Vehicle == null) { noVehicle++; continue; }

            var v = e.Vehicle;
            var id = v.Vehicle?.Id;
            if (string.IsNullOrEmpty(id)) { noId++; id = e.Id; }

            if (v.Position == null) { noPosition++; continue; }
            if (!v.HasTimestamp) { noTimestamp++; continue; }

            var lat = v.Position.Latitude;
            var lon = v.Position.Longitude;
            //if (lat is < -90 or > 90 || lon is < -180 or > 180 || (lat == 0 && lon == 0))
            //{
            //    badCoord++;
            //    continue;
            //}
            if (lat == 0 && lon == 0) { zeroCoord++; badCoord++; continue; }
            if (lat is < -90 or > 90 ||lon is < -180 or > 180) { badRange++;badCoord++; continue; }
            
            var ts = v.Timestamp;
            var age = nowUnix - (long)ts;
            if (age < -5) futureTs++;
            ages.Add(age);
            if (age > 3600) over1h++;
            if (age > 10800) over3h++;
            if (!byId.TryGetValue(id, out var list)) byId[id] = list = new List<string>();
            list.Add($"entity={e.Id} label={v.Vehicle?.Label} trip={v.Trip?.TripId} route={v.Trip?.RouteId} ts={ts}");

            vehicles.Add(id);
            var key = e.Id;
            if (lastSeen.TryGetValue(id, out var prev))
            {
                if (prev == ts) { unchanged++; continue; }
                if (ts < prev) { outOfOrder++; continue; }
            }
            lastSeen[key] = ts;
            if (age <= 10800) { changed++; changedTotal++; }
            //lastSeen[id] = ts;
            //changed++;
            //changedTotal++;
        }
        var dups = byId.Where(kv => kv.Value.Count > 1).ToList();
        dupInFeed += dups.Count;
        if (dups.Count > 0 && sampleShown < 3)
        {
            sampleShown++;
            Console.WriteLine($"   ORNEK tekrar eden id: {dups[0].Key}");
            foreach (var s in dups[0].Value.Take(4)) Console.WriteLine("     " + s);
        }
        durationsMs.Add(sw.ElapsedMilliseconds);
        sizesKb.Add(bytes.Length / 1024.0);
        changedPerPoll.Add(changed);

        var headerTs = DateTimeOffset.FromUnixTimeSeconds((long)feed.Header.Timestamp);
        Console.WriteLine(
            $"#{polls,3} | {sw.ElapsedMilliseconds,5} ms | {bytes.Length / 1024,5} KB | " +
            $"varlik: {feed.Entity.Count,6} | yeni konum: {changed,6} | header: {headerTs:HH:mm:ss}Z");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"#{polls,3} | HATA: {ex.GetType().Name}: {ex.Message}");
    }
}
while (DateTime.UtcNow < end && await timer.WaitForNextTickAsync());

// ---- Ozet ----
ages.Sort();
durationsMs.Sort();
double avgChanged = changedPerPoll.Count > 1 ? changedPerPoll.Skip(1).Average() : 0;
var pollsPerHour = 3600.0 / intervalSeconds;

Console.WriteLine("\n===== OZET =====");
Console.WriteLine($"Okuma: {polls} (basarisiz: {failures})");
Console.WriteLine($"Okuma suresi ms  -> medyan: {P(durationsMs.Select(x => (double)x).ToList(), 0.5):F0}, p95: {P(durationsMs.Select(x => (double)x).ToList(), 0.95):F0}");
Console.WriteLine($"Yanit boyutu KB  -> ortalama: {(sizesKb.Count > 0 ? sizesKb.Average() : 0):F0}");
Console.WriteLine($"Toplam varlik: {entities},farklı varlik id: {lastSeen.Count} ,farkli arac: {vehicles.Count}");
Console.WriteLine($"Gecersiz koordinant -> (0,0): {zeroCoord}, aralik disi: {badRange}");
Console.WriteLine($"Yeni konum: {changedTotal}, ayni konum tekrar: {unchanged}, zamani geri giden: {outOfOrder}");
Console.WriteLine($"Bozuk -> arac yok: {noVehicle}, arac id yok: {noId}, konum yok: {noPosition}, zaman yok: {noTimestamp}, gecersiz koordinat: {badCoord}, gelecekte zaman: {futureTs}");
Console.WriteLine($"Konum yasi (sn)  -> medyan: {P(ages, 0.5):F0}, p95: {P(ages, 0.95):F0}, maks: {P(ages, 1.0):F0}");
Console.WriteLine($"Okuma basina ortalama yeni konum: {avgChanged:F0}");
Console.WriteLine($"Tahmini satir/saat: {avgChanged * pollsPerHour:N0}, 3 saat: {avgChanged * pollsPerHour * 3:N0}");
Console.WriteLine($"Ayni akista tekrar eden arac id (okuma basina toplam): {dupInFeed}");
Console.WriteLine($"Farkli header zamani: {feedVersions.Count}, header geri gitti: {headerRegress}");
Console.WriteLine($"Yas > 1 saat: {over1h}, yas > 3 saat: {over3h} (toplam {ages.Count})");

static double P(List<double> sorted, double p)
{
    if (sorted.Count == 0) return double.NaN;
    sorted.Sort();
    return sorted[(int)Math.Floor(p * (sorted.Count - 1))];
}