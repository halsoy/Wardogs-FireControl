using System.Text.Json;

namespace WardogsFireControl;

public sealed class KnownTargetRepository
{
    private readonly Dictionary<string, List<KnownTarget>> _targets = new(StringComparer.OrdinalIgnoreCase);
    public static readonly IReadOnlyDictionary<string, string> Maps = new Dictionary<string, string>
    {
        ["bakurani"] = "Bakurani",
        ["ozeti"] = "Ozeti",
        ["zestafona"] = "Zestafona"
    };

    public KnownTargetRepository(string mapsDirectory)
    {
        foreach (var map in Maps)
        {
            var path = Path.Combine(mapsDirectory, map.Key + ".json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            _targets[map.Key] = document.RootElement.GetProperty("markers")
                .EnumerateArray()
                .Where(marker => marker.GetProperty("icon").GetString() == "tower")
                .Select(marker => new KnownTarget(
                    map.Key,
                    marker.GetProperty("label").GetString() ?? "Tower",
                    new MapCoordinate(
                        marker.GetProperty("x").GetDouble() / 100d,
                        marker.GetProperty("y").GetDouble() / 100d)))
                .OrderBy(target => target.Name)
                .ToList();
        }
    }

    public IReadOnlyList<KnownTarget> ForMap(string mapId) =>
        _targets.TryGetValue(mapId, out var targets) ? targets : [];
}
