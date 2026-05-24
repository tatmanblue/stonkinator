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
        if (e.PropertyName == nameof(SearchAnalyzeViewModel.ChartBars) && viewModel is not null)
            UpdateChart(viewModel.ChartBars);
    }

    private async void OnCopyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null && viewModel is not null)
            await clipboard.SetTextAsync(viewModel.AnalysisText);
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
