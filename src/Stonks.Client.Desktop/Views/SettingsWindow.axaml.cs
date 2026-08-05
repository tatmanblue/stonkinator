using Avalonia.Controls;
using Stonks.Client.Desktop.ViewModels;

namespace Stonks.Client.Desktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
                vm.RequestClose = Close;
        };
    }
}
