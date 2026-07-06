using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace customSTT.Services;

public sealed class WindowIconManager : IDisposable
{
    private const int WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;

    private Icon? _largeIcon;
    private Icon? _smallIcon;

    public void Apply(Window window)
    {
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
        if (!File.Exists(iconPath))
            return;

        using var stream = File.OpenRead(iconPath);
        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frames = decoder.Frames.ToList();
        if (frames.Count > 0)
            window.Icon = frames.OrderByDescending(f => f.PixelWidth).First();

        _largeIcon?.Dispose();
        _smallIcon?.Dispose();
        _largeIcon = new Icon(iconPath);
        _smallIcon = new Icon(iconPath, 16, 16);

        void ApplyNativeIcons()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero || _largeIcon == null || _smallIcon == null)
                return;

            SendMessage(hwnd, WmSetIcon, (IntPtr)IconBig, _largeIcon.Handle);
            SendMessage(hwnd, WmSetIcon, (IntPtr)IconSmall, _smallIcon.Handle);
        }

        if (window.IsLoaded)
            ApplyNativeIcons();
        else
            window.SourceInitialized += (_, _) => ApplyNativeIcons();
    }

    public void Dispose()
    {
        _largeIcon?.Dispose();
        _smallIcon?.Dispose();
        _largeIcon = null;
        _smallIcon = null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
