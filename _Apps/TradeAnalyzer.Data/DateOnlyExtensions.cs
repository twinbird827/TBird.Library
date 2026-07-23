namespace TradeAnalyzer.Data;

/// <summary>
/// 日付の ISO 8601 文字列化（yyyy-MM-dd）を 1 か所へ集約する。SQL 文字列・CLI 引数・Claude 注入事実・
/// API クエリ等、機械が読む日付補間はすべてこれを使うこと（culture 未指定の <c>$"{t}"</c> はレビューで
/// 複数回再発した既知の欠陥クラス）。
/// DateOnly の "O"（round-trip 指定子）は culture・暦非依存で常にグレゴリオ暦の yyyy-MM-dd を生成するため、
/// 非グレゴリオ暦カルチャ（th-TH=仏暦等）のホストでも年がずれない（InvariantCulture 明示と出力同一）。
/// </summary>
public static class DateOnlyExtensions
{
    public static string ToIso(this DateOnly d) => d.ToString("O");
}
