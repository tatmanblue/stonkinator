using System.Data;

namespace Stonks.Server.Data;

public interface IDatabase
{
    Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null);
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, IReadOnlyDictionary<string, object?>? parameters, Func<IDataReader, T> mapper);
    Task<T?> QuerySingleAsync<T>(string sql, IReadOnlyDictionary<string, object?>? parameters, Func<IDataReader, T> mapper);
    Task EnsureSchemaAsync();
}
