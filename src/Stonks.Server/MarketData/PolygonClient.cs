using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Stonks.Server.Cache;
using Stonks.Shared.Grpc;

namespace Stonks.Server.MarketData;

public class PolygonClient : IMarketDataClient
{
    private const string BASE_URL = "https://api.massive.com/v2/aggs/ticker";

    private readonly HttpClient httpClient;
    private readonly ICacheService cache;
    private readonly string apiKey;

    public PolygonClient(HttpClient httpClient, ICacheService cache)
    {
        this.httpClient = httpClient;
        this.cache = cache;
        apiKey = Environment.GetEnvironmentVariable("STOCK_DATA_API_KEY")
            ?? throw new InvalidOperationException("STOCK_DATA_API_KEY is not set.");
    }

    public async Task<IReadOnlyList<OhlcvBar>> GetOhlcvAsync(
        string ticker, DateOnly startDate, DateOnly endDate,
        CancellationToken ct = default)
    {
        var cacheKey = $"{ticker.ToUpper()}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
        if (cache.TryGet<List<OhlcvBarDto>>(cacheKey, out var cached) && cached is not null)
            return cached.Select(ToProto).ToList();

        var url = $"{BASE_URL}/{ticker.ToUpper()}/range/1/day" +
                  $"/{startDate:yyyy-MM-dd}/{endDate:yyyy-MM-dd}" +
                  $"?adjusted=true&sort=asc&limit=50000&apiKey={apiKey}";

        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<PolygonResponse>(cancellationToken: ct);

        if (raw?.Results is null || raw.Results.Length == 0)
            return [];

        var bars = raw.Results
            .Select(r => new OhlcvBarDto
            {
                Date   = DateTimeOffset.FromUnixTimeMilliseconds(r.T).UtcDateTime.ToString("yyyy-MM-dd"),
                Open   = r.O,
                High   = r.H,
                Low    = r.L,
                Close  = r.C,
                Volume = (long)r.V
            })
            .ToList();

        var ttl = endDate < DateOnly.FromDateTime(DateTime.UtcNow) ? (TimeSpan?)null : TimeSpan.FromHours(1);
        cache.Set(cacheKey, bars, ttl);

        return bars.Select(ToProto).ToList();
    }

    private static OhlcvBar ToProto(OhlcvBarDto dto) => new()
    {
        Date   = dto.Date,
        Open   = dto.Open,
        High   = dto.High,
        Low    = dto.Low,
        Close  = dto.Close,
        Volume = dto.Volume
    };

    private sealed class PolygonResponse
    {
        [JsonPropertyName("results")]  public PolygonBar[]? Results     { get; set; }
        [JsonPropertyName("status")]   public string?       Status      { get; set; }
        [JsonPropertyName("resultsCount")] public int       ResultsCount { get; set; }
    }

    private sealed class PolygonBar
    {
        [JsonPropertyName("o")] public double O { get; set; }
        [JsonPropertyName("h")] public double H { get; set; }
        [JsonPropertyName("l")] public double L { get; set; }
        [JsonPropertyName("c")] public double C { get; set; }
        [JsonPropertyName("v")] public double V { get; set; }
        [JsonPropertyName("t")] public long   T { get; set; }
    }

    private sealed class OhlcvBarDto
    {
        public string Date   { get; set; } = "";
        public double Open   { get; set; }
        public double High   { get; set; }
        public double Low    { get; set; }
        public double Close  { get; set; }
        public long   Volume { get; set; }
    }
}
