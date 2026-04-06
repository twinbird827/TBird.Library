namespace LanobeReader.Helpers;

public static class ThemeHelper
{
    public static (Color background, Color text) GetThemeColors(int themeIndex) => themeIndex switch
    {
        1 => (Color.FromArgb("#121212"), Color.FromArgb("#E0E0E0")), // Dark
        2 => (Color.FromArgb("#F5E6C8"), Color.FromArgb("#3E2C1C")), // Sepia
        _ => (Color.FromArgb("#FFFFFF"), Color.FromArgb("#212121")), // White (default)
    };

    public static double GetLineHeight(int lineSpacingIndex) => lineSpacingIndex switch
    {
        0 => 1.4,
        2 => 2.1,
        _ => 1.7, // Normal (default)
    };
}
