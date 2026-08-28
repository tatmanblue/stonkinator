using Stonks.Shared.Grpc;

namespace Stonks.Server.Indicators;

public class TechnicalIndicatorCalculator : ITechnicalIndicatorCalculator
{
    private static readonly int[] SMA_PERIODS = [20, 50, 200];
    private const int RSI_PERIOD = 14;
    private const int MACD_FAST_PERIOD = 12;
    private const int MACD_SLOW_PERIOD = 26;
    private const int MACD_SIGNAL_PERIOD = 9;
    private const int STOCHASTIC_PERIOD = 14;
    private const int STOCHASTIC_SMOOTH_K = 3;
    private const int STOCHASTIC_SMOOTH_D = 3;
    private const int WILLIAMS_R_PERIOD = 14;
    private const int SWING_WINDOW = 5;
    private const int MAX_LEVELS_PER_SIDE = 2;

    public TechnicalIndicators Calculate(IReadOnlyList<OhlcvBar> bars)
    {
        var result = new TechnicalIndicators();
        if (bars.Count == 0) return result;

        double[] closes = bars.Select(b => b.Close).ToArray();
        double currentClose = closes[^1];

        CalculateMovingAverages(result, closes, currentClose);
        CalculateRsi(result, closes);
        CalculateMacd(result, closes);
        CalculateStochastic(result, bars);
        CalculateWilliamsR(result, bars);
        CalculateSupportResistance(result, bars, currentClose);

        return result;
    }

    private static void CalculateMovingAverages(TechnicalIndicators result, double[] closes, double currentClose)
    {
        double? sma50 = null;
        double? sma200 = null;

        foreach (int period in SMA_PERIODS)
        {
            if (closes.Length < period) continue;

            double sma = closes[^period..].Average();
            result.MovingAverages.Add(new TechnicalIndicators.Types.MovingAverage
            {
                Period     = period,
                Value      = sma,
                AbovePrice = currentClose > sma,
            });

            if (period == 50) sma50 = sma;
            if (period == 200) sma200 = sma;
        }

        if (sma50.HasValue && sma200.HasValue)
        {
            result.HasGoldenCross = sma50.Value > sma200.Value;
            result.HasDeathCross  = sma50.Value < sma200.Value;
        }
    }

    private static void CalculateRsi(TechnicalIndicators result, double[] closes)
    {
        if (closes.Length <= RSI_PERIOD) return;

        double avgGain = 0;
        double avgLoss = 0;
        for (int i = 1; i <= RSI_PERIOD; i++)
        {
            double change = closes[i] - closes[i - 1];
            if (change > 0) avgGain += change;
            else avgLoss -= change;
        }
        avgGain /= RSI_PERIOD;
        avgLoss /= RSI_PERIOD;

        for (int i = RSI_PERIOD + 1; i < closes.Length; i++)
        {
            double change = closes[i] - closes[i - 1];
            double gain = change > 0 ? change : 0;
            double loss = change < 0 ? -change : 0;
            avgGain = (avgGain * (RSI_PERIOD - 1) + gain) / RSI_PERIOD;
            avgLoss = (avgLoss * (RSI_PERIOD - 1) + loss) / RSI_PERIOD;
        }

        result.HasRsi = true;
        result.Rsi14 = avgLoss == 0 ? 100 : 100 - 100 / (1 + avgGain / avgLoss);
    }

    private static void CalculateMacd(TechnicalIndicators result, double[] closes)
    {
        int minBarsNeeded = MACD_SLOW_PERIOD + MACD_SIGNAL_PERIOD;
        if (closes.Length < minBarsNeeded) return;

        double[] emaFast = ComputeEma(closes, MACD_FAST_PERIOD);
        double[] emaSlow = ComputeEma(closes, MACD_SLOW_PERIOD);

        int macdStart = MACD_SLOW_PERIOD - 1;
        double[] macdLine = new double[closes.Length - macdStart];
        for (int i = macdStart; i < closes.Length; i++)
            macdLine[i - macdStart] = emaFast[i] - emaSlow[i];

        double[] signalLine = ComputeEma(macdLine, MACD_SIGNAL_PERIOD);

        result.HasMacd       = true;
        result.MacdLine      = macdLine[^1];
        result.MacdSignal    = signalLine[^1];
        result.MacdHistogram = result.MacdLine - result.MacdSignal;
    }

