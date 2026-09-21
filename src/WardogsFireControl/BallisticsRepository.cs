using System.Text.Json;

namespace WardogsFireControl;

public sealed class BallisticsRepository
{
    private readonly Dictionary<string, WeaponData> _weapons;

    public BallisticsRepository(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        _weapons = document.RootElement.GetProperty("weapons")
            .EnumerateArray()
            .Select(ParseWeapon)
            .ToDictionary(w => w.Summary.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<WeaponSummary> Weapons => _weapons.Values.Select(w => w.Summary).ToList();

    public FiringSolution Calculate(string weaponId, MapCoordinate gun, MapCoordinate target)
    {
        var weapon = _weapons[weaponId];
        var dx = target.X - gun.X;
        var dy = target.Y - gun.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy) * 100d;
        var azimuth = Math.Atan2(dx, dy) * 180d / Math.PI;
        if (azimuth < 0) azimuth += 360d;

        var insideReported = distance >= weapon.Summary.ReportedMinMeters - 0.001 &&
                             distance <= weapon.Summary.ReportedMaxMeters + 0.001;
        if (!insideReported)
        {
            var status = distance < weapon.Summary.ReportedMinMeters
                ? $"TOO CLOSE · minimum {weapon.Summary.ReportedMinMeters:0} m"
                : $"OUT OF RANGE · maximum {weapon.Summary.ReportedMaxMeters:0} m";
            return new FiringSolution(distance, azimuth, status, [], false, false);
        }

        var solutions = new List<MilSolution>();
        AddIfAvailable(solutions, "MORTAR", weapon.Single, distance);
        AddIfAvailable(solutions, "LOW", weapon.Low, distance);
        AddIfAvailable(solutions, "HIGH", weapon.High, distance);
        var insideTable = solutions.Count > 0;
        var rangeStatus = insideTable
            ? "IN RANGE · FLAT TABLE"
            : "REPORTED IN RANGE · MIL TABLE HAS NO VALUE HERE";

        return new FiringSolution(distance, azimuth, rangeStatus, solutions, true, insideTable);
    }

    private static void AddIfAvailable(List<MilSolution> results, string arc, IReadOnlyList<TablePoint> table, double distance)
    {
        var mil = Interpolate(table, distance);
        if (mil.HasValue) results.Add(new MilSolution(arc, mil.Value));
    }

    public static double? Interpolate(IReadOnlyList<TablePoint> source, double distance)
    {
        if (source.Count == 0) return null;
        var table = source.OrderBy(p => p.Distance).ToList();
        if (distance < table[0].Distance || distance > table[^1].Distance) return null;
        for (var index = 0; index < table.Count; index++)
        {
            if (Math.Abs(table[index].Distance - distance) < 0.000001) return table[index].Mil;
            if (index == table.Count - 1 || distance > table[index + 1].Distance) continue;
            var left = table[index];
            var right = table[index + 1];
            var factor = (distance - left.Distance) / (right.Distance - left.Distance);
            return left.Mil + factor * (right.Mil - left.Mil);
        }
        return null;
    }

    private static WeaponData ParseWeapon(JsonElement element)
    {
        var id = element.GetProperty("id").GetString()!;
        var name = element.GetProperty("names").GetProperty("en").GetString()!;
        var ballistics = element.GetProperty("ballistics");
        var single = ParseOptionalTable(ballistics, "single");
        var low = ParseOptionalTable(ballistics, "low");
        var high = ParseOptionalTable(ballistics, "high");
        var all = single.Concat(low).Concat(high).ToList();

        // Current reported level-ground envelopes. The bundled MIL tables are older
        // community measurements, so their supported domain is shown separately.
        var reportedMin = id == "mortar" ? 52d : 745d;
        var reportedMax = id == "mortar" ? 685d : 2660d;
        var summary = new WeaponSummary(id, name, reportedMin, reportedMax,
            all.Min(p => p.Distance), all.Max(p => p.Distance));
        return new WeaponData(summary, single, low, high);
    }

    private static List<TablePoint> ParseTable(JsonElement table) => table.ValueKind != JsonValueKind.Array
        ? []
        : table.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Array && row.GetArrayLength() >= 2)
            .Select(row => new TablePoint(row[0].GetDouble(), row[1].GetDouble()))
            .ToList();

    private static List<TablePoint> ParseOptionalTable(JsonElement ballistics, string name) =>
        ballistics.TryGetProperty(name, out var table) ? ParseTable(table) : [];

    private sealed record WeaponData(
        WeaponSummary Summary,
        IReadOnlyList<TablePoint> Single,
        IReadOnlyList<TablePoint> Low,
        IReadOnlyList<TablePoint> High);
}

public sealed record TablePoint(double Distance, double Mil);
