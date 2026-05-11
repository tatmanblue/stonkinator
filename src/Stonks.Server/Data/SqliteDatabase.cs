using System.Data;
using Microsoft.Data.Sqlite;

namespace Stonks.Server.Data;

public class SqliteDatabase : IDatabase
{
    private readonly string connectionString;

    public SqliteDatabase()
    {
        var path = Environment.GetEnvironmentVariable("DATABASE_PATH")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Stonks", "stonks.db");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        connectionString = $"Data Source={path}";
    }

    public async Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, parameters);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters,
        Func<IDataReader, T> mapper)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, parameters);
        await using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<T>();
        while (await reader.ReadAsync())
            results.Add(mapper(reader));
        return results;
    }

    public async Task<T?> QuerySingleAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters,
        Func<IDataReader, T> mapper)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, parameters);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return mapper(reader);
        return default;
    }

    public async Task EnsureSchemaAsync()
    {
        await ExecuteAsync(
            @"CREATE TABLE IF NOT EXISTS stonks_analysis_history (
                id              INTEGER  PRIMARY KEY AUTOINCREMENT,
                ticker          TEXT     NOT NULL,
                analyzed_at     TEXT     NOT NULL,
                start_date      TEXT     NOT NULL,
                end_date        TEXT     NOT NULL,
                ai_result_text  TEXT     NOT NULL,
                badges_json     TEXT     NOT NULL,
                price_at_close  REAL     NOT NULL
            )");

        await ExecuteAsync(
            @"CREATE UNIQUE INDEX IF NOT EXISTS stonks_idx_history_ticker
              ON stonks_analysis_history (ticker)");

        await ExecuteAsync(
            @"CREATE INDEX IF NOT EXISTS stonks_idx_history_analyzed_at
              ON stonks_analysis_history (analyzed_at DESC)");
    }

    private static void BindParameters(SqliteCommand cmd, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null) return;
        foreach (var (key, value) in parameters)
            cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
    }
}
