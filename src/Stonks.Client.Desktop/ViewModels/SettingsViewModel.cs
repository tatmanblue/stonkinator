using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Stonks.Client.Desktop.Settings;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsService settingsService;
    private bool includeOhlcvInFollowUp;
    private bool showBollingerOverlay;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IncludeOhlcvInFollowUp
    {
        get => includeOhlcvInFollowUp;
        set => SetField(ref includeOhlcvInFollowUp, value);
    }

    public bool ShowBollingerOverlay
    {
        get => showBollingerOverlay;
        set => SetField(ref showBollingerOverlay, value);
    }

    public ICommand SaveCommand { get; }

    public Action? RequestClose { get; set; }

    public SettingsViewModel(AppSettingsService settingsService)
    {
        this.settingsService = settingsService;
        includeOhlcvInFollowUp = settingsService.Current.IncludeOhlcvInFollowUp;
        showBollingerOverlay = settingsService.Current.ShowBollingerOverlay;
        SaveCommand = new AsyncCommand(SaveAsync);
    }

    private Task SaveAsync()
    {
        settingsService.Current.IncludeOhlcvInFollowUp = includeOhlcvInFollowUp;
        settingsService.Current.ShowBollingerOverlay = showBollingerOverlay;
        settingsService.Save();
        RequestClose?.Invoke();
        return Task.CompletedTask;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
