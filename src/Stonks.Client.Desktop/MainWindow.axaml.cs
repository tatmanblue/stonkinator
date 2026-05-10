using Avalonia.Controls;
using ScottPlot;
using Stonks.Client.Desktop.ViewModels;
using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop;

public partial class MainWindow : Window
{
    private MainWindowViewModel? viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (viewModel is not null)
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        viewModel = DataContext as MainWindowViewModel;

        if (viewModel is not null)
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ChartBars) && viewModel is not null)
            UpdateChart(viewModel.ChartBars);

        if (e.PropertyName == nameof(MainWindowViewModel.AnalysisText))
            AnalysisScroll.ScrollToEnd();
    }

    private void UpdateChart(OhlcvBar[] bars)
    {
        if (bars.Length == 0) return;

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
