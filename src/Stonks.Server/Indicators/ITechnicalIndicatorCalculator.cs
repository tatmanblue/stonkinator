using Stonks.Shared.Grpc;

namespace Stonks.Server.Indicators;

public interface ITechnicalIndicatorCalculator
{
    // bars must be in chronological (oldest-first) order.
    TechnicalIndicators Calculate(IReadOnlyList<OhlcvBar> bars);
}
