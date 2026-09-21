using System.Text.Json;

namespace WardogsFireControl;

public sealed class StateStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public StateStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WardogsFireControl");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "state.json");
    }

    public AppState Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppState();
            return JsonSerializer.Deserialize<AppState>(File.ReadAllText(_path), JsonOptions) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    public void Save(AppState state)
    {
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporary, _path, true);
    }
}
