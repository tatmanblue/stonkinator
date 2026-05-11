using System.Text;
using Grpc.Core;
using Stonks.Server.Ai;
using Stonks.Server.Badges;
using Stonks.Server.MarketData;
using Stonks.Server.Repositories;
using Stonks.Shared.Grpc;

namespace Stonks.Server.Services;

public class StocksAnalysisService : StocksAnalysis.StocksAnalysisBase
{
    private readonly IMarketDataClient marketDataClient;
    private readonly IAiClient aiClient;
    private readonly IAnalysisRepository repository;
    private readonly IBadgeExtractor badgeExtractor;
    private readonly ILogger<StocksAnalysisService> logger;

    public StocksAnalysisService(
        IMarketDataClient marketDataClient,
        IAiClient aiClient,
        IAnalysisRepository repository,
        IBadgeExtractor badgeExtractor,
        ILogger<StocksAnalysisService> logger)
    {
        this.marketDataClient = marketDataClient;
        this.aiClient = aiClient;
        this.repository = repository;
        this.badgeExtractor = badgeExtractor;
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

        var fullText = new StringBuilder();
        bool analysisSucceeded = false;
        try
        {
            await foreach (var chunk in aiClient.AnalyzeAsync(request.Ticker, bars, context.CancellationToken))
            {
                fullText.Append(chunk);
                await responseStream.WriteAsync(new AnalyzeStockResponse { AnalysisChunk = chunk });
            }
            analysisSucceeded = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI analysis failed");
            await responseStream.WriteAsync(new AnalyzeStockResponse { ErrorMessage = ex.Message });
        }

        if (analysisSucceeded && fullText.Length > 0)
        {
            try
            {
                var text = fullText.ToString();
                var badges = badgeExtractor.Extract(text);
                var priceAtClose = bars.Count > 0 ? bars[^1].Close : 0.0;
                var record = new AnalysisRecord(
                    Ticker:       request.Ticker.ToUpperInvariant(),
                    AnalyzedAt:   DateTimeOffset.UtcNow,
                    StartDate:    request.StartDate,
                    EndDate:      request.EndDate,
                    AiResultText: text,
                    Badges:       badges,
                    PriceAtClose: priceAtClose
                );
                await repository.UpsertAsync(record);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist analysis result for {Ticker}", request.Ticker);
            }
        }
    }
}
