namespace ModuWeb;

/// <summary>
/// A simple static logger class that outputs colored log messages to the console.
/// Supports info, warning, error levels and raw output without formatting.
/// </summary>
public class Logger
{
    enum LogType
    {
        Info,
        Warn,
        Error,
        None
    }

    /// <summary>
    /// Logs an error message to the console in red color.
    /// </summary>
    /// <param name="obj">The message or object to log.</param>
    /// <param name="title">An optional title or context label for the message.</param>
    public static void Error(object obj, string title = null)
    {
        Console.Write($"\u001b[0;31m[ERROR]\u001b[0m {(title == null ? "" : $"[{title}] ")}{obj}\n");
    }

    /// <summary>
    /// Logs an informational message to the console in cyan color.
    /// </summary>
    /// <param name="obj">The message or object to log.</param>
    /// <param name="title">An optional title or context label for the message.</param>
    public static void Info(object obj, string title = null)
    {
        Console.Write($"\u001b[0;36m[INFO]\u001b[0m {(title == null ? "" : $"[{title}] ")}{obj}\n");
    }

    /// <summary>
    /// Logs a warning message to the console in blue color.
    /// </summary>
    /// <param name="obj">The message or object to log.</param>
    /// <param name="title">An optional title or context label for the message.</param>
    public static void Warn(object obj, string title = null)
    {
        Console.Write($"\u001b[0;34m[WARN]\u001b[0m {(title == null ? "" : $"[{title}] ")}{obj}\n");
    }

    /// <summary>
    /// Writes raw text to the console without any formatting or newline.
    /// </summary>
    /// <param name="obj">The raw text or object to output.</param>
    public static void Raw(object obj)
    {
        Console.Write(obj);
    }
}
