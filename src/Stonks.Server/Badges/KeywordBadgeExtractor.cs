namespace Stonks.Server.Badges;

public class KeywordBadgeExtractor : IBadgeExtractor
{
    private static readonly IReadOnlyList<(string Badge, string[] Keywords)> BADGE_RULES =
    [
        ("Oversold",      ["oversold", "rsi below 30", "extremely low rsi", "deeply oversold"]),
        ("Overbought",    ["overbought", "rsi above 70", "extremely high rsi", "deeply overbought"]),
        ("Uptrend",       ["uptrend", "bullish trend", "higher highs", "higher lows", "bullish momentum"]),
        ("Downtrend",     ["downtrend", "bearish trend", "lower lows", "lower highs", "bearish momentum"]),
        ("Consolidation", ["consolidation", "sideways", "range-bound", "ranging", "no clear trend"]),
    ];

    public IReadOnlyList<string> Extract(string analysisText)
    {
        var lower = analysisText.ToLowerInvariant();
        var badges = new List<string>();
        foreach (var (badge, keywords) in BADGE_RULES)
        {
            if (keywords.Any(lower.Contains))
                badges.Add(badge);
        }
        return badges;
    }
}
