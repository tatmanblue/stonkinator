using Stonks.Server.Indicators;
using Stonks.Shared.Grpc;

namespace Stonks.Server.Tests.Indicators;

public class TechnicalIndicatorCalculatorTests
{
    private readonly TechnicalIndicatorCalculator calculator = new();

    private static OhlcvBar Bar(int dayOffset, double open, double high, double low, double close) => new()
    {
        Date   = new DateOnly(2024, 1, 1).AddDays(dayOffset).ToString("yyyy-MM-dd"),
        Open   = open,
        High   = high,
        Low    = low,
        Close  = close,
        Volume = 1000,
    };

    // Chronological series where the close rises by 1 each day, starting at `startClose`.
    private static List<OhlcvBar> RisingSeries(int count, double startClose = 1)
    {
        var bars = new List<OhlcvBar>();
        for (int i = 0; i < count; i++)
        {
            double close = startClose + i;
            bars.Add(Bar(i, close, close, close, close));
        }
        return bars;
    }

    // Chronological series where the close falls by 1 each day, starting at `startClose`.
    private static List<OhlcvBar> FallingSeries(int count, double startClose)
    {
        var bars = new List<OhlcvBar>();
        for (int i = 0; i < count; i++)
        {
            double close = startClose - i;
            bars.Add(Bar(i, close, close, close, close));
        }
        return bars;
    }

    [Fact]
    public void Calculate_WithNoBars_ReturnsEmptyResult()
    {
        var result = calculator.Calculate([]);

        Assert.Empty(result.MovingAverages);
        Assert.False(result.HasRsi);
        Assert.False(result.HasMacd);
        Assert.False(result.HasStochastic);
        Assert.False(result.HasWilliamsR);
        Assert.Empty(result.SupportLevels);
        Assert.Empty(result.ResistanceLevels);
    }

    [Fact]
    public void Calculate_WithTooFewBarsForAnyIndicator_OmitsAllOfThem()
    {
        var bars = RisingSeries(10);

        var result = calculator.Calculate(bars);

        Assert.Empty(result.MovingAverages);
        Assert.False(result.HasGoldenCross);
        Assert.False(result.HasDeathCross);
        Assert.False(result.HasRsi);
        Assert.False(result.HasMacd);
        Assert.False(result.HasStochastic);
        Assert.False(result.HasWilliamsR);
        Assert.Empty(result.SupportLevels);
        Assert.Empty(result.ResistanceLevels);
    }

    [Fact]
    public void Calculate_Sma20_AveragesLast20ClosesAndFlagsPricePosition()
    {
        // 25 closes: 1..25. Last 20 are 6..25, average 15.5. Not enough bars for SMA50/200.
        var bars = RisingSeries(25);

        var result = calculator.Calculate(bars);

        var sma20 = Assert.Single(result.MovingAverages);
        Assert.Equal(20, sma20.Period);
        Assert.Equal(15.5, sma20.Value, precision: 6);
        Assert.True(sma20.AbovePrice); // current close (25) is above the SMA
        Assert.False(result.HasGoldenCross);
        Assert.False(result.HasDeathCross);
    }

    [Fact]
    public void Calculate_RisingSeries_Rsi14IsMaxedOut()
    {
        var bars = RisingSeries(20);

        var result = calculator.Calculate(bars);

        Assert.True(result.HasRsi);
        Assert.Equal(100, result.Rsi14, precision: 6);
    }

    [Fact]
    public void Calculate_FallingSeries_Rsi14IsZero()
    {
        var bars = FallingSeries(20, startClose: 100);

        var result = calculator.Calculate(bars);

        Assert.True(result.HasRsi);
        Assert.Equal(0, result.Rsi14, precision: 6);
    }

    [Fact]
    public void Calculate_ConstantPriceSeries_MacdStochasticAndWilliamsRAreFlatFallbacks()
    {
        var bars = new List<OhlcvBar>();
        for (int i = 0; i < 60; i++)
            bars.Add(Bar(i, 100, 100, 100, 100));

        var result = calculator.Calculate(bars);

        Assert.True(result.HasMacd);
        Assert.Equal(0, result.MacdLine, precision: 9);
        Assert.Equal(0, result.MacdSignal, precision: 9);
        Assert.Equal(0, result.MacdHistogram, precision: 9);

        // Zero range windows fall back to the midpoint rather than dividing by zero.
        Assert.True(result.HasStochastic);
        Assert.Equal(50, result.StochasticK, precision: 6);
        Assert.Equal(50, result.StochasticD, precision: 6);

        Assert.True(result.HasWilliamsR);
        Assert.Equal(-50, result.WilliamsR, precision: 6);

        Assert.Empty(result.SupportLevels);
        Assert.Empty(result.ResistanceLevels);
    }

    [Fact]
    public void Calculate_CloseAlwaysAtRisingHigh_StochasticAndWilliamsRAreAtExtremes()
    {
        var bars = new List<OhlcvBar>();
        for (int i = 0; i < 25; i++)
        {
            double high = 100 + i;
            bars.Add(Bar(i, high, high, 90, high)); // close == today's high, low is flat well below
        }

        var result = calculator.Calculate(bars);

        Assert.True(result.HasStochastic);
        Assert.Equal(100, result.StochasticK, precision: 6);
        Assert.Equal(100, result.StochasticD, precision: 6);

        Assert.True(result.HasWilliamsR);
        Assert.Equal(0, result.WilliamsR, precision: 6);
    }

    [Fact]
    public void Calculate_WithOneClearSwingHighAndLow_ReportsThemAsResistanceAndSupport()
    {
        // Flat background exactly at the eventual current close (90), so only the two
        // deliberately-planted swing points fall on the support/resistance side of price.
        var bars = new List<OhlcvBar>();
        for (int i = 0; i < 21; i++)
            bars.Add(Bar(i, 90, 90, 90, 90));

        bars[7]  = Bar(7, 90, 90, 50, 90);   // swing low
        bars[12] = Bar(12, 90, 150, 90, 90); // swing high
        bars[20] = Bar(20, 90, 90, 90, 90);  // current close stays 90

        var result = calculator.Calculate(bars);

        var support = Assert.Single(result.SupportLevels);
        Assert.Equal(50, support.Price, precision: 6);

        var resistance = Assert.Single(result.ResistanceLevels);
        Assert.Equal(150, resistance.Price, precision: 6);
    }
}
