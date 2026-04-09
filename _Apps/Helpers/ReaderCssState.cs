namespace LanobeReader.Helpers;

/// <summary>
/// ReaderHtmlBuilder に渡す CSS カスタムプロパティ値のスナップショット。
/// 将来、ReaderWebView から実行時差し替え用 Bindable としても使えるよう
/// record の value equality を採用している。
/// </summary>
public sealed record ReaderCssState(
    double FontSizePx,
    double LineHeight,
    string BackgroundHex,
    string ForegroundHex);
