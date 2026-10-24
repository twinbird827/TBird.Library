namespace TradeAnalyzer.Core.Indicators;

/// <summary>
/// テクニカル指標（純関数）。入力は調整後終値などの時系列（営業日昇順・欠損なしを前提）。
/// 指標は暦日でなく営業日連番で計算する。欠損は呼出側で除去/補完してから渡すこと。
/// 戻り値は入力と同じ長さの配列で、計算に十分なデータが無い先頭要素は null。
/// </summary>
public static class TechnicalIndicators
{
    /// <summary>単純移動平均。</summary>
    public static double?[] Sma(IReadOnlyList<double> values, int period)
    {
        if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period));
        var result = new double?[values.Count];
        double sum = 0;
        for (int i = 0; i < values.Count; i++)
        {
            sum += values[i];
            if (i >= period) sum -= values[i - period];
            if (i >= period - 1) result[i] = sum / period;
        }
        return result;
    }

    /// <summary>指数移動平均（初項は最初の period 個の SMA でシード）。</summary>
    public static double?[] Ema(IReadOnlyList<double> values, int period)
    {
        if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period));
        var result = new double?[values.Count];
        if (values.Count < period) return result;

        double k = 2.0 / (period + 1);
        double seed = 0;
        for (int i = 0; i < period; i++) seed += values[i];
        double ema = seed / period;
        result[period - 1] = ema;
        for (int i = period; i < values.Count; i++)
        {
            ema = values[i] * k + ema * (1 - k);
            result[i] = ema;
        }
        return result;
    }

    /// <summary>RSI（Wilder 平滑）。既定 period=14。0〜100。</summary>
    public static double?[] Rsi(IReadOnlyList<double> closes, int period = 14)
    {
        if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period));
        var result = new double?[closes.Count];
        if (closes.Count <= period) return result;

        double gainSum = 0, lossSum = 0;
        for (int i = 1; i <= period; i++)
        {
            double diff = closes[i] - closes[i - 1];
            if (diff >= 0) gainSum += diff; else lossSum -= diff;
        }
        double avgGain = gainSum / period;
        double avgLoss = lossSum / period;
        result[period] = ComputeRsi(avgGain, avgLoss);

        for (int i = period + 1; i < closes.Count; i++)
        {
            double diff = closes[i] - closes[i - 1];
            double gain = diff > 0 ? diff : 0;
            double loss = diff < 0 ? -diff : 0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
            result[i] = ComputeRsi(avgGain, avgLoss);
        }
        return result;
    }

    private static double ComputeRsi(double avgGain, double avgLoss)
    {
        if (avgLoss == 0) return 100.0;
        double rs = avgGain / avgLoss;
        return 100.0 - 100.0 / (1 + rs);
    }

    /// <summary>ATR（Wilder 平滑）。high/low/close は同じ長さ・同じ並び。既定 period=14。</summary>
    public static double?[] Atr(
        IReadOnlyList<double> highs, IReadOnlyList<double> lows, IReadOnlyList<double> closes, int period = 14)
    {
        if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period));
        int n = closes.Count;
        if (highs.Count != n || lows.Count != n)
            throw new ArgumentException("high/low/close must have equal length.");

        var result = new double?[n];
        if (n <= period) return result;

        var tr = new double[n];
        tr[0] = highs[0] - lows[0];
        for (int i = 1; i < n; i++)
        {
            double h = highs[i], l = lows[i], pc = closes[i - 1];
            tr[i] = Math.Max(h - l, Math.Max(Math.Abs(h - pc), Math.Abs(l - pc)));
        }

        double sum = 0;
        for (int i = 1; i <= period; i++) sum += tr[i];
        double atr = sum / period;
        result[period] = atr;
        for (int i = period + 1; i < n; i++)
        {
            atr = (atr * (period - 1) + tr[i]) / period;
            result[i] = atr;
        }
        return result;
    }
}
