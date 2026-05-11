using System.Text.Json;
using Stonks.Server.Data;

namespace Stonks.Server.Repositories;

public class AnalysisRepository : IAnalysisRepository
{
    private readonly IDatabase database;

    public AnalysisRepository(IDatabase database)
    {
        this.database = database;
    }

    public async Task UpsertAsync(AnalysisRecord record, int maxItems = 25)
    {
        var ticker = record.Ticker.ToUpperInvariant();
        var badgesJson = JsonSerializer.Serialize(record.Badges);
        var analyzedAt = record.AnalyzedAt.ToString("o");

        var parameters = new Dictionary<string, object?>
        {
            ["@ticker"]         = ticker,
            ["@analyzed_at"]    = analyzedAt,
            ["@start_date"]     = record.StartDate,
            ["@end_date"]       = record.EndDate,
            ["@ai_result_text"] = record.AiResultText,
            ["@badges_json"]    = badgesJson,
            ["@price_at_close"] = record.PriceAtClose,
        };

        var existing = await database.QuerySingleAsync(
            "SELECT COUNT(*) FROM stonks_analysis_history WHERE ticker = @ticker",
            new Dictionary<string, object?> { ["@ticker"] = ticker },
            r => r.GetInt32(0));

        if (existing == 0)
        {
            var count = await database.QuerySingleAsync(
                "SELECT COUNT(*) FROM stonks_analysis_history",
                null,
                r => r.GetInt32(0));

            if (count >= maxItems)
            {
                await database.ExecuteAsync(
                    @"DELETE FROM stonks_analysis_history
                      WHERE id = (
                        SELECT id FROM stonks_analysis_history
                        ORDER BY analyzed_at ASC LIMIT 1)");
            }
        }

        await database.ExecuteAsync(
            @"INSERT INTO stonks_analysis_history
                (ticker, analyzed_at, start_date, end_date, ai_result_text, badges_json, price_at_close)
              VALUES
                (@ticker, @analyzed_at, @start_date, @end_date, @ai_result_text, @badges_json, @price_at_close)
              ON CONFLICT(ticker) DO UPDATE SET
                analyzed_at    = excluded.analyzed_at,
                start_date     = excluded.start_date,
                end_date       = excluded.end_date,
                ai_result_text = excluded.ai_result_text,
                badges_json    = excluded.badges_json,
                price_at_close = excluded.price_at_close",
            parameters);
    }

    public async Task<IReadOnlyList<AnalysisRecord>> GetRecentAsync(int limit = 25)
    {
        return await database.QueryAsync(
            @"SELECT ticker, analyzed_at, start_date, end_date, ai_result_text, badges_json, price_at_close
              FROM stonks_analysis_history
              ORDER BY analyzed_at DESC
              LIMIT @limit",
            new Dictionary<string, object?> { ["@limit"] = limit },
            MapRecord);
    }

    public async Task<AnalysisRecord?> GetByTickerAsync(string ticker)
    {
        return await database.QuerySingleAsync(
            @"SELECT ticker, analyzed_at, start_date, end_date, ai_result_text, badges_json, price_at_close
              FROM stonks_analysis_history WHERE ticker = @ticker",
            new Dictionary<string, object?> { ["@ticker"] = ticker.ToUpperInvariant() },
            MapRecord);
    }

    private static AnalysisRecord MapRecord(System.Data.IDataReader r)
    {
        var badges = JsonSerializer.Deserialize<List<string>>(r.GetString(5)) ?? [];
        return new AnalysisRecord(
            Ticker:       r.GetString(0),
            AnalyzedAt:   DateTimeOffset.Parse(r.GetString(1)),
            StartDate:    r.GetString(2),
            EndDate:      r.GetString(3),
            AiResultText: r.GetString(4),
            Badges:       badges,
            PriceAtClose: r.GetDouble(6)
        );
    }
}
