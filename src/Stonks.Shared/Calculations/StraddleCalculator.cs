namespace Stonks.Shared.Calculations;

public readonly record struct StrikeEvaluation(
    double CallCost,
    double PutCost,
    double Straddle,
    double BreakEvenCall,
    double BreakEvenPut,
    double ItmUp,
    double ItmDown,
    double CallPctChange,
    double PutPctChange);

public static class StraddleCalculator
{
    public static StrikeEvaluation Evaluate(double strike, double callPremium, double putPremium, double currentPrice)
    {
        double callCost = callPremium * 100;
        double putCost = putPremium * 100;
        double breakEvenCall = strike + callPremium;
        double breakEvenPut = strike - putPremium;

        return new StrikeEvaluation(
            CallCost: callCost,
            PutCost: putCost,
            Straddle: callCost + putCost,
            BreakEvenCall: breakEvenCall,
            BreakEvenPut: breakEvenPut,
            ItmUp: strike + callPremium + putPremium,
            ItmDown: strike - callPremium - putPremium,
            CallPctChange: currentPrice == 0 ? 0 : (breakEvenCall - currentPrice) / currentPrice,
            PutPctChange: currentPrice == 0 ? 0 : (currentPrice - breakEvenPut) / currentPrice);
    }
}
