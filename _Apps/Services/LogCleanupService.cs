using System.Globalization;
using TBird.Core;

namespace ComicViewer.Services;

public static class LogCleanupService
{
    private const int KeepDays = 30;

    public static void Run()
    {
        try
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "log");
            if (!Directory.Exists(dir)) return;
            var limit = DateTime.Today.AddDays(-KeepDays);
            foreach (var file in Directory.EnumerateFiles(dir, "*.log"))
            {
                if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date < limit)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }
}
