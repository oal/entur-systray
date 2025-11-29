using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace EnturSystray;

public static class IconGenerator
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    // Track icon handles per icon ID for proper cleanup
    private static readonly Dictionary<string, IntPtr> _iconHandles = new();

    public static Icon CreateBadgeIcon(string text, Color textColor, string iconId = "default")
    {
        // Clean up previous icon handle for this specific icon ID
        if (_iconHandles.TryGetValue(iconId, out var existingHandle) && existingHandle != IntPtr.Zero)
        {
            DestroyIcon(existingHandle);
            _iconHandles.Remove(iconId);
        }

        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        // Create rounded rectangle path - fill entire 32x32 space
        int cornerRadius = 6;
        using var path = CreateRoundedRectanglePath(0, 0, 31, 31, cornerRadius);

        // Draw black background
        using var bgBrush = new SolidBrush(Color.Black);
        graphics.FillPath(bgBrush, path);

        // Draw gray outline
        using var outlinePen = new Pen(Color.Gray, 1);
        graphics.DrawPath(outlinePen, path);

        // Choose font size based on text length
        var fontSize = text.Length switch
        {
            1 => 20,
            2 => 18,
            _ => 14
        };

        // Draw colored text centered
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(textColor);

        var size = graphics.MeasureString(text, font);

        graphics.DrawString(
            text,
            font,
            textBrush,
            (32 - size.Width) / 2,
            (32 - size.Height) / 2);

        var handle = bitmap.GetHicon();
        _iconHandles[iconId] = handle;
        return Icon.FromHandle(handle);
    }

    // Overload for backwards compatibility (yellow text)
    public static Icon CreateBadgeIcon(string text) => CreateBadgeIcon(text, Color.Yellow);

    // Overload for number input
    public static Icon CreateBadgeIcon(int number) => CreateBadgeIcon(number.ToString(), Color.Yellow);

    public static Icon CreateBadgeIcon(int number, Color textColor, string iconId = "default")
        => CreateBadgeIcon(number.ToString(), textColor, iconId);

    private static GraphicsPath CreateRoundedRectanglePath(int x, int y, int width, int height, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;

        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    public static Icon CreateDefaultIcon(string iconId = "default")
    {
        // Clean up previous icon handle for this specific icon ID
        if (_iconHandles.TryGetValue(iconId, out var existingHandle) && existingHandle != IntPtr.Zero)
        {
            DestroyIcon(existingHandle);
            _iconHandles.Remove(iconId);
        }

        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        // Create rounded rectangle path for background
        int cornerRadius = 6;
        using var path = CreateRoundedRectanglePath(0, 0, 31, 31, cornerRadius);

        // Draw Entur dark blue background
        using var bgBrush = new SolidBrush(ColorTranslator.FromHtml("#181c56"));
        graphics.FillPath(bgBrush, path);

        // Draw "EN" text in white, centered
        using var font = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        var text = "EN";
        var textSize = graphics.MeasureString(text, font);
        var textX = (32 - textSize.Width) / 2;
        var textY = (32 - textSize.Height) / 2 - 2; // Shift up slightly to make room for underline
        graphics.DrawString(text, font, textBrush, textX, textY);

        // Draw red underline beneath the text
        using var underlinePen = new Pen(ColorTranslator.FromHtml("#c7313a"), 2);
        var underlineY = textY + textSize.Height - 2;
        graphics.DrawLine(underlinePen, textX + 1, underlineY, textX + textSize.Width - 2, underlineY);

        var handle = bitmap.GetHicon();
        _iconHandles[iconId] = handle;
        return Icon.FromHandle(handle);
    }

    public static void CleanupIcon(string iconId)
    {
        if (_iconHandles.TryGetValue(iconId, out var handle) && handle != IntPtr.Zero)
        {
            DestroyIcon(handle);
            _iconHandles.Remove(iconId);
        }
    }

    public static void Cleanup()
    {
        foreach (var handle in _iconHandles.Values)
        {
            if (handle != IntPtr.Zero)
            {
                DestroyIcon(handle);
            }
        }
        _iconHandles.Clear();
    }
}
