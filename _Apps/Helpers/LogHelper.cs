using System.Diagnostics;

namespace LanobeReader.Helpers;

public static class LogHelper
{
    public static void Info(string className, string message) => Write("INFO", className, message);
    public static void Warn(string className, string message) => Write("WARN", className, message);
    public static void Error(string className, string message) => Write("ERROR", className, message);

    private static void Write(string level, string className, string message)
    {
        var line = $"[LanobeReader][{className}] {level}: {message}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }
}
