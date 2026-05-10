using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using Grpc.Core;
using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly StocksAnalysis.StocksAnalysisClient grpcClient;

    private string ticker = "AAPL";
    private DateTimeOffset? startDate = DateTimeOffset.Now.AddMonths(-3);
    private DateTimeOffset? endDate = DateTimeOffset.Now;
    private string analysisText = "";
    private bool isLoading;
    private OhlcvBar[] chartBars = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel(StocksAnalysis.StocksAnalysisClient grpcClient)
    {
        this.grpcClient = grpcClient;
        AnalyzeCommand = new AsyncCommand(RunAnalysisAsync);
    }

    public string Ticker
    {
        get => ticker;
        set => SetField(ref ticker, value);
    }

    public DateTimeOffset? StartDate
    {
        get => startDate;
        set => SetField(ref startDate, value);
    }

    public DateTimeOffset? EndDate
    {
        get => endDate;
        set => SetField(ref endDate, value);
    }

    public string AnalysisText
    {
        get => analysisText;
        set => SetField(ref analysisText, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        set
        {
            SetField(ref isLoading, value);
            (AnalyzeCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public OhlcvBar[] ChartBars
    {
        get => chartBars;
        set => SetField(ref chartBars, value);
    }

    public ICommand AnalyzeCommand { get; }

    private async Task RunAnalysisAsync()
    {
        IsLoading = true;
        AnalysisText = "";
        ChartBars = [];

        try
        {
            var request = new AnalyzeStockRequest
            {
                Ticker    = Ticker.Trim().ToUpper(),
                StartDate = (StartDate ?? DateTimeOffset.Now.AddMonths(-3)).ToString("yyyy-MM-dd"),
                EndDate   = (EndDate   ?? DateTimeOffset.Now).ToString("yyyy-MM-dd")
            };

            using var call = grpcClient.AnalyzeStock(request);
            await foreach (var response in call.ResponseStream.ReadAllAsync())
            {
                switch (response.PayloadCase)
                {
                    case AnalyzeStockResponse.PayloadOneofCase.OhlcvData:
                        var bars = response.OhlcvData.Bars.ToArray();
                        Dispatcher.UIThread.Post(() => ChartBars = bars);
                        break;

                    case AnalyzeStockResponse.PayloadOneofCase.AnalysisChunk:
                        var chunk = response.AnalysisChunk;
                        Dispatcher.UIThread.Post(() => AnalysisText += chunk);
                        break;

                    case AnalyzeStockResponse.PayloadOneofCase.ErrorMessage:
                        var msg = response.ErrorMessage;
                        Dispatcher.UIThread.Post(() => AnalysisText += $"\n[Error] {msg}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            AnalysisText += $"\n[Error] {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
