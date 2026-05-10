using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stonks.Server.Cache;
using Stonks.Shared.Grpc;

namespace Stonks.Server.Ai;

public class GeminiClient : IAiClient
{
    private const string BASE_ENDPOINT =
        "https://generativelanguage.googleapis.com/v1beta/models/{0}:streamGenerateContent";

    private const int MAX_RETRIES = 3;
    private const int BASE_RETRY_DELAY_MS = 10000;

    private readonly HttpClient httpClient;
    private readonly ICacheService cache;
    private readonly ILogger<GeminiClient> logger;
    private readonly string apiKey;
    private readonly string model;
    private readonly bool cacheResults;

    public GeminiClient(HttpClient httpClient, ICacheService cache, ILogger<GeminiClient> logger)
    {
        this.httpClient = httpClient;
        this.cache = cache;
        this.logger = logger;
        apiKey = Environment.GetEnvironmentVariable("AI_API_KEY")
            ?? throw new InvalidOperationException("AI_API_KEY is not set.");
        model = Environment.GetEnvironmentVariable("AI_MODEL") ?? "gemini-2.0-flash";
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

        var response = await SendWithRetryAsync(prompt, ct);

        try
        {
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
        finally
        {
            response.Dispose();
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        };
        var json = JsonSerializer.Serialize(requestBody);
        var url  = $"{string.Format(BASE_ENDPOINT, model)}?key={apiKey}&alt=sse";

        logger.LogInformation("Sending request to Gemini model: {Model}", model);

        for (int attempt = 0; attempt <= MAX_RETRIES; attempt++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MAX_RETRIES)
            {
                // Respect Retry-After if present, but enforce a minimum delay
                var retryAfter = response.Headers.RetryAfter?.Delta;
                var minDelay   = TimeSpan.FromMilliseconds(BASE_RETRY_DELAY_MS * Math.Pow(2, attempt));
                var delay      = retryAfter.HasValue && retryAfter.Value > minDelay ? retryAfter.Value : minDelay;

                logger.LogWarning(
                    "Gemini 429 on attempt {Attempt}/{Max}. Retrying in {Delay}s.",
                    attempt + 1, MAX_RETRIES + 1, delay.TotalSeconds);

                response.Dispose();
                await Task.Delay(delay, ct);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return response;
        }

        throw new HttpRequestException($"Gemini returned 429 after {MAX_RETRIES + 1} attempts. Check quota at https://aistudio.google.com");
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
