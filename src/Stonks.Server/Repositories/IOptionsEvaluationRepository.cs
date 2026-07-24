namespace Stonks.Server.Repositories;

public interface IOptionsEvaluationRepository
{
    Task<long> UpsertAsync(OptionsEvaluationRecord record);
    Task<IReadOnlyList<OptionsEvaluationRecord>> GetAllAsync(int limit = 50);
    Task<bool> DeleteAsync(long id);
}
