using Grpc.Core;
using Stonks.Server.MarketData;
using Stonks.Shared.Grpc;

namespace Stonks.Server.Services;

public class OptionsMarketDataService : OptionsMarketData.OptionsMarketDataBase
{
    private readonly IOptionsDataClient optionsDataClient;

    public OptionsMarketDataService(IOptionsDataClient optionsDataClient)
    {
        this.optionsDataClient = optionsDataClient;
    }

    public override async Task<GetOptionExpirationsResponse> GetOptionExpirations(
        GetOptionExpirationsRequest request, ServerCallContext context)
    {
        string ticker = request.Ticker.Trim().ToUpperInvariant();
        try
        {
            var dates = await optionsDataClient.GetExpirationDatesAsync(ticker, context.CancellationToken);
            var response = new GetOptionExpirationsResponse();
            response.ExpirationDates.AddRange(dates);
            return response;
        }
        catch (Exception ex)
        {
            throw ToRpcException(ex);
        }
    }

    public override async Task<GetOptionsChainResponse> GetOptionsChain(
        GetOptionsChainRequest request, ServerCallContext context)
    {
        string ticker = request.Ticker.Trim().ToUpperInvariant();
        if (!DateOnly.TryParse(request.ExpirationDate, out var expiration))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid expiration_date: '{request.ExpirationDate}'."));

        try
        {
            var chain = await optionsDataClient.GetOptionsChainAsync(ticker, expiration, context.CancellationToken);
            var response = new GetOptionsChainResponse { UnderlyingPrice = chain.UnderlyingPrice };
            response.Strikes.AddRange(chain.Strikes.Select(s => new OptionStrike
            {
                Strike = s.Strike,
                CallPremium = s.CallPremium,
                PutPremium = s.PutPremium
            }));
            return response;
        }
        catch (Exception ex)
        {
            throw ToRpcException(ex);
        }
    }

    private static RpcException ToRpcException(Exception ex) => ex switch
    {
        OptionsSubscriptionRequiredException => new RpcException(new Status(StatusCode.PermissionDenied, ex.Message)),
        OptionsDataUnauthorizedException => new RpcException(new Status(StatusCode.Unauthenticated, ex.Message)),
        OptionsDataUnavailableException => new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message)),
        _ => new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message))
    };
}
