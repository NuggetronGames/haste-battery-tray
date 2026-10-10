namespace HasteBattery;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Single instance: a second launch (e.g. at startup plus manual) just exits quietly.
        using var mutex = new Mutex(true, "HasteBattery.SingleInstance", out bool created);
        if (!created) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());   // tray only: no window is shown at launch
    }
}
