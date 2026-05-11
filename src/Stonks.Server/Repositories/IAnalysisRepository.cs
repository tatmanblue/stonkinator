namespace Stonks.Server.Repositories;

public interface IAnalysisRepository
{
    Task UpsertAsync(AnalysisRecord record, int maxItems = 25);
    Task<IReadOnlyList<AnalysisRecord>> GetRecentAsync(int limit = 25);
    Task<AnalysisRecord?> GetByTickerAsync(string ticker);
}
