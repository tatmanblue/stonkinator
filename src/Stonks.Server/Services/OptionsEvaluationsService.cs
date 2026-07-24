using Grpc.Core;
using Stonks.Server.Repositories;
using Stonks.Shared.Grpc;

namespace Stonks.Server.Services;

public class OptionsEvaluationsService : OptionsEvaluations.OptionsEvaluationsBase
{
    private readonly IOptionsEvaluationRepository repository;

    public OptionsEvaluationsService(IOptionsEvaluationRepository repository)
    {
        this.repository = repository;
    }

    public override async Task<SaveOptionsEvaluationResponse> SaveOptionsEvaluation(
        SaveOptionsEvaluationRequest request, ServerCallContext context)
    {
        var record = new OptionsEvaluationRecord(
            Id: 0,
            Ticker: request.Ticker.Trim().ToUpperInvariant(),
            ExpirationDate: request.ExpirationDate,
            CurrentPrice: request.CurrentPrice,
            Commission: request.Commission,
            SavedAt: DateTimeOffset.UtcNow,
            Strikes: request.Strikes
                .Select(s => new OptionsEvaluationStrikeRow(s.Strike, s.CallPremium, s.PutPremium))
                .ToList());

        var id = await repository.UpsertAsync(record);
        return new SaveOptionsEvaluationResponse { Id = id };
    }

    public override async Task<GetOptionsEvaluationsResponse> GetOptionsEvaluations(
        GetOptionsEvaluationsRequest request, ServerCallContext context)
    {
        var records = await repository.GetAllAsync();
        var response = new GetOptionsEvaluationsResponse();
        foreach (var r in records)
        {
            var item = new OptionsEvaluationItem
            {
                Id = r.Id,
                Ticker = r.Ticker,
                ExpirationDate = r.ExpirationDate,
                CurrentPrice = r.CurrentPrice,
                Commission = r.Commission,
                SavedAt = r.SavedAt.ToString("o"),
            };
            item.Strikes.AddRange(r.Strikes.Select(s => new OptionEvaluationStrikeRow
            {
                Strike = s.Strike,
                CallPremium = s.CallPremium,
                PutPremium = s.PutPremium
            }));
            response.Items.Add(item);
        }
        return response;
    }

    public override async Task<DeleteOptionsEvaluationResponse> DeleteOptionsEvaluation(
        DeleteOptionsEvaluationRequest request, ServerCallContext context)
    {
        var deleted = await repository.DeleteAsync(request.Id);
        return new DeleteOptionsEvaluationResponse { Deleted = deleted };
    }
}
