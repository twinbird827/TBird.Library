namespace ComicViewer.Logic;

public static class EntryRules
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif",
    };

    private static readonly HashSet<string> IgnoredNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "__MACOSX", "Thumbs.db", "desktop.ini",
    };

    public static bool IsIgnored(string name) => name.StartsWith('.') || IgnoredNames.Contains(name);

    public static bool IsImage(string name) => ImageExtensions.Contains(Path.GetExtension(name));

    public static bool IsZip(string name) => Path.GetExtension(name).Equals(".zip", StringComparison.OrdinalIgnoreCase);

    public static bool IsPdf(string name) => Path.GetExtension(name).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public static string KindBadge(string path) => IsZip(path) ? "ZIP" : IsPdf(path) ? "PDF" : "PIC";

    /// <summary>zip エントリのフルパスのどこかに無視対象（__MACOSX 等）が含まれるか。</summary>
    public static bool IsIgnoredPath(string path) => path.Split('/', '\\').Any(x => x.Length > 0 && IsIgnored(x));

    public static string Combine(string parent, string name) => parent.Length == 0 ? name : $"{parent}/{name}";

    public static string Parent(string path)
    {
        var i = path.LastIndexOf('/');
        return i < 0 ? "" : path[..i];
    }

    public static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];
}
