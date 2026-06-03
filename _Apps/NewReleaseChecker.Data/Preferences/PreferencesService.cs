using System.Text.Json;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Services;

namespace NewReleaseChecker.Data.Preferences;

/// <summary>MAUI Preferences による設定値管理（要件 §5.5）。除外キーワードは JSON 文字列で保存。</summary>
public sealed class PreferencesService : IPreferencesService
{
    private const string KeyNotification = "notification_enabled";
    private const string KeyAutoCheck = "auto_check_enabled";
    private const string KeyInterval = "auto_check_interval";
    private const string KeyExclude = "exclude_keywords";

    private static readonly string[] DefaultExcludes = { "分冊", "単話", "話売り" };

    public bool NotificationEnabled
    {
        get => Microsoft.Maui.Storage.Preferences.Get(KeyNotification, true);
        set => Microsoft.Maui.Storage.Preferences.Set(KeyNotification, value);
    }

    public bool AutoCheckEnabled
    {
        get => Microsoft.Maui.Storage.Preferences.Get(KeyAutoCheck, true);
        set => Microsoft.Maui.Storage.Preferences.Set(KeyAutoCheck, value);
    }

    public string AutoCheckInterval
    {
        get => Microsoft.Maui.Storage.Preferences.Get(KeyInterval, CheckIntervals.DefaultKey);
        set => Microsoft.Maui.Storage.Preferences.Set(KeyInterval, value);
    }

    public IReadOnlyList<string> ExcludeKeywords
    {
        get
        {
            var json = Microsoft.Maui.Storage.Preferences.Get(KeyExclude, string.Empty);
            if (string.IsNullOrEmpty(json)) return DefaultExcludes;
            try
            {
                return JsonSerializer.Deserialize<string[]>(json) ?? DefaultExcludes;
            }
            catch
            {
                return DefaultExcludes;
            }
        }
        set => Microsoft.Maui.Storage.Preferences.Set(KeyExclude, JsonSerializer.Serialize(value.ToArray()));
    }

    public void ResetExcludeKeywords() => ExcludeKeywords = DefaultExcludes;
}
