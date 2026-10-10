namespace HasteBattery;

public sealed class SettingsForm : Form
{
    readonly AppSettings _s;
    readonly Button[] _swatches = new Button[4];
    readonly string[] _labels = { "70 – 100%", "30 – 70%", "10 – 30%", "10% or less" };
    readonly string[] _colors = new string[4];
    readonly NumericUpDown _poll;
    readonly CheckBox _startup;

    public SettingsForm(AppSettings s)
    {
        _s = s;
        _colors[0] = s.ColorHigh;
        _colors[1] = s.ColorMedium;
        _colors[2] = s.ColorLow;
        _colors[3] = s.ColorCritical;

        Text = "Haste Battery – Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(320, 270);

        int y = 16;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            Controls.Add(new Label { Text = _labels[i], Left = 16, Top = y + 6, Width = 120 });
            var b = new Button { Left = 150, Top = y, Width = 150, Height = 28, FlatStyle = FlatStyle.Flat };
            b.Click += (_, _) => PickColor(idx);
            _swatches[i] = b;
            Controls.Add(b);
            PaintSwatch(i);
            y += 36;
        }

        Controls.Add(new Label { Text = "Check every (seconds)", Left = 16, Top = y + 6, Width = 130 });
        _poll = new NumericUpDown { Left = 150, Top = y + 2, Width = 70, Minimum = 10, Maximum = 3600, Value = Math.Clamp(s.PollSeconds, 10, 3600) };
        Controls.Add(_poll);
        y += 38;

        _startup = new CheckBox { Text = "Start with Windows (minimized to tray)", Left = 16, Top = y, Width = 290, Checked = StartupManager.IsEnabled() };
        Controls.Add(_startup);
        y += 40;

        var reset = new Button { Text = "Reset colors", Left = 16, Top = y, Width = 100 };
        reset.Click += (_, _) =>
        {
            var d = new AppSettings();
            _colors[0] = d.ColorHigh; _colors[1] = d.ColorMedium; _colors[2] = d.ColorLow; _colors[3] = d.ColorCritical;
            for (int i = 0; i < 4; i++) PaintSwatch(i);
        };
        var save = new Button { Text = "Save", Left = 150, Top = y, Width = 70 };
        save.Click += (_, _) =>
        {
            Commit();
            DialogResult = DialogResult.OK;   // closes the form after settings are saved
            Close();
        };
        var cancel = new Button { Text = "Cancel", Left = 230, Top = y, Width = 70, DialogResult = DialogResult.Cancel };
        Controls.AddRange(new Control[] { reset, save, cancel });
        AcceptButton = save;
        CancelButton = cancel;
    }

    void PaintSwatch(int i)
    {
        Color c;
        try { c = ColorTranslator.FromHtml(_colors[i]); } catch { c = Color.White; }
        _swatches[i].BackColor = c;
        _swatches[i].ForeColor = c.GetBrightness() > 0.5f ? Color.Black : Color.White;
        _swatches[i].Text = _colors[i].ToUpperInvariant();
    }

    void PickColor(int i)
    {
        using var dlg = new ColorDialog { FullOpen = true, Color = _swatches[i].BackColor };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _colors[i] = ColorTranslator.ToHtml(dlg.Color);
        PaintSwatch(i);
    }

    void Commit()
    {
        _s.ColorHigh = _colors[0];
        _s.ColorMedium = _colors[1];
        _s.ColorLow = _colors[2];
        _s.ColorCritical = _colors[3];
        _s.PollSeconds = (int)_poll.Value;
        _s.Save();
        try { StartupManager.SetEnabled(_startup.Checked); } catch { /* registry blocked by policy */ }
    }
}
