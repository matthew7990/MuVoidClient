namespace OzViewer;

static class Paths
{
    private static string GetWorkspaceRoot()
    {
        // 1) Desde CurrentDirectory (ej. al ejecutar desde raíz del repo)
        var candidates = new List<string>
        {
            Environment.CurrentDirectory,
            Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
            Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, '/')) ?? ""
        };

        foreach (var dir in candidates.Distinct().Where(d => !string.IsNullOrEmpty(d)))
        {
            var current = Path.GetFullPath(dir);
            for (int i = 0; i < 12; i++)
            {
                var iface = Path.Combine(current, "Source", "src", "bin", "Data", "Interface");
                if (Directory.Exists(iface))
                    return current;
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || parent == current) break;
                current = parent;
            }
        }

        // Fallback: asumir que estamos en tools/OzViewer
        var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return fallback;
    }

    /// <summary>Ruta fija al Interface del juego: Source\src\bin\Data\Interface</summary>
    public static string DefaultInterfaceFolder =>
        Path.Combine(GetWorkspaceRoot(), "Source", "src", "bin", "Data", "Interface");

    public static string WorkspaceRoot => GetWorkspaceRoot();
}
