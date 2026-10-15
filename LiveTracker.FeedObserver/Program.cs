using System.Diagnostics;
using System.Globalization;
using TransitRealtime;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var url = args.Length > 0
    ? args[0]
    : "https://api.entur.io/realtime/v1/gtfs-rt/vehicle-positions";
var minutes = args.Length > 1
    ? double.Parse(args[1].Replace(',', '.'), CultureInfo.InvariantCulture)
    : 5;
const int intervalSeconds = 3;
const long maxAgeSeconds = 3 * 3600;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
http.DefaultRequestHeaders.Add("ET-Client-Name", "efe-feedobserver");

var lastSeen = new Dictionary<string, ulong>();   // anahtar: varlik id
var vehicles = new HashSet<string>();
var snapshotAges = new List<double>();            // ilk okumadaki konum yaslari
var updateAges = new List<double>();              // sonradan gelen guncellemelerin yasi
var durationsMs = new List<double>();
var sizesKb = new List<double>();

int polls = 0, failures = 0;
bool initialLoaded = false;
ulong prevHeader = 0;
var headers = new HashSet<ulong>();
long headerRegress = 0;
long entities = 0, unchanged = 0, outOfOrder = 0;
long newTotal = 0, newFresh = 0;
long noVehicle = 0, noId = 0, noPosition = 0, noTimestamp = 0, zeroCoord = 0, badRange = 0, futureTs = 0;
long initialEntities = 0;

var clock = new Stopwatch();   // ilk yuklemeden sonra baslar
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

        if (prevHeader != 0 && feed.Header.Timestamp < prevHeader) headerRegress++;
        prevHeader = feed.Header.Timestamp;
        headers.Add(feed.Header.Timestamp);

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int changed = 0, same = 0;

        foreach (var e in feed.Entity)
        {
            entities++;
            if (e.Vehicle == null) { noVehicle++; continue; }
            var v = e.Vehicle;

            if (v.Position == null) { noPosition++; continue; }
            if (!v.HasTimestamp) { noTimestamp++; continue; }

            var vehicleId = v.Vehicle?.Id;
            if (string.IsNullOrEmpty(vehicleId)) { noId++; vehicleId = e.Id; }

            var lat = v.Position.Latitude;
            var lon = v.Position.Longitude;
            if (lat == 0 && lon == 0) { zeroCoord++; continue; }
            if (lat is < -90 or > 90 || lon is < -180 or > 180) { badRange++; continue; }

            var ts = v.Timestamp;
            var age = nowUnix - (long)ts;
            if (age < -5) futureTs++;
            vehicles.Add(vehicleId);

            var key = e.Id;
            if (lastSeen.TryGetValue(key, out var prev))
            {
                if (prev == ts) { unchanged++; same++; continue; }
                if (ts < prev) { outOfOrder++; continue; }
            }
            lastSeen[key] = ts;

            if (!initialLoaded)
            {
                initialEntities++;
                snapshotAges.Add(age);
                continue;
            }

            newTotal++;
            changed++;
            updateAges.Add(age);
            if (age <= maxAgeSeconds) newFresh++;
        }

        if (!initialLoaded) { initialLoaded = true; clock.Start(); }

        durationsMs.Add(sw.ElapsedMilliseconds);
        sizesKb.Add(bytes.Length / 1024.0);

        var headerTs = DateTimeOffset.FromUnixTimeSeconds((long)feed.Header.Timestamp);
        Console.WriteLine(
            $"#{polls,3} | {sw.ElapsedMilliseconds,5} ms | {bytes.Length / 1024,5} KB | " +
            $"varlik: {feed.Entity.Count,6} | yeni: {changed,6} | ayni: {same,6} | header: {headerTs:HH:mm:ss}Z");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"#{polls,3} | HATA: {ex.GetType().Name}: {ex.Message}");
    }
}
while (DateTime.UtcNow < end && await timer.WaitForNextTickAsync());

// ---- Ozet ----
var elapsed = clock.Elapsed.TotalSeconds;
double perHour = elapsed > 0 ? newTotal / elapsed * 3600 : 0;
double freshPerHour = elapsed > 0 ? newFresh / elapsed * 3600 : 0;

Console.WriteLine("\n===== OZET =====");
Console.WriteLine($"Okuma: {polls} (basarisiz: {failures}), olcum suresi (ilk okumadan sonra): {elapsed:F0} sn");
Console.WriteLine($"Okuma suresi ms -> medyan: {P(durationsMs, 0.5):F0}, p95: {P(durationsMs, 0.95):F0}");
Console.WriteLine($"Yanit boyutu KB -> ortalama: {(sizesKb.Count > 0 ? sizesKb.Average() : 0):F0}");
Console.WriteLine($"Varlik (ilk yukleme): {initialEntities}, farkli varlik id: {lastSeen.Count}, farkli arac id: {vehicles.Count}");
Console.WriteLine($"Header: {headers.Count} farkli deger, {headerRegress} kez geri gitti");
Console.WriteLine($"Ilk yukleme sonrasi: yeni konum {newTotal}, ayni konum tekrar {unchanged}, zamani geri giden {outOfOrder}");
Console.WriteLine($"Bozuk -> arac yok: {noVehicle}, arac id yok: {noId}, konum yok: {noPosition}, zaman yok: {noTimestamp}, (0,0): {zeroCoord}, aralik disi: {badRange}, gelecekte zaman: {futureTs}");
Console.WriteLine($"Ilk yukleme konum yasi (sn) -> medyan: {P(snapshotAges, 0.5):F0}, p95: {P(snapshotAges, 0.95):F0}, maks: {P(snapshotAges, 1.0):F0}");
Console.WriteLine($"Guncellemelerin yasi (sn)   -> medyan: {P(updateAges, 0.5):F0}, p95: {P(updateAges, 0.95):F0}, maks: {P(updateAges, 1.0):F0}");
Console.WriteLine($"Yeni konum hizi: {newTotal / Math.Max(elapsed, 1):F1} /sn");
Console.WriteLine($"Tahmini satir/saat (tum yeni konumlar): {perHour:N0}, 3 saat: {perHour * 3:N0}");
Console.WriteLine($"Tahmini satir/saat (yasi <= 3 saat):    {freshPerHour:N0}, 3 saat: {freshPerHour * 3:N0}");

static double P(List<double> values, double p)
{
    if (values.Count == 0) return double.NaN;
    var s = values.OrderBy(x => x).ToList();
    return s[(int)Math.Floor(p * (s.Count - 1))];
}