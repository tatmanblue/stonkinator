using Stonks.Server.Indicators;
using Stonks.Shared.Grpc;

namespace Stonks.Server.Tests.Indicators;

public class ChartOverlayTrimmerTests
{
    private static readonly DateOnly START = new(2024, 3, 10);
    private static readonly DateOnly END = new(2024, 3, 20);

    [Fact]
    public void TrimToRange_KeepsOnlyOverlayPointsInsideRangeInclusive()
    {
        var series = new ChartOverlaySeries { Id = "bb_upper" };
        series.Dates.AddRange(["2024-03-09", "2024-03-10", "2024-03-15", "2024-03-20", "2024-03-21"]);
        series.Values.AddRange([1, 2, 3, 4, 5]);
        var indicators = new TechnicalIndicators();
        indicators.ChartOverlays.Add(series);

        ChartOverlayTrimmer.TrimToRange(indicators, START, END);

        Assert.Equal(["2024-03-10", "2024-03-15", "2024-03-20"], series.Dates);
        Assert.Equal([2.0, 3.0, 4.0], series.Values);
    }

    [Fact]
    public void TrimToRange_DropsOutsideSqueezePeriodsAndClipsOverlappingOnes()
    {
        var indicators = new TechnicalIndicators();
        indicators.BollingerSqueezePeriods.Add(new DateRange { StartDate = "2024-03-01", EndDate = "2024-03-05" });
        indicators.BollingerSqueezePeriods.Add(new DateRange { StartDate = "2024-03-08", EndDate = "2024-03-12" });
        indicators.BollingerSqueezePeriods.Add(new DateRange { StartDate = "2024-03-14", EndDate = "2024-03-15" });
        indicators.BollingerSqueezePeriods.Add(new DateRange { StartDate = "2024-03-19", EndDate = "2024-03-25" });
        indicators.BollingerSqueezePeriods.Add(new DateRange { StartDate = "2024-03-22", EndDate = "2024-03-28" });

        ChartOverlayTrimmer.TrimToRange(indicators, START, END);

        Assert.Collection(indicators.BollingerSqueezePeriods,
            p => { Assert.Equal("2024-03-10", p.StartDate); Assert.Equal("2024-03-12", p.EndDate); },
            p => { Assert.Equal("2024-03-14", p.StartDate); Assert.Equal("2024-03-15", p.EndDate); },
            p => { Assert.Equal("2024-03-19", p.StartDate); Assert.Equal("2024-03-20", p.EndDate); });
    }
}
