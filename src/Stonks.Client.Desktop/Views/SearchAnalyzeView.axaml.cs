using Avalonia.Controls;
using ScottPlot;
using Stonks.Client.Desktop.ViewModels;
using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop.Views;

public partial class SearchAnalyzeView : UserControl
{
    private SearchAnalyzeViewModel? viewModel;

    public SearchAnalyzeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (viewModel is not null)
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        viewModel = DataContext as SearchAnalyzeViewModel;

        if (viewModel is not null)
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (viewModel is null) return;

        if (e.PropertyName == nameof(SearchAnalyzeViewModel.ChartBars))
            UpdateChart(viewModel.ChartBars);

        if (e.PropertyName == nameof(SearchAnalyzeViewModel.IsQaPanelVisible))
            UpdateQaColumnWidth(viewModel.IsQaPanelVisible);
    }

    // ColumnDefinition.Width isn't bound in XAML — set imperatively so hiding the panel
    // actually reclaims its column width instead of leaving a blank gap.
    private void UpdateQaColumnWidth(bool isVisible) =>
        ContentGrid.ColumnDefinitions[4].Width = isVisible
            ? new GridLength(1.5, GridUnitType.Star)
            : new GridLength(0);

    private async void OnCopyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null && viewModel is not null)
            await clipboard.SetTextAsync(viewModel.AnalysisText);
    }

    private async void OnCopyQaTurnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: QaTurnViewModel turn }) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync($"Q: {turn.Question}\n\nA: {turn.Answer}");
    }

    private void UpdateChart(OhlcvBar[] bars)
    {
        if (bars.Length == 0)
        {
            AvaPlot.Plot.Clear();
            AvaPlot.Refresh();
            return;
        }

        var ohlcList = bars
            .Select(b => new OHLC(b.Open, b.High, b.Low, b.Close,
                DateTime.Parse(b.Date), TimeSpan.FromDays(1)))
            .ToList();

        AvaPlot.Plot.Clear();
        AvaPlot.Plot.Add.Candlestick(ohlcList);
        AvaPlot.Plot.Axes.DateTimeTicksBottom();
        AvaPlot.Refresh();
    }
}
