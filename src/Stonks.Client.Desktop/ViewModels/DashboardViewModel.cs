using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Grpc.Core;
using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly StocksHistory.StocksHistoryClient historyClient;
    private readonly StocksAnalysis.StocksAnalysisClient analysisClient;

    private bool isRefreshing;
    private string? errorMessage;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Action<AnalysisHistoryItemViewModel>? ItemOpenRequested { get; set; }

    public ObservableCollection<AnalysisHistoryItemViewModel> Items { get; } = new();

    public bool IsEmpty => Items.Count == 0;
    public bool IsNotEmpty => Items.Count > 0;

    public bool IsRefreshing
    {
        get => isRefreshing;
        private set
        {
            SetField(ref isRefreshing, value);
            (ReanalyzeAllCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            SetField(ref errorMessage, value);
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => errorMessage is not null;

    public ICommand ReanalyzeAllCommand { get; }

    public DashboardViewModel(
        StocksHistory.StocksHistoryClient historyClient,
        StocksAnalysis.StocksAnalysisClient analysisClient)
    {
        this.historyClient  = historyClient;
        this.analysisClient = analysisClient;
        ReanalyzeAllCommand = new AsyncCommand(ReanalyzeAllAsync, () => !isRefreshing);
    }

    public void OpenItem(AnalysisHistoryItemViewModel item)
    {
        ItemOpenRequested?.Invoke(item);
    }

    public async Task LoadHistoryAsync()
    {
        ErrorMessage = null;
        try
        {
            var response = await historyClient.GetAnalysisHistoryAsync(new GetAnalysisHistoryRequest());
            Items.Clear();
            foreach (var item in response.Items)
                Items.Add(new AnalysisHistoryItemViewModel(item, ReanalyzeItemAsync));
            NotifyEmptyChanged();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            ErrorMessage = "Could not connect to server. Check that the server is running.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load history: {ex.Message}";
        }
    }

    private async Task ReanalyzeItemAsync(AnalysisHistoryItemViewModel item)
    {
        IsRefreshing = true;
        ErrorMessage = null;
        try
        {
            await RunAnalysisAsync(item.Ticker);
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Re-analysis failed: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task ReanalyzeAllAsync()
    {
        IsRefreshing = true;
        ErrorMessage = null;
        var tickers = Items.Select(i => i.Ticker).ToList();
        try
        {
            foreach (var ticker in tickers)
            {
                await RunAnalysisAsync(ticker);
                await LoadHistoryAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Re-analysis failed: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task RunAnalysisAsync(string ticker)
    {
        var request = new AnalyzeStockRequest
        {
            Ticker    = ticker,
            // TODO: make this computation configurable
            StartDate = DateTimeOffset.Now.AddMonths(-3).ToString("yyyy-MM-dd"),
            EndDate   = DateTimeOffset.Now.ToString("yyyy-MM-dd"),
        };
        using var call = analysisClient.AnalyzeStock(request);
        await foreach (var _ in call.ResponseStream.ReadAllAsync()) { }
    }

    private void NotifyEmptyChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsNotEmpty));
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
