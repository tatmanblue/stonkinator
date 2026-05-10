using Stonks.Shared.Grpc;

namespace Stonks.Server.MarketData;

public interface IMarketDataClient
{
    Task<IReadOnlyList<OhlcvBar>> GetOhlcvAsync(
        string ticker, DateOnly startDate, DateOnly endDate,
        CancellationToken ct = default);
}
