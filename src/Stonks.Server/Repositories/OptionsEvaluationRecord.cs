namespace Stonks.Server.Repositories;

public record OptionsEvaluationStrikeRow(double Strike, double CallPremium, double PutPremium);

public record OptionsEvaluationRecord(
    long Id,
    string Ticker,
    string ExpirationDate,
    double CurrentPrice,
    double Commission,
    DateTimeOffset SavedAt,
    IReadOnlyList<OptionsEvaluationStrikeRow> Strikes);
