namespace OzViewer;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Log.Write("Inicio Main");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log.Error("UnhandledException", ex);
        };
        Application.ThreadException += (_, e) =>
        {
            Log.Error("ThreadException", e.Exception);
        };

        try
        {
            Log.Write("ApplicationConfiguration.Initialize");
            ApplicationConfiguration.Initialize();
            Log.Write("Creando MainForm");
            var form = new MainForm();

            if (args.Length > 0)
            {
                var path = args[0];
                Log.Write($"Args[0]={path}");
                if (Directory.Exists(path))
                    form.LoadFolder(path, showHudTab: true);
                else if (File.Exists(path))
                    form.LoadImage(path);
            }
            else if (Directory.Exists(Paths.DefaultInterfaceFolder))
            {
                form.LoadFolder(Paths.DefaultInterfaceFolder, showHudTab: true);
            }

            Log.Write("Application.Run");
            Application.Run(form);
            Log.Write("Fin Main (normal)");
        }
        catch (Exception ex)
        {
            Log.Error("Excepcion en Main", ex);
            var logPath = Log.Path;
            MessageBox.Show($"Error al iniciar:\n{ex.Message}\n\nLog: {logPath}", "OzViewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
