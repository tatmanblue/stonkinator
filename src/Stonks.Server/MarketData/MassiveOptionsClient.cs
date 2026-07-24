using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Stonks.Server.Cache;

namespace Stonks.Server.MarketData;

public class MassiveOptionsClient : IOptionsDataClient
{
    private const string BASE_URL = "https://api.massive.com";
    private static readonly TimeSpan EXPIRATIONS_TTL = TimeSpan.FromHours(1);
    private static readonly TimeSpan CHAIN_TTL = TimeSpan.FromMinutes(5);

    private readonly HttpClient httpClient;
    private readonly ICacheService cache;
    private readonly string apiKey;

    public MassiveOptionsClient(HttpClient httpClient, ICacheService cache)
    {
        this.httpClient = httpClient;
        this.cache = cache;
        apiKey = Environment.GetEnvironmentVariable("STOCK_DATA_API_KEY")
            ?? throw new InvalidOperationException("STOCK_DATA_API_KEY is not set.");
    }

    public async Task<IReadOnlyList<string>> GetExpirationDatesAsync(string ticker, CancellationToken ct = default)
    {
        string tickerUpper = ticker.ToUpperInvariant();
        string cacheKey = $"options_expirations_{tickerUpper}";
        if (cache.TryGet<List<string>>(cacheKey, out var cached) && cached is not null)
            return cached;

        string url = $"{BASE_URL}/v3/reference/options/contracts?underlying_ticker={tickerUpper}&limit=1000&apiKey={apiKey}";
        var response = await PolygonHttpHelper.SendWithRetryAsync(httpClient, url, ct);
        await ThrowIfNotSuccessAsync(response, ct);

        var raw = await response.Content.ReadFromJsonAsync<ContractsResponse>(cancellationToken: ct);
        if (raw?.Results is null || raw.Results.Length == 0)
            throw new OptionsDataUnavailableException($"No option contracts found for '{tickerUpper}'.");

        var dates = raw.Results
            .Select(r => r.ExpirationDate)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        cache.Set(cacheKey, dates, EXPIRATIONS_TTL);
        return dates!;
    }

    public async Task<OptionsChainResult> GetOptionsChainAsync(string ticker, DateOnly expiration, CancellationToken ct = default)
    {
        string tickerUpper = ticker.ToUpperInvariant();
        string expirationStr = expiration.ToString("yyyy-MM-dd");
        string cacheKey = $"options_chain_{tickerUpper}_{expiration:yyyyMMdd}";
        if (cache.TryGet<OptionsChainResult>(cacheKey, out var cached) && cached is not null)
            return cached;

        string url = $"{BASE_URL}/v3/snapshot/options/{tickerUpper}?expiration_date={expirationStr}&limit=250&apiKey={apiKey}";
        var response = await PolygonHttpHelper.SendWithRetryAsync(httpClient, url, ct);
        await ThrowIfNotSuccessAsync(response, ct);

        var raw = await response.Content.ReadFromJsonAsync<ChainSnapshotResponse>(cancellationToken: ct);
        if (raw?.Results is null || raw.Results.Length == 0)
            throw new OptionsDataUnavailableException($"No option chain data found for '{tickerUpper}' expiring {expirationStr}.");

        double underlyingPrice = raw.Results
            .Select(r => r.UnderlyingAsset?.Price ?? 0)
            .FirstOrDefault(p => p > 0);

        var byStrike = raw.Results
            .Where(r => r.Details is not null)
            .GroupBy(r => r.Details!.StrikePrice);

        var strikes = new List<OptionStrikeData>();
        foreach (var group in byStrike)
        {
            double callPremium = ExtractPremium(group.FirstOrDefault(r =>
                string.Equals(r.Details!.ContractType, "call", StringComparison.OrdinalIgnoreCase)));
            double putPremium = ExtractPremium(group.FirstOrDefault(r =>
                string.Equals(r.Details!.ContractType, "put", StringComparison.OrdinalIgnoreCase)));

            strikes.Add(new OptionStrikeData(group.Key, callPremium, putPremium));
        }

        var result = new OptionsChainResult(
            underlyingPrice,
            strikes.OrderBy(s => s.Strike).ToList());

        cache.Set(cacheKey, result, CHAIN_TTL);
        return result;
    }

    private static double ExtractPremium(OptionContractSnapshot? contract)
    {
        if (contract is null) return 0;
        if (contract.Day?.Close is > 0) return contract.Day.Close.Value;
        if (contract.LastQuote is { Bid: > 0, Ask: > 0 })
            return (contract.LastQuote.Bid.Value + contract.LastQuote.Ask.Value) / 2;
        return 0;
    }

    private static async Task ThrowIfNotSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                throw new OptionsDataUnauthorizedException("Options data provider returned 401 Unauthorized.");
            case HttpStatusCode.Forbidden:
                throw new OptionsSubscriptionRequiredException(
                    "Options data requires a paid subscription tier that is not enabled for this API key.");
            default:
                string body = await response.Content.ReadAsStringAsync(ct);
                throw new OptionsDataUnavailableException(
                    $"Options data request failed with status {(int)response.StatusCode}: {body}");
        }
    }

    private sealed class ContractsResponse
    {
        [JsonPropertyName("results")] public ContractResult[]? Results { get; set; }
    }

    private sealed class ContractResult
    {
        [JsonPropertyName("expiration_date")] public string? ExpirationDate { get; set; }
    }

    private sealed class ChainSnapshotResponse
    {
        [JsonPropertyName("results")] public OptionContractSnapshot[]? Results { get; set; }
    }

    private sealed class OptionContractSnapshot
    {
        [JsonPropertyName("details")] public ContractDetails? Details { get; set; }
        [JsonPropertyName("day")] public DaySummary? Day { get; set; }
        [JsonPropertyName("last_quote")] public LastQuote? LastQuote { get; set; }
        [JsonPropertyName("underlying_asset")] public UnderlyingAsset? UnderlyingAsset { get; set; }
    }

    private sealed class ContractDetails
    {
        [JsonPropertyName("strike_price")] public double StrikePrice { get; set; }
        [JsonPropertyName("contract_type")] public string? ContractType { get; set; }
    }

    private sealed class DaySummary
    {
        [JsonPropertyName("close")] public double? Close { get; set; }
    }

    private sealed class LastQuote
    {
        [JsonPropertyName("bid")] public double? Bid { get; set; }
        [JsonPropertyName("ask")] public double? Ask { get; set; }
    }

    private sealed class UnderlyingAsset
    {
        [JsonPropertyName("price")] public double? Price { get; set; }
    }
}
