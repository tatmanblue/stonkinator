using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Grpc.Core;
using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class OptionsEvaluatorViewModel : INotifyPropertyChanged
{
    private readonly OptionsMarketData.OptionsMarketDataClient marketDataClient;
    private readonly OptionsEvaluations.OptionsEvaluationsClient evaluationsClient;

    private string ticker = "";
    private string? selectedExpiration;
    private DateTimeOffset? manualExpirationDate;
    private bool useManualExpiration;
    private double? currentPrice;
    private double commission;
    private string? errorMessage;
    private string? statusNotice;
    private bool isLoadingExpirations;
    private bool isLoadingChain;
    private bool isSaving;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> AvailableExpirations { get; } = new();
    public ObservableCollection<StrikeRowViewModel> Strikes { get; } = new();
    public ObservableCollection<SavedEvaluationSummaryViewModel> SavedEvaluations { get; } = new();

    public ICommand FetchExpirationsCommand { get; }
    public ICommand FetchChainCommand { get; }
    public ICommand AddStrikeRowCommand { get; }
    public ICommand SaveEvaluationCommand { get; }

    public OptionsEvaluatorViewModel(
        OptionsMarketData.OptionsMarketDataClient marketDataClient,
        OptionsEvaluations.OptionsEvaluationsClient evaluationsClient)
    {
        this.marketDataClient = marketDataClient;
        this.evaluationsClient = evaluationsClient;

        FetchExpirationsCommand = new AsyncCommand(FetchExpirationsAsync, () => !isLoadingExpirations);
        FetchChainCommand = new AsyncCommand(FetchChainAsync, () => !isLoadingChain);
        AddStrikeRowCommand = new AsyncCommand(() => { AddStrikeRow(0, 0, 0); return Task.CompletedTask; });
        SaveEvaluationCommand = new AsyncCommand(SaveEvaluationAsync, () => !isSaving);
    }

    public string Ticker
    {
        get => ticker;
        set => SetField(ref ticker, value);
    }

    public string? SelectedExpiration
    {
        get => selectedExpiration;
        set => SetField(ref selectedExpiration, value);
    }

    public DateTimeOffset? ManualExpirationDate
    {
        get => manualExpirationDate;
        set => SetField(ref manualExpirationDate, value);
    }

    public bool UseManualExpiration
    {
        get => useManualExpiration;
        set => SetField(ref useManualExpiration, value);
    }

    public double? CurrentPrice
    {
        get => currentPrice;
        set
        {
            SetField(ref currentPrice, value);
            foreach (var row in Strikes)
                row.RecomputeForCurrentPriceChange();
        }
    }

    public double Commission
    {
        get => commission;
        set => SetField(ref commission, value);
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

    public string? StatusNotice
    {
        get => statusNotice;
        set
        {
            SetField(ref statusNotice, value);
            OnPropertyChanged(nameof(HasStatusNotice));
        }
    }

    public bool HasStatusNotice => statusNotice is not null;

    public bool IsLoadingExpirations
    {
        get => isLoadingExpirations;
        private set
        {
            SetField(ref isLoadingExpirations, value);
            (FetchExpirationsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool IsLoadingChain
    {
        get => isLoadingChain;
        private set
        {
            SetField(ref isLoadingChain, value);
            (FetchChainCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool IsSaving
    {
        get => isSaving;
        private set
        {
            SetField(ref isSaving, value);
            (SaveEvaluationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public async Task LoadSavedEvaluationsAsync()
    {
        try
        {
            var response = await evaluationsClient.GetOptionsEvaluationsAsync(new GetOptionsEvaluationsRequest());
            SavedEvaluations.Clear();
            foreach (var item in response.Items)
                SavedEvaluations.Add(new SavedEvaluationSummaryViewModel(item, DeleteEvaluationAsync));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            ErrorMessage = "Could not connect to server. Check that the server is running.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load saved evaluations: {ex.Message}";
        }
    }

    public void OpenSavedEvaluation(SavedEvaluationSummaryViewModel item)
    {
        Ticker = item.Ticker;
        if (AvailableExpirations.Contains(item.ExpirationDate))
        {
            UseManualExpiration = false;
            SelectedExpiration = item.ExpirationDate;
        }
        else
        {
            UseManualExpiration = true;
            ManualExpirationDate = DateTimeOffset.TryParse(item.ExpirationDate, out var d) ? d : null;
        }

        CurrentPrice = item.CurrentPrice;
        Commission = item.Commission;
        ErrorMessage = null;
        StatusNotice = null;

        Strikes.Clear();
        foreach (var s in item.Strikes)
            AddStrikeRow(s.Strike, s.CallPremium, s.PutPremium);
    }

    private void AddStrikeRow(double strike, double callPremium, double putPremium)
    {
        Strikes.Add(new StrikeRowViewModel(
            strike, callPremium, putPremium,
            () => CurrentPrice ?? 0,
            row => Strikes.Remove(row)));
    }

    private async Task FetchExpirationsAsync()
    {
        if (string.IsNullOrWhiteSpace(Ticker))
        {
            ErrorMessage = "Enter a ticker first.";
            return;
        }

        IsLoadingExpirations = true;
        ErrorMessage = null;
        StatusNotice = null;
        try
        {
            var response = await marketDataClient.GetOptionExpirationsAsync(
                new GetOptionExpirationsRequest { Ticker = Ticker.Trim().ToUpper() });

            AvailableExpirations.Clear();
            foreach (var date in response.ExpirationDates)
                AvailableExpirations.Add(date);

            UseManualExpiration = false;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            ErrorMessage = "Could not connect to server. Check that the server is running.";
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.PermissionDenied
            or StatusCode.Unauthenticated or StatusCode.FailedPrecondition)
        {
            UseManualExpiration = true;
            StatusNotice = "Options expiration data unavailable from your API plan — enter the expiration date manually.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to fetch expirations: {ex.Message}";
        }
        finally
        {
            IsLoadingExpirations = false;
        }
    }

    private async Task FetchChainAsync()
    {
        var expiration = GetSelectedExpirationDate();
        if (string.IsNullOrWhiteSpace(Ticker) || expiration is null)
        {
            ErrorMessage = "Enter a ticker and expiration date first.";
            return;
        }

        IsLoadingChain = true;
        ErrorMessage = null;
        StatusNotice = null;
        try
        {
            var response = await marketDataClient.GetOptionsChainAsync(new GetOptionsChainRequest
            {
                Ticker = Ticker.Trim().ToUpper(),
                ExpirationDate = expiration
            });

            Strikes.Clear();
            foreach (var s in response.Strikes)
                AddStrikeRow(s.Strike, s.CallPremium, s.PutPremium);

            if ((CurrentPrice is null or 0) && response.UnderlyingPrice > 0)
                CurrentPrice = response.UnderlyingPrice;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            ErrorMessage = "Could not connect to server. Check that the server is running.";
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.PermissionDenied
            or StatusCode.Unauthenticated or StatusCode.FailedPrecondition)
        {
            StatusNotice = "Options chain unavailable from your API plan — add strikes manually below.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to fetch options chain: {ex.Message}";
        }
        finally
        {
            IsLoadingChain = false;
        }
    }

    private async Task SaveEvaluationAsync()
    {
        var expiration = GetSelectedExpirationDate();
        if (string.IsNullOrWhiteSpace(Ticker) || expiration is null || Strikes.Count == 0)
        {
            ErrorMessage = "Ticker, expiration date, and at least one strike row are required to save.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var request = new SaveOptionsEvaluationRequest
            {
                Ticker = Ticker.Trim().ToUpper(),
                ExpirationDate = expiration,
                CurrentPrice = CurrentPrice ?? 0,
                Commission = Commission,
            };
            request.Strikes.AddRange(Strikes.Select(s => new OptionEvaluationStrikeRow
            {
                Strike = s.Strike,
                CallPremium = s.CallPremium,
                PutPremium = s.PutPremium
            }));

            await evaluationsClient.SaveOptionsEvaluationAsync(request);
            await LoadSavedEvaluationsAsync();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            ErrorMessage = "Could not connect to server. Check that the server is running.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save evaluation: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task DeleteEvaluationAsync(SavedEvaluationSummaryViewModel item)
    {
        try
        {
            await evaluationsClient.DeleteOptionsEvaluationAsync(new DeleteOptionsEvaluationRequest { Id = item.Id });
            await LoadSavedEvaluationsAsync();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            ErrorMessage = "Could not connect to server. Check that the server is running.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete evaluation: {ex.Message}";
        }
    }

    private string? GetSelectedExpirationDate() => UseManualExpiration
        ? ManualExpirationDate?.ToString("yyyy-MM-dd")
        : SelectedExpiration;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
