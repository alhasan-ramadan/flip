using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Flip;

enum OutputKind { Speaker, Headphones, Other }

/// <summary>Zeichnet das Taskleisten-Symbol: Lautsprecher oder Kopfhörer, einfarbig passend zur Taskleiste.</summary>
static class IconRenderer
{
    public static Icon Render(OutputKind kind, int? size = null)
    {
        int px = size ?? SystemInformation.SmallIconSize.Width;
        // Ein drittes Gerät (etwa eine Bluetooth-Box) bekommt den Lautsprecher in Grau.
        var color = kind == OutputKind.Other ? Color.FromArgb(0x8A, 0x8A, 0x8A)
            : LightTaskbar() ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.White;

        using var bitmap = new Bitmap(px, px, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (kind == OutputKind.Headphones)
                DrawHeadphones(g, px, color);
            else
                DrawSpeaker(g, px, color);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    static void DrawSpeaker(Graphics g, float s, Color color)
    {
        using var fill = new SolidBrush(color);
        g.FillPolygon(fill, [
            new PointF(s * 0.06f, s * 0.36f), new PointF(s * 0.24f, s * 0.36f), new PointF(s * 0.48f, s * 0.14f),
            new PointF(s * 0.48f, s * 0.86f), new PointF(s * 0.24f, s * 0.64f), new PointF(s * 0.06f, s * 0.64f),
        ]);
        using var pen = new Pen(color, Math.Max(1.6f, s * 0.1f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        foreach (float r in new[] { s * 0.2f, s * 0.38f })
            g.DrawArc(pen, s * 0.5f - r, s * 0.5f - r, r * 2, r * 2, -45, 90);
    }

    static void DrawHeadphones(Graphics g, float s, Color color)
    {
        using (var pen = new Pen(color, Math.Max(1.4f, s * 0.11f)))
            g.DrawArc(pen, s * 0.17f, s * 0.12f, s * 0.66f, s * 0.70f, 180, 180);
        using var fill = new SolidBrush(color);
        float cupWidth = s * 0.26f, cupHeight = s * 0.40f, top = s * 0.47f;
        using (var left = RoundedRect(new RectangleF(s * 0.06f, top, cupWidth, cupHeight), cupWidth * 0.35f))
            g.FillPath(fill, left);
        using (var right = RoundedRect(new RectangleF(s * 0.94f - cupWidth, top, cupWidth, cupHeight), cupWidth * 0.35f))
            g.FillPath(fill, right);
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    static bool LightTaskbar()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
    }

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);
}
