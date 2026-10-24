using TradeAnalyzer.Data.Entities;

namespace TradeAnalyzer.Data.External.Edinet;

/// <summary>
/// <see cref="EdinetFinFact"/> の生値（<see cref="EdinetFinFact.Value"/>）を単位
/// （<see cref="EdinetFinFact.Unit"/>）に応じて円基準へ換算する、単一の読み手ヘルパ。
/// パーサは桁を正規化せず生値＋Unit を保持するため、金額の比較・利用は必ず本ヘルパを通す。
/// 段階2で EDINET 財務から時価総額近似（ApproxMarketCap）を配線する際もこの変換を共有する。
/// 分類:
///   - 金額系（売上高・営業利益・経常利益・純利益・純資産）: Unit 係数を乗じて円換算。
///   - 1株系（BPS/EPS）: 「円/株」なので Unit 係数を乗じる（多くは円＝係数1）。
///   - 比率系（自己資本比率）: 換算せず生値（％ or 比率のまま）。
/// </summary>
public static class EdinetFinFactConverter
{
    // 換算対象外（比率・無次元）の科目。FactName は EdinetCsvParser.ElementMap の値と一致させる。
    private static readonly HashSet<string> RatioFacts = new(StringComparer.Ordinal) { "EquityRatio" };

    /// <summary>
    /// 単位文字列 → 円換算係数。百万円=1e6 / 千円=1e3 / 円・JPY・Pure・null・未知=1。
    /// </summary>
    public static double UnitFactor(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return 1d;
        var u = unit.Trim();
        if (u.Contains("百万")) return 1_000_000d;
        if (u.Contains("千")) return 1_000d;      // 千円
        return 1d;                                  // 円 / JPY / Pure / その他
    }

    /// <summary>
    /// 金額系・1株系は円換算した値、比率系は生値、<see cref="EdinetFinFact.Value"/> 欠損は null を返す。
    /// </summary>
    public static double? ToYen(EdinetFinFact fact)
    {
        if (fact.Value is not double v) return null;
        if (RatioFacts.Contains(fact.FactName)) return v; // 比率は換算しない
        return v * UnitFactor(fact.Unit);
    }
}
