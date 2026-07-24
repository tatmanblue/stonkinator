using System.Windows.Input;
using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class SavedEvaluationSummaryViewModel
{
    public long Id { get; }
    public string Ticker { get; }
    public string ExpirationDate { get; }
    public double CurrentPrice { get; }
    public double Commission { get; }
    public string SavedAt { get; }
    public IReadOnlyList<OptionEvaluationStrikeRow> Strikes { get; }
    public ICommand DeleteCommand { get; }

    public SavedEvaluationSummaryViewModel(
        OptionsEvaluationItem proto,
        Func<SavedEvaluationSummaryViewModel, Task> deleteAction)
    {
        Id             = proto.Id;
        Ticker         = proto.Ticker;
        ExpirationDate = proto.ExpirationDate;
        CurrentPrice   = proto.CurrentPrice;
        Commission     = proto.Commission;
        SavedAt        = DateTimeOffset.TryParse(proto.SavedAt, out var dt)
                              ? dt.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                              : proto.SavedAt;
        Strikes        = proto.Strikes.ToList();
        DeleteCommand  = new AsyncCommand(() => deleteAction(this));
    }
}
