namespace LanobeReader.Helpers;

public static class SettingsKeys
{
    public const string CACHE_MONTHS = "cache_months";
    public const string UPDATE_INTERVAL_HOURS = "update_interval_hours";
    public const string FONT_SIZE_SP = "font_size_sp";
    public const string BACKGROUND_THEME = "background_theme";
    public const string LINE_SPACING = "line_spacing";
    public const string EPISODES_PER_PAGE = "episodes_per_page";
    public const string PREFETCH_ENABLED = "prefetch_enabled";
    public const string REQUEST_DELAY_MS = "request_delay_ms";
    public const string VERTICAL_WRITING = "vertical_writing";
    public const string NOVEL_SORT_KEY = "novel_sort_key";
    public const string LAST_SCHEDULED_HOURS = "last_scheduled_hours";
    public const string AUTO_MARK_READ_ENABLED = "auto_mark_read_enabled";

    // Default values
    public const int DEFAULT_CACHE_MONTHS = 3;
    public const int DEFAULT_UPDATE_INTERVAL_HOURS = 6;
    public const int DEFAULT_FONT_SIZE_SP = 16;
    public const int DEFAULT_BACKGROUND_THEME = BackgroundTheme.Light;
    public const int DEFAULT_LINE_SPACING = LineSpacing.Normal;
    public const int DEFAULT_EPISODES_PER_PAGE = 50;
    public const int DEFAULT_PREFETCH_ENABLED = 1;
    public const int DEFAULT_REQUEST_DELAY_MS = 800;
    public const int DEFAULT_VERTICAL_WRITING = 0;
    public const int DEFAULT_AUTO_MARK_READ_ENABLED = 0;
    public const string DEFAULT_NOVEL_SORT_KEY = "updated_desc";
    public const int MIN_REQUEST_DELAY_MS = 500;
    public const int MAX_REQUEST_DELAY_MS = 2000;
}
