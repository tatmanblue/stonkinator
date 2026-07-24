namespace Stonks.Server.MarketData;

public interface IOptionsDataClient
{
    Task<IReadOnlyList<string>> GetExpirationDatesAsync(string ticker, CancellationToken ct = default);

    Task<OptionsChainResult> GetOptionsChainAsync(string ticker, DateOnly expiration, CancellationToken ct = default);
}

public sealed record OptionsChainResult(double UnderlyingPrice, IReadOnlyList<OptionStrikeData> Strikes);

public sealed record OptionStrikeData(double Strike, double CallPremium, double PutPremium);
