using System.Text;
using Grpc.Core;
using Stonks.Server.Ai;
using Stonks.Server.Badges;
using Stonks.Server.Indicators;
using Stonks.Server.MarketData;
using Stonks.Server.Repositories;
using Stonks.Shared.Grpc;

namespace Stonks.Server.Services;

public class StocksAnalysisService : StocksAnalysis.StocksAnalysisBase
{
    // Indicators (esp. SMA200) need more history than the user's selected chart range, so
    // indicator math always runs over this much lookback ending at the requested end date,
    // regardless of what range gets streamed to the chart.
    private const int INDICATOR_LOOKBACK_DAYS = 400;

    private readonly IMarketDataClient marketDataClient;
    private readonly IAiClient aiClient;
    private readonly IAnalysisRepository repository;
    private readonly IBadgeExtractor badgeExtractor;
    private readonly ITechnicalIndicatorCalculator indicatorCalculator;
    private readonly ILogger<StocksAnalysisService> logger;

    public StocksAnalysisService(
        IMarketDataClient marketDataClient,
        IAiClient aiClient,
        IAnalysisRepository repository,
        IBadgeExtractor badgeExtractor,
        ITechnicalIndicatorCalculator indicatorCalculator,
        ILogger<StocksAnalysisService> logger)
    {
        this.marketDataClient = marketDataClient;
        this.aiClient = aiClient;
        this.repository = repository;
        this.badgeExtractor = badgeExtractor;
        this.indicatorCalculator = indicatorCalculator;
        this.logger = logger;
    }

    private async Task<TechnicalIndicators> CalculateIndicatorsAsync(
        string ticker, DateOnly endDate, CancellationToken ct)
    {
        var lookbackStart = endDate.AddDays(-INDICATOR_LOOKBACK_DAYS);
        var lookbackBars = await marketDataClient.GetOhlcvAsync(ticker, lookbackStart, endDate, ct);
        return indicatorCalculator.Calculate(lookbackBars);
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
            await responseStream.WriteAsync(new AnalyzeStockResponse { ErrorMessage = $"Market data fetch failed: {ex.Message}" });
            return;
        }

        var ohlcvData = new OhlcvData { Ticker = request.Ticker };
        ohlcvData.Bars.AddRange(bars);
        await responseStream.WriteAsync(new AnalyzeStockResponse { OhlcvData = ohlcvData });

        TechnicalIndicators indicators;
        try
        {
            var end = DateOnly.Parse(request.EndDate);
            indicators = await CalculateIndicatorsAsync(request.Ticker, end, context.CancellationToken);
            await responseStream.WriteAsync(new AnalyzeStockResponse { TechnicalIndicators = indicators });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compute technical indicators for {Ticker}", request.Ticker);
            indicators = new TechnicalIndicators();
        }

        var fullText = new StringBuilder();
        bool analysisSucceeded = false;
        try
        {
            await foreach (var chunk in aiClient.AnalyzeAsync(request.Ticker, bars, indicators, context.CancellationToken))
            {
                fullText.Append(chunk);
                await responseStream.WriteAsync(new AnalyzeStockResponse { AnalysisChunk = chunk });
            }
            analysisSucceeded = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI analysis failed");
            await responseStream.WriteAsync(new AnalyzeStockResponse { ErrorMessage = $"AI analysis failed: {ex.Message}" });
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

    // History items don't persist their bars, so reopening one re-fetches them here rather
    // than through the streaming AnalyzeStock (which would also re-run and re-save the AI analysis).
    public override async Task<GetOhlcvBarsResponse> GetOhlcvBars(
        GetOhlcvBarsRequest request, ServerCallContext context)
    {
        if (!DateOnly.TryParse(request.StartDate, out var start))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid start_date: '{request.StartDate}'."));
        if (!DateOnly.TryParse(request.EndDate, out var end))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid end_date: '{request.EndDate}'."));

        try
        {
            var bars = await marketDataClient.GetOhlcvAsync(request.Ticker, start, end, context.CancellationToken);
            var response = new GetOhlcvBarsResponse();
            response.Bars.AddRange(bars);
            response.Indicators = await CalculateIndicatorsAsync(request.Ticker, end, context.CancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch OHLCV bars for {Ticker}", request.Ticker);
            throw new RpcException(new Status(StatusCode.FailedPrecondition, $"Market data fetch failed: {ex.Message}"));
        }
    }

    // Deliberately never reads or writes `repository` — follow-up Q&A must never be persisted.
    public override async Task AskFollowUp(
        AskFollowUpRequest request,
        IServerStreamWriter<AskFollowUpResponse> responseStream,
        ServerCallContext context)
    {
        logger.LogInformation("AskFollowUp: {Ticker} — {Question}", request.Ticker, request.Question);

        try
        {
            await foreach (var chunk in aiClient.AskFollowUpAsync(
                request.Ticker,
                request.AnalysisText,
                request.PriorTurns,
                request.Question,
                request.IncludeBars ? request.Bars : [],
                context.CancellationToken))
            {
                await responseStream.WriteAsync(new AskFollowUpResponse { AnswerChunk = chunk });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Follow-up AI request failed for {Ticker}", request.Ticker);
            await responseStream.WriteAsync(new AskFollowUpResponse { ErrorMessage = $"Follow-up failed: {ex.Message}" });
        }
    }
}
