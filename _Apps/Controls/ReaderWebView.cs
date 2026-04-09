using System.Diagnostics;
using System.Globalization;
using LanobeReader.Helpers;

namespace LanobeReader.Controls;

/// <summary>
/// Reader 画面の縦書き表示用 WebView。
///
/// 本コントロールのプロパティ変更通知は UI スレッドからのみ発生する前提で実装されている
/// （MAUI の BindableProperty 既定動作）。別スレッドから HtmlSource / CssVariables を
/// 書き換える場合は呼び出し側で Dispatcher 経由にすること。
/// </summary>
public sealed class ReaderWebView : WebView
{
    private bool _htmlLoaded;
    private ReaderCssState? _pendingCss;

    public ReaderWebView()
    {
        Navigated += OnNavigated;
    }

    // --- HtmlSource ---

    public static readonly BindableProperty HtmlSourceProperty = BindableProperty.Create(
        nameof(HtmlSource), typeof(string), typeof(ReaderWebView),
        default(string), propertyChanged: OnHtmlSourceChanged);

    public string? HtmlSource
    {
        get => (string?)GetValue(HtmlSourceProperty);
        set => SetValue(HtmlSourceProperty, value);
    }

    private static void OnHtmlSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (ReaderWebView)bindable;
        var html = newValue as string;
        if (string.IsNullOrEmpty(html)) return;

        // 新しい HTML をロードする直前に、古い document 向けの保留 CSS を破棄する。
        self._htmlLoaded = false;
        self._pendingCss = null;
        self.Source = new HtmlWebViewSource { Html = html };
    }

    // --- CssVariables ---

    public static readonly BindableProperty CssVariablesProperty = BindableProperty.Create(
        nameof(CssVariables), typeof(ReaderCssState), typeof(ReaderWebView),
        default(ReaderCssState), propertyChanged: OnCssVariablesChanged);

    public ReaderCssState? CssVariables
    {
        get => (ReaderCssState?)GetValue(CssVariablesProperty);
        set => SetValue(CssVariablesProperty, value);
    }

    private static void OnCssVariablesChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (ReaderWebView)bindable;
        if (newValue is not ReaderCssState state) return;

        if (self._htmlLoaded)
        {
            _ = self.ApplyCssAsync(state);
        }
        else
        {
            self._pendingCss = state;
        }
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result != WebNavigationResult.Success) return;
        _htmlLoaded = true;
        if (_pendingCss is not null)
        {
            var s = _pendingCss;
            _pendingCss = null;
            _ = ApplyCssAsync(s);
        }
    }

    private async Task ApplyCssAsync(ReaderCssState state)
    {
        var inv = CultureInfo.InvariantCulture;
        var js =
            "(function(){var s=document.documentElement.style;" +
            $"s.setProperty('--reader-fs','{state.FontSizePx.ToString("0.##", inv)}px');" +
            $"s.setProperty('--reader-lh','{state.LineHeight.ToString("0.##", inv)}');" +
            $"s.setProperty('--reader-bg','{state.BackgroundHex}');" +
            $"s.setProperty('--reader-fg','{state.ForegroundHex}');" +
            "})();";
        try
        {
            await EvaluateJavaScriptAsync(js);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReaderWebView] ApplyCssAsync failed: {ex}");
        }
    }
}
