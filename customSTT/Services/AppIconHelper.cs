using System;
using System.Drawing;
using System.IO;
using System.Windows;

namespace customSTT.Services;

public static class AppIconHelper
{
    private const string IconFileName = "app.ico";

    public static Icon GetTrayIcon(int size = 16)
    {
        var filePath = GetIconFilePath();
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Файл иконки приложения не найден.", filePath);

        return new Icon(filePath, size, size);
    }

    public static Icon GetTrayIconFromColor(Color color, int size = 16)
    {
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            var padding = Math.Max(2, size / 8);
            graphics.FillEllipse(brush, padding, padding, size - padding * 2, size - padding * 2);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public static string GetIconFilePath()
        => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", IconFileName);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
