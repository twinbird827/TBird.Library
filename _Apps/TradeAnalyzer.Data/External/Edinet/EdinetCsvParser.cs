using System.Globalization;
using System.IO.Compression;
using System.Text;
using TradeAnalyzer.Data.Entities;

namespace TradeAnalyzer.Data.External.Edinet;

/// <summary>
/// EDINET type=5（XBRL→CSV）の ZIP を展開し、主要財務科目を <see cref="EdinetFinFact"/> に抽出する。
/// CSV は UTF-16LE・タブ区切り（<see cref="Encoding.Unicode"/> で BOM 判定可、追加プロバイダ不要）。
/// MVP は少数の主要科目に限定。要素ID対応表は本クラスに定数化。
/// 同一科目に複数候補があれば「連結・当年度コンテキスト」を優先して1件採用。
/// </summary>
public class EdinetCsvParser
{
    // 要素ID → 正規化科目名。有報「主要な経営指標等」(SummaryOfBusinessResults) を主対象とし、
    // 実データ(jpcrp030000-asr 2025-06-25) で要素ID・列名を確認済み。
    private static readonly Dictionary<string, string> ElementMap = new(StringComparer.Ordinal)
    {
        // 売上高 / 収益
        ["jpcrp_cor:NetSalesSummaryOfBusinessResults"] = "Sales",
        ["jpcrp_cor:RevenueIFRSSummaryOfBusinessResults"] = "Sales",
        ["jppfs_cor:NetSales"] = "Sales",
        // 経常利益（サマリーで一般的）
        ["jpcrp_cor:OrdinaryIncomeLossSummaryOfBusinessResults"] = "OrdinaryProfit",
        // 営業利益（サマリーに無い会社も多い。財務諸表本体から補完）
        ["jpcrp_cor:OperatingIncomeLossSummaryOfBusinessResults"] = "OperatingProfit",
        ["jppfs_cor:OperatingIncome"] = "OperatingProfit",
        // 当期純利益（親会社株主帰属）
        ["jpcrp_cor:ProfitLossAttributableToOwnersOfParentSummaryOfBusinessResults"] = "NetProfit",
        // 純資産
        ["jpcrp_cor:NetAssetsSummaryOfBusinessResults"] = "NetAssets",
        ["jppfs_cor:NetAssets"] = "NetAssets",
        // 自己資本比率
        ["jpcrp_cor:EquityToAssetRatioSummaryOfBusinessResults"] = "EquityRatio",
        // 1株当たり純資産 / 1株当たり利益
        ["jpcrp_cor:NetAssetsPerShareSummaryOfBusinessResults"] = "Bps",
        ["jpcrp_cor:BasicEarningsLossPerShareSummaryOfBusinessResults"] = "Eps",
    };

    // CSV ヘッダ列名（日本語）。位置はファイルで一定でないため名前で索引する。
    private const string ColElementId = "要素ID";
    private const string ColContextId = "コンテキストID";
    private const string ColConsolidated = "連結・個別";
    private const string ColUnit = "単位";
    private const string ColValue = "値";

    /// <summary>ZIP バイト列を解析し、選別済みの財務事実を返す。</summary>
    public List<EdinetFinFact> Parse(byte[] zipBytes, string docId, string? code, DateOnly? periodEnd)
    {
        var candidates = new List<EdinetFinFact>();

        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) continue;
            using var es = entry.Open();
            // UTF-16LE。BOM 判定を有効化。
            using var reader = new StreamReader(es, Encoding.Unicode, detectEncodingFromByteOrderMarks: true);
            ParseCsv(reader, docId, code, periodEnd, candidates);
        }

        return SelectBest(candidates);
    }

    private static void ParseCsv(
        StreamReader reader, string docId, string? code, DateOnly? periodEnd, List<EdinetFinFact> sink)
    {
        var header = reader.ReadLine();
        if (header == null) return;
        var cols = header.Split('\t');
        int iElem = IndexOf(cols, ColElementId);
        int iCtx = IndexOf(cols, ColContextId);
        int iCons = IndexOf(cols, ColConsolidated);
        int iUnit = IndexOf(cols, ColUnit);
        int iVal = IndexOf(cols, ColValue);
        if (iElem < 0 || iVal < 0) return; // 想定外フォーマットはスキップ

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var f = line.Split('\t');
            if (iElem >= f.Length) continue;
            var elementId = f[iElem].Trim().Trim('"'); // 値は引用符付き("...")なので除去
            if (!ElementMap.TryGetValue(elementId, out var factName)) continue;

            var ctx = Get(f, iCtx);
            var cons = Get(f, iCons);
            var unit = Get(f, iUnit);
            var rawVal = Get(f, iVal);

            sink.Add(new EdinetFinFact
            {
                DocId = docId,
                Code = code,
                ElementId = elementId,
                FactName = factName,
                ContextId = ctx,
                IsConsolidated = IsConsolidated(cons, ctx),
                Value = ParseValue(rawVal),
                Unit = unit,
                PeriodEnd = periodEnd,
            });
        }
    }

    /// <summary>科目ごとに連結・当年度コンテキストを優先して1件選ぶ。</summary>
    private static List<EdinetFinFact> SelectBest(List<EdinetFinFact> candidates)
    {
        var result = new List<EdinetFinFact>();
        foreach (var group in candidates.GroupBy(c => c.FactName))
        {
            EdinetFinFact? best = null;
            int bestScore = int.MinValue;
            foreach (var c in group)
            {
                int score = (c.IsConsolidated ? 2 : 0)
                    + (c.ContextId != null && c.ContextId.StartsWith("CurrentYear", StringComparison.Ordinal) ? 1 : 0)
                    + (c.Value.HasValue ? 1 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            if (best != null) result.Add(best);
        }
        return result;
    }

    private static bool IsConsolidated(string? consolidatedColumn, string? contextId)
    {
        // 財務諸表本体(jppfs)は「連結・個別」列に連結/個別が入る。
        if (consolidatedColumn != null && consolidatedColumn.Contains("連結")) return true;
        if (consolidatedColumn != null && consolidatedColumn.Contains("個別")) return false;
        // サマリー(jpcrp)は列が「その他」で、個別値のみ contextId に _NonConsolidatedMember が付く。
        if (contextId != null && contextId.Contains("NonConsolidated")) return false;
        if (contextId != null && contextId.Contains("Consolidated")) return true;
        // 接尾辞なしの主要指標は連結（連結を出さない単体会社も同型のため SelectBest で代替）。
        return true;
    }

    private static double? ParseValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw!.Trim();
        if (s == "－" || s == "-" || s == "—") return null;
        s = s.Replace(",", string.Empty);
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static int IndexOf(string[] cols, string name)
    {
        for (int i = 0; i < cols.Length; i++)
            if (cols[i].Trim().Trim('"') == name) return i;
        return -1;
    }

    private static string? Get(string[] fields, int index)
        => index >= 0 && index < fields.Length ? fields[index].Trim().Trim('"') : null;
}
