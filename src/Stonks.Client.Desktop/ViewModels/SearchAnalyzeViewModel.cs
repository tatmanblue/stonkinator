using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using Grpc.Core;
using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class SearchAnalyzeViewModel : INotifyPropertyChanged
{
    private readonly StocksAnalysis.StocksAnalysisClient grpcClient;

    private string ticker = "AAPL";
    private DateTimeOffset? startDate = DateTimeOffset.Now.AddMonths(-3);
    private DateTimeOffset? endDate = DateTimeOffset.Now;
    private string analysisText = "";
    private string rawBuffer = "";
    private string? errorMessage;
    private bool isLoading;
    private OhlcvBar[] chartBars = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public SearchAnalyzeViewModel(StocksAnalysis.StocksAnalysisClient grpcClient)
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
        set
        {
            SetField(ref analysisText, value);
            OnPropertyChanged(nameof(HasAnalysisText));
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        set
        {
            SetField(ref errorMessage, value);
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => errorMessage is not null;

    public bool HasAnalysisText => !string.IsNullOrEmpty(analysisText);

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

    public Func<Task>? AnalysisCompleted { get; set; }

    public void LoadFromHistory(AnalysisHistoryItemViewModel item)
    {
        Ticker = item.Ticker;
        if (DateTimeOffset.TryParse(item.StartDate, out var start))
            StartDate = start;
        if (DateTimeOffset.TryParse(item.EndDate, out var end))
            EndDate = end;
        AnalysisText = item.AiResultText;
        ChartBars = [];
        ErrorMessage = null;
    }

    private async Task RunAnalysisAsync()
    {
        IsLoading = true;
        rawBuffer = "";
        AnalysisText = "";
        ChartBars = [];
        ErrorMessage = null;

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
                        rawBuffer += response.AnalysisChunk;
                        break;

                    case AnalyzeStockResponse.PayloadOneofCase.ErrorMessage:
                        var errMsg = response.ErrorMessage;
                        Dispatcher.UIThread.Post(() => ErrorMessage = errMsg);
                        break;
                }
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            ErrorMessage = "Could not connect to server. Check that the server is running.";
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            ErrorMessage = "Ticker symbol not recognized or no data available for this date range.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            AnalysisText = rawBuffer;
            IsLoading = false;
        }

        if (ErrorMessage is null && AnalysisCompleted is not null)
            await AnalysisCompleted();
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
