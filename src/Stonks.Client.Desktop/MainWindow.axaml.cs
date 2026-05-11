using Avalonia.Controls;
using Stonks.Client.Desktop.ViewModels;

namespace Stonks.Client.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnWindowOpened;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await vm.Dashboard.LoadHistoryAsync();
    }
}
