namespace Stonks.Server.MarketData;

public class UnsupportedOptionsDataClient : IOptionsDataClient
{
    private readonly string providerName;

    public UnsupportedOptionsDataClient(string providerName)
    {
        this.providerName = providerName;
    }

    public Task<IReadOnlyList<string>> GetExpirationDatesAsync(string ticker, CancellationToken ct = default)
        => throw new OptionsDataUnavailableException($"Options data not supported for provider '{providerName}'.");

    public Task<OptionsChainResult> GetOptionsChainAsync(string ticker, DateOnly expiration, CancellationToken ct = default)
        => throw new OptionsDataUnavailableException($"Options data not supported for provider '{providerName}'.");
}
