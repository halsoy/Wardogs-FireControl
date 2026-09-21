using WardogsFireControl;
using System.Windows.Forms;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Assert(CoordinateOcrService.TryParseCoordinates("y109.41   x98.53", out var coordinate), "Screenshot coordinate text should parse.");
Assert(coordinate is not null && Math.Abs(coordinate.X - 98.53) < 0.001 && Math.Abs(coordinate.Y - 109.41) < 0.001,
    "Parsed coordinates should retain precision.");
Assert(!CoordinateOcrService.TryParseCoordinates("x999.00 y20.00", out _), "Impossible coordinates should be rejected.");

var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
var ballistics = new BallisticsRepository(Path.Combine(dataDirectory, "weapons.json"));
var east = ballistics.Calculate("mortar", new MapCoordinate(10, 10), new MapCoordinate(14.5, 10));
Assert(Math.Abs(east.DistanceMeters - 450) < 0.001, "Coordinate distance should use 100 metres per unit.");
Assert(Math.Abs(east.AzimuthDegrees - 90) < 0.001, "East should be 90 degrees.");
Assert(east.Mils.Count == 1 && Math.Abs(east.Mils[0].Mil - 525) < 0.001, "450 m L81 interpolation should be 525 mil.");

var north = ballistics.Calculate("mortar", new MapCoordinate(10, 10), new MapCoordinate(10, 14));
Assert(Math.Abs(north.AzimuthDegrees) < 0.001, "North should be 0 degrees.");
var tooClose = ballistics.Calculate("spg", new MapCoordinate(10, 10), new MapCoordinate(15, 10));
Assert(!tooClose.IsWithinReportedEnvelope && tooClose.RangeStatus.StartsWith("TOO CLOSE"), "SPH-2 minimum range should be enforced.");

var plainHotkey = new HotkeyBinding(Keys.F1, HotkeyModifiers.None);
var modifiedHotkey = new HotkeyBinding(Keys.T, HotkeyModifiers.Control | HotkeyModifiers.Shift);
Assert(plainHotkey.ToString() == "F1", "Plain hotkeys should retain their familiar label.");
Assert(modifiedHotkey.ToString() == "Ctrl + Shift + T", "Modified hotkeys should have a clear display label.");
Assert(modifiedHotkey != new HotkeyBinding(Keys.T, HotkeyModifiers.Control), "Modifiers must be part of hotkey identity.");

var known = new KnownTargetRepository(Path.Combine(dataDirectory, "maps"));
Assert(known.ForMap("bakurani").Count == 5, "Bakurani should provide five towers.");
Assert(known.ForMap("ozeti").Count == 4, "Ozeti should provide four published towers.");

if (args.Length == 3)
{
    using var ocr = new CoordinateOcrService();
    var result = ocr.ReadImageAtPoint(args[0], new System.Drawing.Point(int.Parse(args[1]), int.Parse(args[2])));
    Console.WriteLine($"Screenshot OCR: success={result.IsSuccess}; coordinate={result.Coordinate}; raw={result.RawText.ReplaceLineEndings(" ")}");
    Assert(result.IsSuccess, "The supplied screenshot coordinate readout should be recognized.");
}

Console.WriteLine("All WardogsFireControl tests passed.");
