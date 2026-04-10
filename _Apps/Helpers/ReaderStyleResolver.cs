namespace LanobeReader.Helpers;

/// <summary>
/// Reader画面のスタイル値解決。Colors.xamlのテーマ色をリソースから取得し、
/// 行間インデックスからCSS line-height値を解決する。
/// ReaderHtmlBuilder（初回HTML生成）とReaderWebView（ライブCSS差し替え）の両方から使用。
/// </summary>
public static class ReaderStyleResolver
{
    public static (string bg, string fg) ResolveThemeColors(int themeIndex)
    {
        var bgKey = themeIndex switch { 1 => "ThemeDarkBg", 2 => "ThemeSepiaBg", _ => "ThemeWhiteBg" };
        var fgKey = themeIndex switch { 1 => "ThemeDarkText", 2 => "ThemeSepiaText", _ => "ThemeWhiteText" };

        var bg = Application.Current!.Resources.TryGetValue(bgKey, out var b) && b is Color bc
            ? ColorToHex(bc) : "#FFFFFF";
        var fg = Application.Current!.Resources.TryGetValue(fgKey, out var f) && f is Color fc
            ? ColorToHex(fc) : "#212121";
        return (bg, fg);
    }

    public static double ResolveLineHeight(int lineSpacingIndex) => lineSpacingIndex switch
    {
        0 => 1.4,
        2 => 2.1,
        _ => 1.7,
    };

    public static string ColorToHex(Color c) =>
        $"#{(int)(c.Red * 255):X2}{(int)(c.Green * 255):X2}{(int)(c.Blue * 255):X2}";
}
