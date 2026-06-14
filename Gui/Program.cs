namespace GW2RaidsGui;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        AppPaths paths;
        try
        {
            paths = AppPaths.Resolve(args);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "GW2 Raid Summaries",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm(paths));
    }
}
