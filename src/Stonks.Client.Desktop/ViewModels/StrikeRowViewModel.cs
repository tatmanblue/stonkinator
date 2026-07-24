using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Stonks.Shared.Calculations;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class StrikeRowViewModel : INotifyPropertyChanged
{
    private readonly Func<double> getCurrentPrice;

    private double strike;
    private double callPremium;
    private double putPremium;
    private StrikeEvaluation evaluation;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RemoveCommand { get; }

    public StrikeRowViewModel(
        double strike, double callPremium, double putPremium,
        Func<double> getCurrentPrice, Action<StrikeRowViewModel> onRemove)
    {
        this.strike = strike;
        this.callPremium = callPremium;
        this.putPremium = putPremium;
        this.getCurrentPrice = getCurrentPrice;
        RemoveCommand = new AsyncCommand(() => { onRemove(this); return Task.CompletedTask; });
        Recompute();
    }

    public double Strike
    {
        get => strike;
        set { SetField(ref strike, value); Recompute(); }
    }

    public double CallPremium
    {
        get => callPremium;
        set { SetField(ref callPremium, value); Recompute(); }
    }

    public double PutPremium
    {
        get => putPremium;
        set { SetField(ref putPremium, value); Recompute(); }
    }

    public double CallCost => evaluation.CallCost;
    public double PutCost => evaluation.PutCost;
    public double Straddle => evaluation.Straddle;
    public double BreakEvenCall => evaluation.BreakEvenCall;
    public double BreakEvenPut => evaluation.BreakEvenPut;
    public double ItmUp => evaluation.ItmUp;
    public double ItmDown => evaluation.ItmDown;
    public double CallPctChange => evaluation.CallPctChange;
    public double PutPctChange => evaluation.PutPctChange;

    public void RecomputeForCurrentPriceChange() => Recompute();

    private void Recompute()
    {
        evaluation = StraddleCalculator.Evaluate(strike, callPremium, putPremium, getCurrentPrice());
        OnPropertyChanged(nameof(CallCost));
        OnPropertyChanged(nameof(PutCost));
        OnPropertyChanged(nameof(Straddle));
        OnPropertyChanged(nameof(BreakEvenCall));
        OnPropertyChanged(nameof(BreakEvenPut));
        OnPropertyChanged(nameof(ItmUp));
        OnPropertyChanged(nameof(ItmDown));
        OnPropertyChanged(nameof(CallPctChange));
        OnPropertyChanged(nameof(PutPctChange));
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
