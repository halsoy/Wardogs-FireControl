using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Tesseract;

namespace WardogsFireControl;

public sealed partial class CoordinateOcrService : IDisposable
{
    private readonly TesseractEngine _engine;

    public CoordinateOcrService()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        _engine = new TesseractEngine(dataPath, "eng", EngineMode.LstmOnly);
        _engine.SetVariable("tessedit_char_whitelist", "xXyY0123456789.,");
        _engine.SetVariable("preserve_interword_spaces", "1");
    }

    public CoordinateRead CaptureAtMouse()
    {
        if (!GetCursorPos(out var cursor))
            return CoordinateRead.Failure("Windows could not read the mouse position.");

        var screen = Screen.FromPoint(new Point(cursor.X, cursor.Y));
        const int sourceWidth = 560;
        const int sourceHeight = 360;
        var left = Math.Clamp(cursor.X - sourceWidth / 2, screen.Bounds.Left,
            Math.Max(screen.Bounds.Left, screen.Bounds.Right - sourceWidth));
        var top = Math.Clamp(cursor.Y - sourceHeight / 2, screen.Bounds.Top,
            Math.Max(screen.Bounds.Top, screen.Bounds.Bottom - sourceHeight));
        var width = Math.Min(sourceWidth, screen.Bounds.Right - left);
        var height = Math.Min(sourceHeight, screen.Bounds.Bottom - top);

        using var capture = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(capture))
            graphics.CopyFromScreen(left, top, 0, 0, capture.Size, CopyPixelOperation.SourceCopy);

        return Recognize(capture, saveDiagnostic: true);
    }

    public CoordinateRead ReadImageAtPoint(string path, Point point)
    {
        using var image = new Bitmap(path);
        const int sourceWidth = 560;
        const int sourceHeight = 360;
        var left = Math.Clamp(point.X - sourceWidth / 2, 0, Math.Max(0, image.Width - sourceWidth));
        var top = Math.Clamp(point.Y - sourceHeight / 2, 0, Math.Max(0, image.Height - sourceHeight));
        var rectangle = new Rectangle(left, top, Math.Min(sourceWidth, image.Width - left), Math.Min(sourceHeight, image.Height - top));
        using var capture = image.Clone(rectangle, PixelFormat.Format24bppRgb);
        return Recognize(capture, saveDiagnostic: false);
    }

    private CoordinateRead Recognize(Bitmap capture, bool saveDiagnostic)
    {

        var attempts = new List<string>();
        foreach (var variant in CreateVariants(capture))
        {
            using (variant)
            {
                var text = ReadBitmap(variant);
                attempts.Add(text);
                if (TryParseCoordinates(text, out var coordinate))
                    return CoordinateRead.Success(coordinate!, string.Join(" | ", attempts));
            }
        }

        string? diagnosticPath = null;
        if (saveDiagnostic)
        {
            var diagnosticDirectory = Path.Combine(Path.GetTempPath(), "WardogsFireControl");
            Directory.CreateDirectory(diagnosticDirectory);
            diagnosticPath = Path.Combine(diagnosticDirectory, $"ocr-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            capture.Save(diagnosticPath, System.Drawing.Imaging.ImageFormat.Png);
        }
        return CoordinateRead.Failure(
            "Coordinates were not recognized. Keep the map open and place the pointer beside its x/y readout.",
            string.Join(" | ", attempts), diagnosticPath);
    }

    private string ReadBitmap(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        using var pix = Pix.LoadFromMemory(stream.ToArray());
        using var page = _engine.Process(pix, PageSegMode.SparseText);
        return page.GetText() ?? string.Empty;
    }

    private static IEnumerable<Bitmap> CreateVariants(Bitmap source)
    {
        yield return Scale(source, 3, threshold: null);
        yield return Scale(source, 3, threshold: 170);
        yield return Scale(source, 3, threshold: 205);
    }

    private static Bitmap Scale(Bitmap source, int factor, byte? threshold)
    {
        var output = new Bitmap(source.Width * factor, source.Height * factor, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(output);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, new Rectangle(0, 0, output.Width, output.Height));

        if (threshold is null) return output;

        var rectangle = new Rectangle(0, 0, output.Width, output.Height);
        var data = output.LockBits(rectangle, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        try
        {
            unsafe
            {
                for (var y = 0; y < data.Height; y++)
                {
                    var row = (byte*)data.Scan0 + y * data.Stride;
                    for (var x = 0; x < data.Width; x++)
                    {
                        var pixel = row + x * 3;
                        var luminance = (pixel[2] * 299 + pixel[1] * 587 + pixel[0] * 114) / 1000;
                        var value = luminance >= threshold ? (byte)255 : (byte)0;
                        pixel[0] = pixel[1] = pixel[2] = value;
                    }
                }
            }
        }
        finally
        {
            output.UnlockBits(data);
        }
        return output;
    }

    public static bool TryParseCoordinates(string text, out MapCoordinate? coordinate)
    {
        coordinate = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var normalized = text.Replace(',', '.');
        var x = XRegex().Match(normalized);
        var y = YRegex().Match(normalized);
        if (!x.Success || !y.Success ||
            !double.TryParse(x.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var xValue) ||
            !double.TryParse(y.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var yValue) ||
            xValue is < 0 or > 163.84 || yValue is < 0 or > 163.84)
            return false;

        coordinate = new MapCoordinate(xValue, yValue);
        return true;
    }

    public void Dispose() => _engine.Dispose();

    [GeneratedRegex(@"(?i)x\s*[:=]?\s*(\d{1,3}(?:\.\d{1,2})?)")]
    private static partial Regex XRegex();

    [GeneratedRegex(@"(?i)y\s*[:=]?\s*(\d{1,3}(?:\.\d{1,2})?)")]
    private static partial Regex YRegex();

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}

public sealed record CoordinateRead(bool IsSuccess, MapCoordinate? Coordinate, string Message, string RawText, string? DiagnosticPath)
{
    public static CoordinateRead Success(MapCoordinate coordinate, string rawText) =>
        new(true, coordinate, "Coordinates captured.", rawText, null);

    public static CoordinateRead Failure(string message, string rawText = "", string? diagnosticPath = null) =>
        new(false, null, message, rawText, diagnosticPath);
}
