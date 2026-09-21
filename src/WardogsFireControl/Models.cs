using System.Text.Json.Serialization;

namespace WardogsFireControl;

public sealed record MapCoordinate(double X, double Y)
{
    public override string ToString() => FormattableString.Invariant($"x{X:0.00}  y{Y:0.00}");
}

public sealed class SavedPosition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MapId { get; set; } = "bakurani";
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }

    [JsonIgnore]
    public MapCoordinate Coordinate => new(X, Y);

    public override string ToString() => FormattableString.Invariant($"{Name}  ·  x{X:0.00} y{Y:0.00}");
}

public sealed class HotkeySettings
{
    public Keys TargetKey { get; set; } = Keys.F1;
    public HotkeyModifiers TargetModifiers { get; set; }
    public Keys GunKey { get; set; } = Keys.F5;
    public HotkeyModifiers GunModifiers { get; set; }
}

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008
}

public readonly record struct HotkeyBinding(Keys Key, HotkeyModifiers Modifiers)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyName(Key));
        return string.Join(" + ", parts);
    }

    private static string KeyName(Keys key) => key switch
    {
        >= Keys.D0 and <= Keys.D9 => ((int)(key - Keys.D0)).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => $"Num {(int)(key - Keys.NumPad0)}",
        Keys.Oemcomma => ",",
        Keys.OemPeriod => ".",
        Keys.OemQuestion => "/",
        Keys.OemSemicolon => ";",
        Keys.OemQuotes => "'",
        Keys.OemOpenBrackets => "[",
        Keys.OemCloseBrackets => "]",
        Keys.OemPipe => "\\",
        Keys.OemMinus => "-",
        Keys.Oemplus => "=",
        Keys.Space => "Space",
        Keys.Return => "Enter",
        Keys.Escape => "Esc",
        Keys.Back => "Backspace",
        _ => key.ToString()
    };
}

public sealed class AppState
{
    public string SelectedMapId { get; set; } = "bakurani";
    public string SelectedWeaponId { get; set; } = "mortar";
    public List<SavedPosition> Guns { get; set; } = [];
    public List<SavedPosition> Targets { get; set; } = [];
    public HotkeySettings Hotkeys { get; set; } = new();
    public bool AlwaysOnTop { get; set; } = true;
}

public sealed record KnownTarget(string MapId, string Name, MapCoordinate Coordinate)
{
    public override string ToString() => $"{Name}  ·  {Coordinate}";
}

public sealed record MilSolution(string Arc, double Mil);

public sealed record FiringSolution(
    double DistanceMeters,
    double AzimuthDegrees,
    string RangeStatus,
    IReadOnlyList<MilSolution> Mils,
    bool IsWithinReportedEnvelope,
    bool IsWithinTable);

public sealed record WeaponSummary(
    string Id,
    string Name,
    double ReportedMinMeters,
    double ReportedMaxMeters,
    double TableMinMeters,
    double TableMaxMeters)
{
    public override string ToString() => Name;
}
