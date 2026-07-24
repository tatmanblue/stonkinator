using Avalonia.Controls;
using Avalonia.Input;
using Stonks.Client.Desktop.ViewModels;

namespace Stonks.Client.Desktop.Views;

public partial class OptionsEvaluatorView : UserControl
{
    public OptionsEvaluatorView()
    {
        InitializeComponent();
    }

    private void OnSavedListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is OptionsEvaluatorViewModel vm &&
            SavedList.SelectedItem is SavedEvaluationSummaryViewModel item)
        {
            vm.OpenSavedEvaluation(item);
        }
    }
}
