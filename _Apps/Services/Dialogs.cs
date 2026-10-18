namespace ComicViewer.Services;

/// <summary>表示中の Shell 上でダイアログを出す。</summary>
public static class Dialogs
{
    public static Task AlertAsync(string message)
        => Shell.Current?.DisplayAlertAsync("", message, "OK") ?? Task.CompletedTask;

    public static Task<bool> ConfirmAsync(string message, string accept)
        => Shell.Current?.DisplayAlertAsync("", message, accept, "キャンセル") ?? Task.FromResult(false);

    /// <summary>キャンセル時は null。</summary>
    public static async Task<string?> ActionSheetAsync(string title, params string[] buttons)
    {
        if (Shell.Current is null) return null;
        var result = await Shell.Current.DisplayActionSheetAsync(title, "キャンセル", null, buttons);
        return buttons.Contains(result) ? result : null;
    }
}
