using System.Text.Json;
using Stonks.Server.Data;

namespace Stonks.Server.Repositories;

public class OptionsEvaluationRepository : IOptionsEvaluationRepository
{
    private readonly IDatabase database;

    public OptionsEvaluationRepository(IDatabase database)
    {
        this.database = database;
    }

    public async Task<long> UpsertAsync(OptionsEvaluationRecord record)
    {
        var ticker = record.Ticker.ToUpperInvariant();
        var strikesJson = JsonSerializer.Serialize(record.Strikes);
        var savedAt = record.SavedAt.ToString("o");

        var parameters = new Dictionary<string, object?>
        {
            ["@ticker"]          = ticker,
            ["@expiration_date"] = record.ExpirationDate,
            ["@current_price"]   = record.CurrentPrice,
            ["@commission"]      = record.Commission,
            ["@saved_at"]        = savedAt,
            ["@strikes_json"]    = strikesJson,
        };

        await database.ExecuteAsync(
            @"INSERT INTO stonks_options_evaluations
                (ticker, expiration_date, current_price, commission, saved_at, strikes_json)
              VALUES
                (@ticker, @expiration_date, @current_price, @commission, @saved_at, @strikes_json)
              ON CONFLICT(ticker, expiration_date) DO UPDATE SET
                current_price = excluded.current_price,
                commission    = excluded.commission,
                saved_at      = excluded.saved_at,
                strikes_json  = excluded.strikes_json",
            parameters);

        return await database.QuerySingleAsync(
            "SELECT id FROM stonks_options_evaluations WHERE ticker = @ticker AND expiration_date = @expiration_date",
            new Dictionary<string, object?>
            {
                ["@ticker"]          = ticker,
                ["@expiration_date"] = record.ExpirationDate,
            },
            r => r.GetInt64(0));
    }

    public async Task<IReadOnlyList<OptionsEvaluationRecord>> GetAllAsync(int limit = 50)
    {
        return await database.QueryAsync(
            @"SELECT id, ticker, expiration_date, current_price, commission, saved_at, strikes_json
              FROM stonks_options_evaluations
              ORDER BY saved_at DESC
              LIMIT @limit",
            new Dictionary<string, object?> { ["@limit"] = limit },
            MapRecord);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var rows = await database.ExecuteAsync(
            "DELETE FROM stonks_options_evaluations WHERE id = @id",
            new Dictionary<string, object?> { ["@id"] = id });
        return rows > 0;
    }

    private static OptionsEvaluationRecord MapRecord(System.Data.IDataReader r)
    {
        var strikes = JsonSerializer.Deserialize<List<OptionsEvaluationStrikeRow>>(r.GetString(6)) ?? [];
        return new OptionsEvaluationRecord(
            Id:             r.GetInt64(0),
            Ticker:         r.GetString(1),
            ExpirationDate: r.GetString(2),
            CurrentPrice:   r.GetDouble(3),
            Commission:     r.GetDouble(4),
            SavedAt:        DateTimeOffset.Parse(r.GetString(5)),
            Strikes:        strikes
        );
    }
}
