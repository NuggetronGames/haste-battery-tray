using System.Text.Json;
using Microsoft.Win32;

namespace HasteBattery;

public sealed class AppSettings
{
    // Tray colors, stored as hex strings so settings.json stays readable.
    public string ColorHigh { get; set; } = "#2ECC40";      // 70-100%
    public string ColorMedium { get; set; } = "#FFDC00";    // 30-70%
    public string ColorLow { get; set; } = "#FF851B";       // 10-30%
    public string ColorCritical { get; set; } = "#FF4136";  // 10% or less

    public int PollSeconds { get; set; } = 60;

    // HID protocol knobs. Defaults come from a USB capture of HyperX NGENUITY talking to a
    // Pulsefire Haste Wireless dongle (03F0:028E): it sends 51 00 00... about once a second and
    // the dongle replies 51 00 00 09 5D 00 0A 1B 00 07 10 02 04..., where byte 4 (0x5D = 93)
    // is believed to be the battery percentage (pending confirmation against NGENUITY).
    public int VendorId { get; set; } = 0x03F0;
    public List<int> ProductIds { get; set; } = new();      // empty = any HyperX HID device
    public string ProbeHex { get; set; } = "51";
    public string HeaderHex { get; set; } = "51 00 00";
    public int BatteryOffset { get; set; } = 4;             // offset from start of header
    public int ChargingOffset { get; set; } = -1;           // offset from start of header, -1 = unknown

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HasteBattery");

    static string FilePath => Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* fall through to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public Color ColorFor(int percent)
    {
        string hex = percent >= 70 ? ColorHigh
                   : percent >= 30 ? ColorMedium
                   : percent > 10 ? ColorLow
                   : ColorCritical;
        try { return ColorTranslator.FromHtml(hex); }
        catch { return Color.White; }
    }
}

public static class StartupManager
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "HasteBattery";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, "\"" + Environment.ProcessPath + "\"");
        else
            key.DeleteValue(ValueName, false);
    }
}
