using Avalonia.Controls;
using ScottPlot;
using Stonks.Client.Desktop.ViewModels;
using Stonks.Shared.Charting;
using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop.Views;

public partial class SearchAnalyzeView : UserControl
{
    private static readonly ScottPlot.Color BOLLINGER_LINE_COLOR = ScottPlot.Color.FromHex("#5B7FBF");
    private static readonly ScottPlot.Color BOLLINGER_FILL_COLOR = ScottPlot.Color.FromHex("#5B7FBF").WithAlpha(0.12);
    private static readonly ScottPlot.Color BOLLINGER_SQUEEZE_COLOR = ScottPlot.Color.FromHex("#F0AD4E").WithAlpha(0.15);

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

        // Bars and indicators arrive separately during a live analysis, so redraw on either.
        if (e.PropertyName is nameof(SearchAnalyzeViewModel.ChartBars)
            or nameof(SearchAnalyzeViewModel.Indicators)
            or nameof(SearchAnalyzeViewModel.ShowBollingerOverlay))
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
        AvaPlot.Plot.HideLegend();

        // Overlays are added first so the candlesticks draw on top of them.
        if (viewModel is { ShowBollingerOverlay: true, Indicators: not null })
            AddBollingerOverlay(viewModel.Indicators);

        AvaPlot.Plot.Add.Candlestick(ohlcList);
        AvaPlot.Plot.Axes.DateTimeTicksBottom();
        AvaPlot.Refresh();
    }

    private void AddBollingerOverlay(TechnicalIndicatorsViewModel indicators)
    {
        foreach (DateRange period in indicators.BollingerSqueezePeriods)
        {
            // Pad half a day each side so a single-day squeeze is still visible.
            double x1 = DateTime.Parse(period.StartDate).AddHours(-12).ToOADate();
            double x2 = DateTime.Parse(period.EndDate).AddHours(12).ToOADate();
            // ScottPlot's HorizontalSpan covers an x (date) range; VerticalSpan covers a y range.
            var span = AvaPlot.Plot.Add.HorizontalSpan(x1, x2);
            span.FillColor = BOLLINGER_SQUEEZE_COLOR;
            span.LineWidth = 0;
        }

        ChartOverlaySeries? upper = FindOverlay(indicators, ChartOverlayIds.BOLLINGER_UPPER);
        ChartOverlaySeries? middle = FindOverlay(indicators, ChartOverlayIds.BOLLINGER_MIDDLE);
        ChartOverlaySeries? lower = FindOverlay(indicators, ChartOverlayIds.BOLLINGER_LOWER);
        if (upper is null || middle is null || lower is null || upper.Dates.Count == 0) return;

        double[] xs = upper.Dates.Select(d => DateTime.Parse(d).ToOADate()).ToArray();

        var fill = AvaPlot.Plot.Add.FillY(xs, lower.Values.ToArray(), upper.Values.ToArray());
        fill.FillColor = BOLLINGER_FILL_COLOR;
        fill.LineWidth = 0;

        var upperLine = AvaPlot.Plot.Add.ScatterLine(xs, upper.Values.ToArray(), BOLLINGER_LINE_COLOR);
        upperLine.LineWidth = 1;
        upperLine.LegendText = "Bollinger (20,2)";

        var lowerLine = AvaPlot.Plot.Add.ScatterLine(xs, lower.Values.ToArray(), BOLLINGER_LINE_COLOR);
        lowerLine.LineWidth = 1;

        var middleLine = AvaPlot.Plot.Add.ScatterLine(xs, middle.Values.ToArray(), BOLLINGER_LINE_COLOR);
        middleLine.LineWidth = 1;
        middleLine.LinePattern = LinePattern.Dashed;
        middleLine.LegendText = "SMA 20";

        AvaPlot.Plot.ShowLegend(Alignment.UpperLeft);
    }

    private static ChartOverlaySeries? FindOverlay(TechnicalIndicatorsViewModel indicators, string id) =>
        indicators.ChartOverlays.FirstOrDefault(o => o.Id == id);
}
