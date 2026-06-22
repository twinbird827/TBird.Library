namespace TradeAnalyzer.Data.Entities;

/// <summary>
/// EDINET 書類一覧の1件（GET /documents.json?type=2 の results[]）。
/// </summary>
public class EdinetDocument
{
    /// <summary>書類管理番号（`docID`。主キー）。</summary>
    public string DocId { get; set; } = string.Empty;

    /// <summary>EDINETコード（`edinetCode`。提出者）。</summary>
    public string? EdinetCode { get; set; }

    /// <summary>証券コード（`secCode`。通常5桁・末尾0）。J-Quants Code との突合用。</summary>
    public string? SecCode { get; set; }

    /// <summary>正規化済み4桁銘柄コード（secCode から導出。J-Quants Code と照合）。</summary>
    public string? NormalizedCode { get; set; }

    /// <summary>書類種別コード（`docTypeCode`。120=有価証券報告書）。</summary>
    public string? DocTypeCode { get; set; }

    /// <summary>様式コード（`formCode`）。</summary>
    public string? FormCode { get; set; }

    /// <summary>提出日（一覧取得対象日）。先読み防止の基準日に使用。</summary>
    public DateOnly SubmitDate { get; set; }

    /// <summary>対象期間開始（`periodStart`）。</summary>
    public DateOnly? PeriodStart { get; set; }
    /// <summary>対象期間終了（`periodEnd`）。</summary>
    public DateOnly? PeriodEnd { get; set; }

    /// <summary>CSV提供フラグ（`csvFlag`。"1"で取得可）。</summary>
    public string? CsvFlag { get; set; }
    /// <summary>XBRL提供フラグ（`xbrlFlag`）。</summary>
    public string? XbrlFlag { get; set; }

    /// <summary>CSV(ZIP)取得・解析済みか。</summary>
    public bool Parsed { get; set; }
}

/// <summary>
/// EDINET CSV(type=5) から抽出した財務事実の1要素。
/// 主要科目（売上高・営業利益・純資産・自己資本比率 等）に限定して格納。
/// コンテキストは連結優先・無ければ個別。
/// </summary>
public class EdinetFinFact
{
    /// <summary>サロゲートキー。</summary>
    public long Id { get; set; }

    /// <summary>由来書類（`docID`）。</summary>
    public string DocId { get; set; } = string.Empty;

    /// <summary>正規化済み4桁銘柄コード。</summary>
    public string? Code { get; set; }

    /// <summary>要素ID（XBRL ElementId / 行の「要素ID」列）。</summary>
    public string ElementId { get; set; } = string.Empty;

    /// <summary>正規化した科目名（売上高/営業利益/純資産/自己資本比率 等）。</summary>
    public string FactName { get; set; } = string.Empty;

    /// <summary>コンテキストID（CurrentYearDuration 等。連結/個別の判別に使用）。</summary>
    public string? ContextId { get; set; }

    /// <summary>連結値か（true=連結, false=個別）。</summary>
    public bool IsConsolidated { get; set; }

    /// <summary>値（数値化できたもの。テキスト科目は null）。</summary>
    public double? Value { get; set; }

    /// <summary>単位（`unitRef` 等。JPY/Pure 等）。</summary>
    public string? Unit { get; set; }

    /// <summary>対象期間終了日（先読み防止の参考。提出日でガードするのが主）。</summary>
    public DateOnly? PeriodEnd { get; set; }
}
