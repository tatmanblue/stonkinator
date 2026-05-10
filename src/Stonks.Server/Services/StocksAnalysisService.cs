using Grpc.Core;
using Stonks.Server.Ai;
using Stonks.Server.MarketData;
using Stonks.Shared.Grpc;

namespace Stonks.Server.Services;

public class StocksAnalysisService : StocksAnalysis.StocksAnalysisBase
{
    private readonly IMarketDataClient marketDataClient;
    private readonly IAiClient aiClient;
    private readonly ILogger<StocksAnalysisService> logger;

    public StocksAnalysisService(
        IMarketDataClient marketDataClient,
        IAiClient aiClient,
        ILogger<StocksAnalysisService> logger)
    {
        this.marketDataClient = marketDataClient;
        this.aiClient = aiClient;
        this.logger = logger;
    }

    public override async Task AnalyzeStock(
        AnalyzeStockRequest request,
        IServerStreamWriter<AnalyzeStockResponse> responseStream,
        ServerCallContext context)
    {
        logger.LogInformation("AnalyzeStock: {Ticker} {Start} → {End}",
            request.Ticker, request.StartDate, request.EndDate);

        IReadOnlyList<Stonks.Shared.Grpc.OhlcvBar> bars;
        try
        {
            var start = DateOnly.Parse(request.StartDate);
            var end   = DateOnly.Parse(request.EndDate);
            bars = await marketDataClient.GetOhlcvAsync(request.Ticker, start, end, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch OHLCV data");
            await responseStream.WriteAsync(new AnalyzeStockResponse { ErrorMessage = ex.Message });
            return;
        }

        var ohlcvData = new OhlcvData { Ticker = request.Ticker };
        ohlcvData.Bars.AddRange(bars);
        await responseStream.WriteAsync(new AnalyzeStockResponse { OhlcvData = ohlcvData });

        try
        {
            await foreach (var chunk in aiClient.AnalyzeAsync(request.Ticker, bars, context.CancellationToken))
            {
                await responseStream.WriteAsync(new AnalyzeStockResponse { AnalysisChunk = chunk });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI analysis failed");
            await responseStream.WriteAsync(new AnalyzeStockResponse { ErrorMessage = ex.Message });
        }
    }
}
