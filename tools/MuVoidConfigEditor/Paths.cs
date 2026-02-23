namespace MuVoidConfigEditor;

static class Paths
{
    private static string GetWorkspaceRoot()
    {
        var candidates = new[]
        {
            Environment.CurrentDirectory,
            Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory
        };

        foreach (var dir in candidates.Distinct().Where(d => !string.IsNullOrEmpty(d)))
        {
            var current = Path.GetFullPath(dir);
            for (int i = 0; i < 12; i++)
            {
                var local = Path.Combine(current, "Source", "src", "bin", "Data", "Local");
                if (Directory.Exists(local))
                    return current;
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || parent == current) break;
                current = parent;
            }
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    public static string DefaultDataLocal => Path.Combine(GetWorkspaceRoot(), "Source", "src", "bin", "Data", "Local");
    public static string DefaultServerListPath => Path.Combine(DefaultDataLocal, "ServerList.bmd");
}
