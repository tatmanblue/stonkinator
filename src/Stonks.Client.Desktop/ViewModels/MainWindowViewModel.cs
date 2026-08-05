using System.ComponentModel;
using System.Runtime.CompilerServices;
using Stonks.Client.Desktop.Settings;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private int selectedTabIndex;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DashboardViewModel Dashboard { get; }
    public SearchAnalyzeViewModel SearchAnalyze { get; }
    public OptionsEvaluatorViewModel OptionsEvaluator { get; }
    public AppSettingsService SettingsService { get; }

    public int SelectedTabIndex
    {
        get => selectedTabIndex;
        set => SetField(ref selectedTabIndex, value);
    }

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        SearchAnalyzeViewModel searchAnalyze,
        OptionsEvaluatorViewModel optionsEvaluator,
        AppSettingsService settingsService)
    {
        Dashboard        = dashboard;
        SearchAnalyze    = searchAnalyze;
        OptionsEvaluator = optionsEvaluator;
        SettingsService  = settingsService;

        dashboard.ItemOpenRequested = item =>
        {
            searchAnalyze.LoadFromHistory(item);
            SelectedTabIndex = 1;
        };

        searchAnalyze.AnalysisCompleted = dashboard.LoadHistoryAsync;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
