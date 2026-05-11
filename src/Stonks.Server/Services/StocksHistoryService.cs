using Grpc.Core;
using Stonks.Server.Repositories;
using Stonks.Shared.Grpc;

namespace Stonks.Server.Services;

public class StocksHistoryService : StocksHistory.StocksHistoryBase
{
    private readonly IAnalysisRepository repository;

    public StocksHistoryService(IAnalysisRepository repository)
    {
        this.repository = repository;
    }

    public override async Task<GetAnalysisHistoryResponse> GetAnalysisHistory(
        GetAnalysisHistoryRequest request, ServerCallContext context)
    {
        var records = await repository.GetRecentAsync(25);
        var response = new GetAnalysisHistoryResponse();
        foreach (var r in records)
        {
            var item = new AnalysisHistoryItem
            {
                Ticker        = r.Ticker,
                AnalyzedAt    = r.AnalyzedAt.ToString("o"),
                StartDate     = r.StartDate,
                EndDate       = r.EndDate,
                AiResultText  = r.AiResultText,
                PriceAtClose  = r.PriceAtClose,
            };
            item.Badges.AddRange(r.Badges);
            response.Items.Add(item);
        }
        return response;
    }

    public override Task<ReanalyzeStockResponse> ReanalyzeStock(
        ReanalyzeStockRequest request, ServerCallContext context)
        => Task.FromResult(new ReanalyzeStockResponse { Queued = true });

    public override async Task<ReanalyzeAllResponse> ReanalyzeAll(
        ReanalyzeAllRequest request, ServerCallContext context)
    {
        var records = await repository.GetRecentAsync(25);
        return new ReanalyzeAllResponse { QueuedCount = records.Count };
    }
}
