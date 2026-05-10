using System.Net.Http.Json;
using System.Text.Json;
using Stonks.Server.Cache;
using Stonks.Shared.Grpc;

namespace Stonks.Server.MarketData;

public class FinnhubClient : IMarketDataClient
{
    private const string BASE_URL = "https://finnhub.io/api/v1/stock/candle";

    private readonly HttpClient httpClient;
    private readonly ICacheService cache;
    private readonly string apiKey;

    public FinnhubClient(HttpClient httpClient, ICacheService cache)
    {
        this.httpClient = httpClient;
        this.cache = cache;
        apiKey = Environment.GetEnvironmentVariable("FINNHUB_API_KEY")
            ?? throw new InvalidOperationException("FINNHUB_API_KEY is not set.");
    }

    public async Task<IReadOnlyList<OhlcvBar>> GetOhlcvAsync(
        string ticker, DateOnly startDate, DateOnly endDate,
        CancellationToken ct = default)
    {
        var cacheKey = $"{ticker.ToUpper()}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
        if (cache.TryGet<List<OhlcvBarDto>>(cacheKey, out var cached) && cached is not null)
            return cached.Select(ToProto).ToList();

        var from = ToUnixSeconds(startDate);
        var to   = ToUnixSeconds(endDate);
        var url  = $"{BASE_URL}?symbol={ticker}&resolution=D&from={from}&to={to}&token={apiKey}";

        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<FinnhubResponse>(
            cancellationToken: ct);

        if (raw is null || raw.S != "ok" || raw.T is null)
            return [];

        var bars = Enumerable.Range(0, raw.T.Length)
            .Select(i => new OhlcvBarDto
            {
                Date   = DateTimeOffset.FromUnixTimeSeconds(raw.T[i]).UtcDateTime.ToString("yyyy-MM-dd"),
                Open   = raw.O![i],
                High   = raw.H![i],
                Low    = raw.L![i],
                Close  = raw.C![i],
                Volume = raw.V![i]
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

    private static long ToUnixSeconds(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();

    private sealed class FinnhubResponse
    {
        public double[]? C { get; set; }
        public double[]? H { get; set; }
        public double[]? L { get; set; }
        public double[]? O { get; set; }
        public string?   S { get; set; }
        public long[]?   T { get; set; }
        public long[]?   V { get; set; }
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
