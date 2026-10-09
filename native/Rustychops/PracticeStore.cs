using System.IO;
using System.Text.Json;

namespace Rustychops;

public sealed record RecentScore(string Path, DateTime OpenedAt);
public sealed record LibraryScore(string Path, string Title, DateTime AddedAt, string ContentHash, string Notes = "");
public sealed record PracticeRegion(string Id, string Path, string Name, double StartTick, double EndTick, double Speed);
public sealed class PracticeStore
{
    public List<RecentScore> Recent { get; set; } = [];
    public List<LibraryScore> Library { get; set; } = [];
    public List<PracticeRegion> Regions { get; set; } = [];
    public string? InputDevice { get; set; }
    public string? OutputDevice { get; set; }
    public int InputChannel { get; set; }
    public double TunerReference { get; set; } = 440;
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rustychops", "settings.json");
    public static PracticeStore Load(string? path = null)
    {
        try { return JsonSerializer.Deserialize<PracticeStore>(File.ReadAllText(path ?? DefaultPath)) ?? new(); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
