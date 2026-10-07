using Stonks.Shared.Grpc;

namespace Stonks.Server.Indicators;

// Indicators are computed over a longer lookback than the chart shows, so per-day overlay data
// is cut down to the requested chart range before being sent to the client.
public static class ChartOverlayTrimmer
{
    public static void TrimToRange(TechnicalIndicators indicators, DateOnly start, DateOnly end)
    {
        // YYYY-MM-DD strings sort chronologically, so ordinal comparison is enough.
        string startText = start.ToString("yyyy-MM-dd");
        string endText = end.ToString("yyyy-MM-dd");

        foreach (ChartOverlaySeries series in indicators.ChartOverlays)
        {
            List<int> keep = Enumerable.Range(0, series.Dates.Count)
                .Where(i => IsWithin(series.Dates[i], startText, endText))
                .ToList();
            string[] dates = keep.Select(i => series.Dates[i]).ToArray();
            double[] values = keep.Select(i => series.Values[i]).ToArray();

            series.Dates.Clear();
            series.Dates.AddRange(dates);
            series.Values.Clear();
            series.Values.AddRange(values);
        }

        List<DateRange> clipped = new();
        foreach (DateRange range in indicators.BollingerSqueezePeriods)
        {
            if (string.CompareOrdinal(range.EndDate, startText) < 0) continue;
            if (string.CompareOrdinal(range.StartDate, endText) > 0) continue;
            clipped.Add(new DateRange
            {
                StartDate = string.CompareOrdinal(range.StartDate, startText) < 0 ? startText : range.StartDate,
                EndDate   = string.CompareOrdinal(range.EndDate, endText) > 0 ? endText : range.EndDate,
            });
        }
        indicators.BollingerSqueezePeriods.Clear();
        indicators.BollingerSqueezePeriods.AddRange(clipped);
    }

    private static bool IsWithin(string date, string startText, string endText) =>
        string.CompareOrdinal(date, startText) >= 0 && string.CompareOrdinal(date, endText) <= 0;
}
