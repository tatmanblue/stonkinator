namespace Stonks.Server.Repositories;

public record AnalysisRecord(
    string Ticker,
    DateTimeOffset AnalyzedAt,
    string StartDate,
    string EndDate,
    string AiResultText,
    IReadOnlyList<string> Badges,
    double PriceAtClose
);
