using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using customSTT.Models;

namespace customSTT.Services;

public readonly record struct OverlayScreenInfo(int Index, string DisplayName, bool IsPrimary);

public static class OverlayLayoutHelper
{
    private const double Margin = 16;

    public static IReadOnlyList<OverlayScreenInfo> GetScreens()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
        {
            return new[]
            {
                new OverlayScreenInfo(0, "Дисплей 1", true)
            };
        }

        return screens
            .Select((screen, index) => new OverlayScreenInfo(
                index,
                FormatScreenName(screen, index),
                screen.Primary))
            .ToArray();
    }

    public static Rect GetWorkArea(int screenIndex)
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
            return SystemParameters.WorkArea;

        var index = Math.Clamp(screenIndex, 0, screens.Length - 1);
        var workingArea = screens[index].WorkingArea;
        var transform = GetTransformFromDevice();

        var topLeft = transform.Transform(new Point(workingArea.Left, workingArea.Top));
        var bottomRight = transform.Transform(new Point(workingArea.Right, workingArea.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    public static Point GetOverlayPosition(Rect workArea, double width, double height, OverlayCorner corner)
    {
        return corner switch
        {
            OverlayCorner.TopLeft => new Point(workArea.Left + Margin, workArea.Top + Margin),
            OverlayCorner.TopRight => new Point(workArea.Right - width - Margin, workArea.Top + Margin),
            OverlayCorner.BottomLeft => new Point(workArea.Left + Margin, workArea.Bottom - height - Margin),
            OverlayCorner.BottomRight => new Point(workArea.Right - width - Margin, workArea.Bottom - height - Margin),
            _ => new Point(workArea.Right - width - Margin, workArea.Top + Margin)
        };
    }

    private static string FormatScreenName(Screen screen, int index)
    {
        var primary = screen.Primary ? ", основной" : string.Empty;
        var bounds = screen.Bounds;
        return $"Дисплей {index + 1}{primary} ({bounds.Width}×{bounds.Height})";
    }

    private static Matrix GetTransformFromDevice()
    {
        var source = PresentationSource.FromVisual(System.Windows.Application.Current.MainWindow);
        if (source?.CompositionTarget != null)
            return source.CompositionTarget.TransformFromDevice;

        return Matrix.Identity;
    }
}
