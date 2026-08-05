using Avalonia.Controls;
using Avalonia.Interactivity;
using Stonks.Client.Desktop.ViewModels;
using Stonks.Client.Desktop.Views;

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
        {
            await vm.Dashboard.LoadHistoryAsync();
            await vm.OptionsEvaluator.LoadSavedEvaluationsAsync();
        }
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var settingsVm = new SettingsViewModel(vm.SettingsService);
        var window = new SettingsWindow { DataContext = settingsVm };
        window.ShowDialog(this);
    }
}
