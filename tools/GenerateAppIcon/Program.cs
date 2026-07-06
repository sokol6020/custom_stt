using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static Bitmap CreateBitmap(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);

    var bg = Color.FromArgb(20, 20, 28);
    var accent = Color.FromArgb(91, 141, 239);
    var accentDark = Color.FromArgb(58, 108, 210);
    var white = Color.FromArgb(245, 246, 250);

    var rect = new RectangleF(0, 0, size, size);
    var radius = size * 0.22f;
    using (var path = RoundedRect(rect, radius))
    using (var brush = new LinearGradientBrush(rect, accent, accentDark, 135f))
        g.FillPath(brush, path);

    using (var path = RoundedRect(new RectangleF(size * 0.06f, size * 0.06f, size * 0.88f, size * 0.88f), radius * 0.8f))
    using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), Math.Max(1f, size * 0.03f)))
        g.DrawPath(pen, path);

    var cx = size * 0.5f;
    var micTop = size * 0.24f;
    var micW = size * 0.18f;
    var micH = size * 0.34f;
    using var micBrush = new SolidBrush(white);
    FillRoundedRectangle(g, micBrush, cx - micW / 2f, micTop, micW, micH, micW * 0.45f);

    var arcW = size * 0.42f;
    var arcH = size * 0.22f;
    var arcY = size * 0.52f;
    using var arcPen = new Pen(white, Math.Max(1.5f, size * 0.07f))
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
    };
    g.DrawArc(arcPen, cx - arcW / 2f, arcY, arcW, arcH, 0, 180);

    var standW = size * 0.06f;
    var standH = size * 0.12f;
    g.FillRectangle(micBrush, cx - standW / 2f, arcY + arcH * 0.72f, standW, standH);
    g.FillEllipse(micBrush, cx - size * 0.11f, arcY + arcH * 0.72f + standH - size * 0.02f, size * 0.22f, size * 0.05f);

    if (size >= 32)
    {
        using var wavePen = new Pen(Color.FromArgb(210, 255, 255, 255), Math.Max(1f, size * 0.05f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        DrawWave(g, wavePen, size * 0.16f, size * 0.42f, size * 0.08f);
        DrawWave(g, wavePen, size * 0.78f, size * 0.38f, size * 0.08f);
    }

    return bmp;
}

static void DrawWave(Graphics g, Pen pen, float x, float y, float amp)
{
    var path = new GraphicsPath();
    path.AddBezier(x, y, x + amp * 0.4f, y - amp, x + amp * 0.8f, y + amp, x + amp * 1.2f, y);
    g.DrawPath(pen, path);
    path.Dispose();
}

static GraphicsPath RoundedRect(RectangleF bounds, float radius)
{
    var path = new GraphicsPath();
    var d = radius * 2;
    path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
    path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
    path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
    path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}

static void FillRoundedRectangle(Graphics g, Brush brush, float x, float y, float w, float h, float r)
{
    using var path = RoundedRect(new RectangleF(x, y, w, h), r);
    g.FillPath(brush, path);
}

static byte[] BitmapToPngBytes(Bitmap bitmap)
{
    using var ms = new MemoryStream();
    bitmap.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

static void WriteIcon(string path, int[] sizes)
{
    var images = sizes.Select(CreateBitmap).ToList();
    try
    {
        using var fs = File.Create(path);
        using var writer = new BinaryWriter(fs);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)images.Count);

        var offset = 6 + 16 * images.Count;
        var pngData = new List<byte[]>();

        for (var i = 0; i < images.Count; i++)
        {
            var size = sizes[i];
            var png = BitmapToPngBytes(images[i]);
            pngData.Add(png);

            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }

        foreach (var png in pngData)
            writer.Write(png);
    }
    finally
    {
        foreach (var image in images)
            image.Dispose();
    }
}

var output = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "customSTT", "Assets", "app.ico"));

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
WriteIcon(output, [16, 24, 32, 48, 64, 128, 256]);
Console.WriteLine($"Created {output}");
