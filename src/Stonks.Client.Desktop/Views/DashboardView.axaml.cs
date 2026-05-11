using Avalonia.Controls;
using Avalonia.Input;
using Stonks.Client.Desktop.ViewModels;

namespace Stonks.Client.Desktop.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm &&
            HistoryList.SelectedItem is AnalysisHistoryItemViewModel item)
        {
            vm.OpenItem(item);
        }
    }
}
