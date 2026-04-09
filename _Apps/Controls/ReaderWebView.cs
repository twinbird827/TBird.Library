namespace LanobeReader.Controls;

/// <summary>
/// Reader 画面の縦書き表示用 WebView。
/// HtmlSource (string) を bind すると HTML を再ロードする。
/// プロパティ変更通知は UI スレッドからのみ発生する前提で実装している
/// （MAUI の BindableProperty 既定動作）。
/// 将来、CSS 変数のライブ差し替え用 CssVariables Bindable を追加する予定
/// （plan_2026-04-09_pr3b-reader-live-css.md）。
/// </summary>
public sealed class ReaderWebView : WebView
{
    public static readonly BindableProperty HtmlSourceProperty = BindableProperty.Create(
        nameof(HtmlSource),
        typeof(string),
        typeof(ReaderWebView),
        default(string),
        propertyChanged: OnHtmlSourceChanged);

    public string? HtmlSource
    {
        get => (string?)GetValue(HtmlSourceProperty);
        set => SetValue(HtmlSourceProperty, value);
    }

    private static void OnHtmlSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (ReaderWebView)bindable;
        var html = newValue as string;
        if (string.IsNullOrEmpty(html))
        {
            return;
        }
        self.Source = new HtmlWebViewSource { Html = html };
    }
}
