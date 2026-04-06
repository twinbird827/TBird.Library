using System.Diagnostics;

namespace LanobeReader.Helpers;

public static class LogHelper
{
    public static void Info(string className, string message)
    {
        Debug.WriteLine($"[LanobeReader][{className}] {message}");
    }

    public static void Warn(string className, string message)
    {
        Debug.WriteLine($"[LanobeReader][{className}] WARN: {message}");
    }

    public static void Error(string className, string message)
    {
        Debug.WriteLine($"[LanobeReader][{className}] ERROR: {message}");
    }
}
