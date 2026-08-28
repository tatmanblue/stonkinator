using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class IndicatorRowViewModel
{
    public string Label { get; }
    public string Value { get; }

    public IndicatorRowViewModel(string label, string value)
    {
        Label = label;
        Value = value;
    }
}

public sealed class TechnicalIndicatorsViewModel
{
    public IReadOnlyList<BadgeViewModel> SignalBadges { get; }
    public IReadOnlyList<IndicatorRowViewModel> MovingAverageRows { get; }
    public IReadOnlyList<IndicatorRowViewModel> MomentumRows { get; }
    public string SupportText { get; }
    public string ResistanceText { get; }

    public TechnicalIndicatorsViewModel(TechnicalIndicators proto)
    {
        MovingAverageRows = proto.MovingAverages
            .Select(ma => new IndicatorRowViewModel($"SMA {ma.Period}", $"${ma.Value:F2} {(ma.AbovePrice ? "▲" : "▼")}"))
            .ToList();

        var momentumRows = new List<IndicatorRowViewModel>();
        if (proto.HasRsi)
            momentumRows.Add(new IndicatorRowViewModel("RSI(14)", $"{proto.Rsi14:F1}"));
        if (proto.HasMacd)
            momentumRows.Add(new IndicatorRowViewModel("MACD(12,26,9)",
                $"{proto.MacdLine:F2} / {proto.MacdSignal:F2} / {proto.MacdHistogram:F2}"));
        if (proto.HasStochastic)
            momentumRows.Add(new IndicatorRowViewModel("Stochastic %K/%D", $"{proto.StochasticK:F1} / {proto.StochasticD:F1}"));
        if (proto.HasWilliamsR)
            momentumRows.Add(new IndicatorRowViewModel("Williams %R", $"{proto.WilliamsR:F1}"));
        MomentumRows = momentumRows;

        SupportText = proto.SupportLevels.Count > 0
            ? string.Join(", ", proto.SupportLevels.Select(l => $"${l.Price:F2}"))
            : "—";
        ResistanceText = proto.ResistanceLevels.Count > 0
            ? string.Join(", ", proto.ResistanceLevels.Select(l => $"${l.Price:F2}"))
            : "—";

        SignalBadges = BuildSignalBadges(proto);
    }

    private static IReadOnlyList<BadgeViewModel> BuildSignalBadges(TechnicalIndicators proto)
    {
        var badges = new List<string>();

        if (proto.MovingAverages.Count > 0)
        {
            if (proto.MovingAverages.All(ma => ma.AbovePrice)) badges.Add("Uptrend");
            else if (proto.MovingAverages.All(ma => !ma.AbovePrice)) badges.Add("Downtrend");
        }

        if (proto.HasGoldenCross) badges.Add("Golden Cross");
        if (proto.HasDeathCross) badges.Add("Death Cross");

        if (proto.HasRsi)
        {
            if (proto.Rsi14 < 30) badges.Add("RSI Oversold");
            else if (proto.Rsi14 > 70) badges.Add("RSI Overbought");
        }

        if (proto.HasStochastic)
        {
            if (proto.StochasticK < 20) badges.Add("Stoch Oversold");
            else if (proto.StochasticK > 80) badges.Add("Stoch Overbought");
        }

        if (proto.HasMacd)
            badges.Add(proto.MacdHistogram >= 0 ? "MACD: Buy" : "MACD: Sell");

        return badges.Select(b => new BadgeViewModel(b)).ToList();
    }
}
