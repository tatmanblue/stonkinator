using Stonks.Shared.Grpc;

namespace Stonks.Server.Ai;

public interface IAiClient
{
    IAsyncEnumerable<string> AnalyzeAsync(
        string ticker, IReadOnlyList<OhlcvBar> bars,
        CancellationToken ct = default);

    IAsyncEnumerable<string> AskFollowUpAsync(
        string ticker, string analysisText, IReadOnlyList<QaTurn> priorTurns,
        string question, IReadOnlyList<OhlcvBar> bars,
        CancellationToken ct = default);
}
