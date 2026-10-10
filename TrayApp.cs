using System.Diagnostics;

namespace HasteBattery;

public sealed class TrayApp : ApplicationContext
{
    readonly NotifyIcon _tray;
    readonly System.Windows.Forms.Timer _timer;
    readonly ToolStripMenuItem _status;
    AppSettings _s;
    BatteryReading? _last;
    DateTime _lastOk = DateTime.MinValue;
    Icon? _icon;
    bool _busy;
    SettingsForm? _settingsForm;

    public TrayApp()
    {
        _s = AppSettings.Load();

        _status = new ToolStripMenuItem("Battery: reading…") { Enabled = false };
        var menu = new ContextMenuStrip();
        menu.Items.Add(_status);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Refresh now", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add("Run diagnostics…", null, async (_, _) => await RunDiagnosticsAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowSettings();

        _timer = new System.Windows.Forms.Timer { Interval = Math.Max(10, _s.PollSeconds) * 1000 };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        UpdateUi();
        _ = RefreshAsync();
    }

    async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var settings = _s;
            var r = await Task.Run(() => HidBattery.TryRead(settings));
            if (r != null) { _last = r; _lastOk = DateTime.Now; }
        }
        catch { /* keep last known value */ }
        finally { _busy = false; }
        UpdateUi();
    }

    void UpdateUi()
    {
        string text;
        Color color;
        string tip;

        if (_last == null)
        {
            text = "--";
            color = Color.Gray;
            tip = "Pulsefire Haste: no reading yet (mouse asleep or not found)";
            _status.Text = "Battery: no reading yet";
        }
        else
        {
            text = _last.Percent.ToString();
            color = _s.ColorFor(_last.Percent);
            bool stale = DateTime.Now - _lastOk > TimeSpan.FromSeconds(Math.Max(10, _s.PollSeconds) * 3);
            string charging = _last.Charging == true ? ", charging" : "";
            tip = stale
                ? $"Pulsefire Haste: {_last.Percent}% (last seen {_lastOk:t})"
                : $"Pulsefire Haste: {_last.Percent}%{charging}";
            _status.Text = $"Battery: {_last.Percent}%{charging}" + (stale ? $" (as of {_lastOk:t})" : "");
        }

        var newIcon = TrayIconRenderer.Render(text, color);
        _tray.Icon = newIcon;
        _icon?.Dispose();
        _icon = newIcon;
        _tray.Text = tip.Length > 127 ? tip[..127] : tip;
    }

    void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }
        _settingsForm = new SettingsForm(_s);
        _settingsForm.FormClosed += (_, _) =>
        {
            if (_settingsForm?.DialogResult == DialogResult.OK)
            {
                _s = AppSettings.Load();
                _timer.Interval = Math.Max(10, _s.PollSeconds) * 1000;
                UpdateUi();
            }
            _settingsForm = null;
        };
        _settingsForm.Show();
    }

    async Task RunDiagnosticsAsync()
    {
        var settings = _s;
        string report = await Task.Run(() => HidBattery.Diagnostics(settings));
        Directory.CreateDirectory(AppSettings.Folder);
        string path = Path.Combine(AppSettings.Folder, "diagnostics.txt");
        File.WriteAllText(path, report);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    void ExitApp()
    {
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _icon?.Dispose();
        ExitThread();
    }
}
