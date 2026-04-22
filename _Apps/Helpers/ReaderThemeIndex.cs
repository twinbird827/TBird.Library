namespace LanobeReader.Helpers;

/// <summary>
/// Reader 画面の背景テーマ設定値。AppSetting "background_theme" に int で保存される。
/// XAML の DataTrigger は int リテラル ("0" / "1" / "2") のまま使用するため、
/// ここは C# 側の自己文書化用途。値を変える場合は ReaderPage.xaml の DataTrigger Value も要同期更新。
/// </summary>
public static class BackgroundTheme
{
    public const int Light = 0;
    public const int Dark = 1;
    public const int Sepia = 2;
}

/// <summary>
/// Reader 画面の行間設定値。AppSetting "line_spacing" に int で保存される。
/// </summary>
public static class LineSpacing
{
    public const int Compact = 0;   // CSS line-height: 1.4
    public const int Normal = 1;    // CSS line-height: 1.7  (default)
    public const int Relaxed = 2;   // CSS line-height: 2.1
}
