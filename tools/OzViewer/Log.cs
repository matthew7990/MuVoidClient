namespace OzViewer;

static class Log
{
    private static string GetLogPath()
    {
        var dir = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrEmpty(dir))
            dir = Environment.CurrentDirectory;
        if (string.IsNullOrEmpty(dir))
            dir = System.IO.Path.GetTempPath();
        return System.IO.Path.Combine(dir, "OzViewer.log");
    }

    public static string Path => GetLogPath();

    private static readonly object Lock = new();

    public static void Write(string msg)
    {
        try
        {
            var logPath = GetLogPath();
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}";
            lock (Lock)
            {
                File.AppendAllText(logPath, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(GetLogPath() + ".err", ex.ToString()); } catch { }
        }
    }

    public static void Error(string msg, Exception? ex = null)
    {
        var full = ex != null ? $"{msg}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : msg;
        Write("ERROR: " + full.Replace("\r", "").Replace("\n", " | "));
    }
}
