namespace Stonks.Server.MarketData;

internal static class PolygonHttpHelper
{
    private static readonly int[] BACKOFF_SECONDS = [1, 2, 4];

    public static async Task<HttpResponseMessage> SendWithRetryAsync(HttpClient httpClient, string url, CancellationToken ct)
    {
        HttpResponseMessage? response = null;
        for (int attempt = 0; attempt <= BACKOFF_SECONDS.Length; attempt++)
        {
            response = await httpClient.GetAsync(url, ct);
            if (response.IsSuccessStatusCode ||
                (response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable &&
                 response.StatusCode != System.Net.HttpStatusCode.TooManyRequests))
                return response;

            if (attempt < BACKOFF_SECONDS.Length)
                await Task.Delay(TimeSpan.FromSeconds(BACKOFF_SECONDS[attempt]), ct);
        }
        return response!;
    }
}
