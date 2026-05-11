using System.Windows.Input;
using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class BadgeViewModel
{
    public string Name { get; }
    public string Color { get; }

    public BadgeViewModel(string name)
    {
        Name = name;
        Color = name switch
        {
            "Oversold" or "Overbought" => "#FFF3CD",
            "Uptrend"                  => "#D4EDDA",
            "Downtrend"                => "#F8D7DA",
            _                          => "#E2E3E5",
        };
    }
}

public sealed class AnalysisHistoryItemViewModel
{
    public string Ticker { get; }
    public string AnalyzedAt { get; }
    public string PriceAtClose { get; }
    public string StartDate { get; }
    public string EndDate { get; }
    public string AiResultText { get; }
    public IReadOnlyList<BadgeViewModel> Badges { get; }
    public ICommand ReanalyzeCommand { get; }

    public AnalysisHistoryItemViewModel(
        AnalysisHistoryItem proto,
        Func<AnalysisHistoryItemViewModel, Task> reanalyzeAction)
    {
        Ticker       = proto.Ticker;
        AnalyzedAt   = DateTimeOffset.TryParse(proto.AnalyzedAt, out var dt)
                           ? dt.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                           : proto.AnalyzedAt;
        PriceAtClose = $"${proto.PriceAtClose:F2}";
        StartDate    = proto.StartDate;
        EndDate      = proto.EndDate;
        AiResultText = proto.AiResultText;
        Badges       = proto.Badges.Select(b => new BadgeViewModel(b)).ToList();
        ReanalyzeCommand = new AsyncCommand(() => reanalyzeAction(this));
    }
}