    // Seeds with a simple average of the first `period` values, then applies the standard
    // recursive EMA formula. Entries before index (period - 1) are left as 0 and must not be read.
    private static double[] ComputeEma(double[] values, int period)
    {
        double[] ema = new double[values.Length];
        double multiplier = 2.0 / (period + 1);
        ema[period - 1] = values[..period].Average();
        for (int i = period; i < values.Length; i++)
            ema[i] = (values[i] - ema[i - 1]) * multiplier + ema[i - 1];
        return ema;
    }

    private static void CalculateStochastic(TechnicalIndicators result, IReadOnlyList<OhlcvBar> bars)
    {
        if (bars.Count < STOCHASTIC_PERIOD) return;

        List<double> rawK = new();
        for (int i = STOCHASTIC_PERIOD - 1; i < bars.Count; i++)
        {
            double highestHigh = double.MinValue;
            double lowestLow = double.MaxValue;
            for (int j = i - STOCHASTIC_PERIOD + 1; j <= i; j++)
            {
                highestHigh = Math.Max(highestHigh, bars[j].High);
                lowestLow = Math.Min(lowestLow, bars[j].Low);
            }
            double range = highestHigh - lowestLow;
            rawK.Add(range == 0 ? 50 : 100 * (bars[i].Close - lowestLow) / range);
        }

        if (rawK.Count < STOCHASTIC_SMOOTH_K) return;
        List<double> smoothK = RollingAverage(rawK, STOCHASTIC_SMOOTH_K);

        if (smoothK.Count < STOCHASTIC_SMOOTH_D) return;
        List<double> smoothD = RollingAverage(smoothK, STOCHASTIC_SMOOTH_D);

        result.HasStochastic = true;
        result.StochasticK = smoothK[^1];
        result.StochasticD = smoothD[^1];
    }

    private static List<double> RollingAverage(List<double> values, int window)
    {
        List<double> result = new();
        for (int i = window - 1; i < values.Count; i++)
            result.Add(values.Skip(i - window + 1).Take(window).Average());
        return result;
    }

    private static void CalculateWilliamsR(TechnicalIndicators result, IReadOnlyList<OhlcvBar> bars)
    {
        if (bars.Count < WILLIAMS_R_PERIOD) return;

        OhlcvBar[] window = bars.Skip(bars.Count - WILLIAMS_R_PERIOD).ToArray();
        double highestHigh = window.Max(b => b.High);
        double lowestLow = window.Min(b => b.Low);
        double range = highestHigh - lowestLow;

        result.HasWilliamsR = true;
        result.WilliamsR = range == 0 ? -50 : -100 * (highestHigh - bars[^1].Close) / range;
    }

    // Fractal swing-point detection: a bar is a swing high/low if its high/low is the
    // max/min within a window of SWING_WINDOW bars on either side. Scans backward from the
    // most recent eligible bar, keeping the nearest distinct levels on each side of price.
    private static void CalculateSupportResistance(
        TechnicalIndicators result, IReadOnlyList<OhlcvBar> bars, double currentClose)
    {
        if (bars.Count < SWING_WINDOW * 2 + 1) return;

        List<double> resistanceCandidates = new();
        List<double> supportCandidates = new();

        for (int i = bars.Count - 1 - SWING_WINDOW; i >= SWING_WINDOW; i--)
        {
            IEnumerable<OhlcvBar> window = bars.Skip(i - SWING_WINDOW).Take(SWING_WINDOW * 2 + 1);
            double high = bars[i].High;
            double low = bars[i].Low;

            if (resistanceCandidates.Count < MAX_LEVELS_PER_SIDE
                && high > currentClose && window.Max(b => b.High) == high)
                resistanceCandidates.Add(high);

            if (supportCandidates.Count < MAX_LEVELS_PER_SIDE
                && low < currentClose && window.Min(b => b.Low) == low)
                supportCandidates.Add(low);

            if (resistanceCandidates.Count >= MAX_LEVELS_PER_SIDE && supportCandidates.Count >= MAX_LEVELS_PER_SIDE)
                break;
        }

        // Nearest level to the current price is listed first on each side.
        foreach (double price in resistanceCandidates.OrderBy(p => p))
            result.ResistanceLevels.Add(new TechnicalIndicators.Types.SupportResistanceLevel { Price = price });
        foreach (double price in supportCandidates.OrderByDescending(p => p))
            result.SupportLevels.Add(new TechnicalIndicators.Types.SupportResistanceLevel { Price = price });
    }
}
