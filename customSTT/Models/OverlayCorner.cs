namespace customSTT.Models;

public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public static class OverlayCornerExtensions
{
    public static readonly string[] DisplayNames =
    {
        "Верхний левый",
        "Верхний правый",
        "Нижний левый",
        "Нижний правый"
    };

    public static string ToStorageId(this OverlayCorner corner) => corner switch
    {
        OverlayCorner.TopLeft => "topLeft",
        OverlayCorner.TopRight => "topRight",
        OverlayCorner.BottomLeft => "bottomLeft",
        OverlayCorner.BottomRight => "bottomRight",
        _ => "topRight"
    };

    public static OverlayCorner FromStorageId(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "topleft" or "top-left" or "top_left" => OverlayCorner.TopLeft,
        "bottomleft" or "bottom-left" or "bottom_left" => OverlayCorner.BottomLeft,
        "bottomright" or "bottom-right" or "bottom_right" => OverlayCorner.BottomRight,
        _ => OverlayCorner.TopRight
    };

    public static int ToIndex(this OverlayCorner corner) => (int)corner;

    public static OverlayCorner FromIndex(int index) =>
        index is >= 0 and <= (int)OverlayCorner.BottomRight ? (OverlayCorner)index : OverlayCorner.TopRight;
}
