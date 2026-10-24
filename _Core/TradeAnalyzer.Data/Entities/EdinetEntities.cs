namespace TradeAnalyzer.Data.Entities;

/// <summary>
/// EDINET 書類一覧の1件（GET /documents.json?type=2 の results[]）。
/// <para>
/// 保存方針: 当日提出の全書類メタを保存する（段階2で <c>TargetDocTypeCodes</c> を四半期/半期へ
/// 拡張する際の検出に有用なため絞り込まない）。同一 docID が複数のファイル日付一覧に再掲された場合
/// （提出処理日＋書類情報修正日＋開示不開示区分変更日）は、PK=<see cref="DocId"/> 単独ゆえ最初に取り込んだ
/// 一覧の <see cref="SubmitDate"/>（取込順依存で最小日付とは限らない）の行のみ保持し、再掲日の再挿入はスキップする（IngestEdinetAsync 参照）。
/// したがって本テーブルには対象外の書類も
/// <see cref="Parsed"/>=false / <see cref="NormalizedCode"/>=null で多数含まれる。
/// <b>消費契約</b>: 財務事実として利用する側は必ず <see cref="Parsed"/>==true かつ
/// <see cref="NormalizedCode"/>!=null の行のみを対象とすること（全行件数を母集団に使わない）。
/// </para>
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

    /// <summary>提出日（一覧取得対象日）。※現状 look-ahead ゲートには未使用（ゲートは DailyBar.Date 側で実装）。
    /// 再掲 docID は最初に取り込んだ一覧日（取込順依存で最小とは限らない）になるため、将来 look-ahead に使うなら
    /// submitDateTime 導入 or 最小提出日保持が必要。</summary>
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

    /// <summary>
    /// 生値（CSV の「値」をそのまま数値化。桁の正規化はしない）。テキスト科目は null。
    /// <b>金額の比較・利用時は必ず <see cref="Unit"/> と併せて
    /// <see cref="External.Edinet.EdinetFinFactConverter"/> で円換算すること</b>
    /// （会社により円/千円/百万円が混在し、生値同士の比較は 10^3〜10^6 倍の誤差を生む）。
    /// </summary>
    public double? Value { get; set; }

    /// <summary>単位（`unitRef` 等。JPY/円/千円/百万円/Pure 等）。<see cref="Value"/> の換算係数の根拠。</summary>
    public string? Unit { get; set; }

    /// <summary>対象期間終了日。※現状 look-ahead ゲートには未使用（ゲートは DailyBar.Date 側で実装）。
    /// 由来 docID が再掲の場合は取込順依存の日付になりうるため、将来 look-ahead に使うなら submitDateTime 導入
    /// or 最小提出日保持が必要。</summary>
    public DateOnly? PeriodEnd { get; set; }
}
