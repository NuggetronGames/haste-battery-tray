using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace HasteBattery;

public static class TrayIconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>Draws the text as large colored digits on a transparent square icon.</summary>
    public static Icon Render(string text, Color color)
    {
        int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // Fit the text to the icon width: shrink until it fits.
            float px = size * (text.Length >= 3 ? 0.72f : 1.0f);
            Font font = new("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
            SizeF measured = g.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic);
            while (measured.Width > size && px > 6)
            {
                font.Dispose();
                px -= 0.5f;
                font = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
                measured = g.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic);
            }

            using (font)
            using (var brush = new SolidBrush(color))
            using (var shadow = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
            using (var fmt = new StringFormat(StringFormat.GenericTypographic)
                   { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var rect = new RectangleF(0, 0, size, size);
                var shadowRect = new RectangleF(0.7f, 0.7f, size, size);
                g.DrawString(text, font, shadow, shadowRect, fmt);
                g.DrawString(text, font, brush, rect, fmt);
            }
        }

        IntPtr h = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(h).Clone(); }
        finally { DestroyIcon(h); }
    }
}
