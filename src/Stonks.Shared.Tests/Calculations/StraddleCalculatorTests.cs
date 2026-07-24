using Stonks.Shared.Calculations;

namespace Stonks.Shared.Tests.Calculations;

public class StraddleCalculatorTests
{
    // Pinned regression case: RKLB strike 66 row from the source spreadsheet.
    [Fact]
    public void Evaluate_MatchesSpreadsheetRegressionCase()
    {
        var result = StraddleCalculator.Evaluate(strike: 66, callPremium: 6.55, putPremium: 2.43, currentPrice: 69.84);

        Assert.Equal(655, result.CallCost, 2);
        Assert.Equal(243, result.PutCost, 2);
        Assert.Equal(898, result.Straddle, 2);
        Assert.Equal(72.55, result.BreakEvenCall, 2);
        Assert.Equal(63.57, result.BreakEvenPut, 2);
        Assert.Equal(74.98, result.ItmUp, 2);
        Assert.Equal(57.02, result.ItmDown, 2);
        Assert.Equal(0.0388, result.CallPctChange, 4);
        Assert.Equal(0.0898, result.PutPctChange, 4);
    }

    [Theory]
    [InlineData(1.5, 700, 300)]
    [InlineData(3.75, 100, 200)]
    [InlineData(0, 50, 50)]
    public void Evaluate_CostIsPremiumTimesHundred(double callPremium, double putPremium, double currentPrice)
    {
        var result = StraddleCalculator.Evaluate(strike: 100, callPremium, putPremium, currentPrice);

        Assert.Equal(callPremium * 100, result.CallCost);
        Assert.Equal(putPremium * 100, result.PutCost);
        Assert.Equal(result.CallCost + result.PutCost, result.Straddle);
    }

    [Fact]
    public void Evaluate_BreakEvensOffsetStrikeByPremium()
    {
        var result = StraddleCalculator.Evaluate(strike: 50, callPremium: 2, putPremium: 3, currentPrice: 50);

        Assert.Equal(52, result.BreakEvenCall);
        Assert.Equal(47, result.BreakEvenPut);
        Assert.Equal(55, result.ItmUp);
        Assert.Equal(45, result.ItmDown);
    }

    [Fact]
    public void Evaluate_PctChangeSignFlipsAboveAndBelowCurrentPrice()
    {
        // Break-even above current price -> positive call % change (upside needed).
        var callAbove = StraddleCalculator.Evaluate(strike: 100, callPremium: 5, putPremium: 1, currentPrice: 100);
        Assert.True(callAbove.CallPctChange > 0);

        // Break-even below current price -> negative call % change (already past break-even).
        var callBelow = StraddleCalculator.Evaluate(strike: 90, callPremium: 1, putPremium: 1, currentPrice: 100);
        Assert.True(callBelow.CallPctChange < 0);

        // Put break-even below current price -> positive put % change (downside needed).
        var putBelow = StraddleCalculator.Evaluate(strike: 100, callPremium: 1, putPremium: 5, currentPrice: 100);
        Assert.True(putBelow.PutPctChange > 0);
    }

    [Fact]
    public void Evaluate_ZeroCurrentPrice_DoesNotThrowAndReturnsZeroPctChange()
    {
        var result = StraddleCalculator.Evaluate(strike: 50, callPremium: 2, putPremium: 2, currentPrice: 0);

        Assert.Equal(0, result.CallPctChange);
        Assert.Equal(0, result.PutPctChange);
    }
}
