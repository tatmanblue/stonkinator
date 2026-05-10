using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stonks.Server.Cache;
using Stonks.Shared.Grpc;

namespace Stonks.Server.Ai;

public class GeminiClient : IAiClient
{
    private const string ENDPOINT =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:streamGenerateContent";

    private readonly HttpClient httpClient;
    private readonly ICacheService cache;
    private readonly string apiKey;
    private readonly bool cacheResults;

    public GeminiClient(HttpClient httpClient, ICacheService cache)
    {
        this.httpClient = httpClient;
        this.cache = cache;
        apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException("GEMINI_API_KEY is not set.");
        cacheResults = string.Equals(
            Environment.GetEnvironmentVariable("CACHE_AI_RESULTS"), "true",
            StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<string> AnalyzeAsync(
        string ticker, IReadOnlyList<OhlcvBar> bars,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var prompt = BuildPrompt(ticker, bars);
        var promptHash = ComputeHash(prompt);
        var startDate = bars.FirstOrDefault()?.Date ?? "";
        var endDate   = bars.LastOrDefault()?.Date  ?? "";
        var cacheKey  = $"ai_{ticker.ToUpper()}_{startDate}_{endDate}_{promptHash}";

        if (cacheResults && cache.TryGet<string>(cacheKey, out var cached) && cached is not null)
        {
            yield return cached;
            yield break;
        }

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };
        var json = JsonSerializer.Serialize(requestBody);
        var url  = $"{ENDPOINT}?key={apiKey}&alt=sse";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var fullText = new StringBuilder();
        var buffer   = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            if (!line.StartsWith("data:"))
            {
                if (line.Length == 0 && buffer.Length > 0)
                {
                    var chunk = TryExtractText(buffer.ToString());
                    if (chunk is not null)
                    {
                        fullText.Append(chunk);
                        yield return chunk;
                    }
                    buffer.Clear();
                }
                continue;
            }

            buffer.AppendLine(line["data:".Length..].TrimStart());
        }

        // flush any remaining buffer
        if (buffer.Length > 0)
        {
            var chunk = TryExtractText(buffer.ToString());
            if (chunk is not null)
            {
                fullText.Append(chunk);
                yield return chunk;
            }
        }

        if (cacheResults && fullText.Length > 0)
            cache.Set(cacheKey, fullText.ToString());
    }

    private static string? TryExtractText(string jsonFragment)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonFragment.Trim());
            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildPrompt(string ticker, IReadOnlyList<OhlcvBar> bars)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are an expert technical analyst. Analyze the following daily OHLCV data for {ticker}");
        sb.AppendLine("and provide a concise technical analysis covering: trend direction, key support/resistance");
        sb.AppendLine("levels, notable patterns, and a brief outlook.");
        sb.AppendLine();
        sb.AppendLine($"Ticker: {ticker}");
        if (bars.Count > 0)
            sb.AppendLine($"Date Range: {bars[0].Date} to {bars[^1].Date}");
        sb.AppendLine();
        sb.AppendLine("Date,Open,High,Low,Close,Volume");
        foreach (var bar in bars.Take(500))
            sb.AppendLine($"{bar.Date},{bar.Open},{bar.High},{bar.Low},{bar.Close},{bar.Volume}");
        sb.AppendLine();
        sb.AppendLine("Provide your analysis in clear, structured paragraphs.");
        return sb.ToString();
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..8];
    }
}
