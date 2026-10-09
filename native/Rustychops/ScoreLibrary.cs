using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using AlphaTab;
using AlphaTab.Importer;

namespace Rustychops;

public sealed record NewScoreOptions(string Title, string Artist, string TrackName, int Tempo, int Numerator, int Denominator, int Measures, string Tuning, int Instrument);

public sealed class ScoreLibrary
{
    private readonly PracticeStore _store;
    private readonly string _settingsPath;
    private readonly string _directory;
    public static readonly IReadOnlyDictionary<string, int[]> Tunings = new Dictionary<string, int[]>
    {
        ["Standard · E A D G B E"] = [64, 59, 55, 50, 45, 40],
        ["Drop D · D A D G B E"] = [64, 59, 55, 50, 45, 38],
        ["E♭ standard · E♭ A♭ D♭ G♭ B♭ E♭"] = [63, 58, 54, 49, 44, 39],
        ["D standard · D G C F A D"] = [62, 57, 53, 48, 43, 38],
    };

    public ScoreLibrary(PracticeStore store, string? settingsPath = null)
    {
        _store = store;
        _settingsPath = settingsPath ?? PracticeStore.DefaultPath;
        _directory = Path.Combine(Path.GetDirectoryName(_settingsPath)!, "Library");
    }

    public LibraryScore Add(string source)
    {
        var bytes = File.ReadAllBytes(source);
        var score = ScoreLoader.LoadScoreFromBytes(bytes, new Settings());
        return Store(bytes, Path.GetExtension(source), string.IsNullOrWhiteSpace(score.Title) ? Path.GetFileNameWithoutExtension(source) : score.Title.Replace('\u00a0', ' '));
    }

    public LibraryScore Create(NewScoreOptions options)
    {
        var bytes = BuildScore(options);
        return Store(bytes, ".gp", options.Title.Trim());
    }

    public AlphaTab.Model.Score Read(LibraryScore entry) => ScoreLoader.LoadScoreFromBytes(File.ReadAllBytes(entry.Path), new Settings());

    public static byte[] Encode(AlphaTab.Model.Score score)
    {
        var exported = new AlphaTab.Exporter.Gp7Exporter().Export(score, new Settings());
        return exported.Buffer.Raw.AsSpan((int)exported.ByteOffset, (int)exported.Length).ToArray();
    }

    private void RequireManaged(LibraryScore entry)
    {
        if (!_store.Library.Any(item => item.Path == entry.Path) ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(entry.Path), "N", out _) ||
            !string.Equals(Path.GetDirectoryName(Path.GetFullPath(entry.Path)), Path.GetFullPath(_directory), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This file is not a managed library score.");
    }

    public LibraryScore SaveScore(LibraryScore entry, AlphaTab.Model.Score score)
    {
        RequireManaged(entry);
        var bytes = Encode(score);
        var path = Path.ChangeExtension(entry.Path, ".gp");
        var previous = File.Exists(path) ? File.ReadAllBytes(path) : null;
        var current = _store.Library.Single(item => item.Path == entry.Path);
        var updated = current with { Path = path, Title = score.Title, ContentHash = Convert.ToHexString(SHA256.HashData(bytes)) };
        int index = _store.Library.IndexOf(current);
        var recent = _store.Recent.ToList(); var regions = _store.Regions.ToList();
        AtomicWrite(path, bytes);
        _store.Library[index] = updated;
        _store.Recent = _store.Recent.Select(item => item.Path == entry.Path ? item with { Path = path } : item).ToList();
        _store.Regions = _store.Regions.Select(item => item.Path == entry.Path ? item with { Path = path } : item).ToList();
        try { _store.Save(_settingsPath); }
        catch
        {
            _store.Library[index] = current; _store.Recent = recent; _store.Regions = regions;
            if (previous == null) File.Delete(path); else AtomicWrite(path, previous);
            throw;
        }
        // Keep imported originals after conversion; deleting a score removes all its managed copies.
        return updated;
    }

    public LibraryScore SaveNotes(LibraryScore entry, string notes)
    {
        RequireManaged(entry);
        var current = _store.Library.Single(item => item.Path == entry.Path);
        int index = _store.Library.IndexOf(current); var updated = current with { Notes = notes };
        _store.Library[index] = updated;
        try { _store.Save(_settingsPath); } catch { _store.Library[index] = current; throw; }
        return updated;
    }

    public void Delete(LibraryScore entry)
    {
        RequireManaged(entry);
        var library = _store.Library.ToList(); var recent = _store.Recent.ToList(); var regions = _store.Regions.ToList();
        _store.Library.RemoveAll(item => item.Path == entry.Path);
        _store.Recent.RemoveAll(item => item.Path == entry.Path);
        _store.Regions.RemoveAll(item => item.Path == entry.Path);
        // Move only files directly inside our own library, then commit metadata. Roll back on failure.
        var copies = Directory.Exists(_directory) ? Directory.GetFiles(_directory, Path.GetFileNameWithoutExtension(entry.Path) + ".*") : [];
        var moved = new List<(string Original, string Trash)>();
        try
        {
            foreach (var copy in copies) { var trash = copy + ".deleted"; File.Move(copy, trash); moved.Add((copy, trash)); }
            _store.Save(_settingsPath);
        }
        catch
        {
            _store.Library = library; _store.Recent = recent; _store.Regions = regions;
            foreach (var pair in moved) File.Move(pair.Trash, pair.Original);
            throw;
        }
        foreach (var pair in moved) { try { File.Delete(pair.Trash); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* Unlisted backup can be cleaned up later. */ } }
    }

    public void Export(AlphaTab.Model.Score score, string path)
    {
        path = Path.GetFullPath(path);
        if (string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(_directory), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose an export folder outside the managed Library. Use Save to update your library score.");
        AtomicWrite(path, Encode(score));
    }

    private static void AtomicWrite(string path, byte[] bytes)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private LibraryScore Store(byte[] bytes, string extension, string title)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var existing = _store.Library.FirstOrDefault(item => item.ContentHash == hash && File.Exists(item.Path));
        if (existing != null) return existing;
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + extension.ToLowerInvariant());
        File.WriteAllBytes(path, bytes);
        var entry = new LibraryScore(path, title, DateTime.Now, hash);
        _store.Library.Insert(0, entry);
        try { _store.Save(_settingsPath); }
        catch { _store.Library.Remove(entry); File.Delete(path); throw; }
        return entry;
    }

    public static byte[] BuildScore(NewScoreOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Title)) throw new ArgumentException("Enter a score title.");
        if (string.IsNullOrWhiteSpace(options.TrackName)) throw new ArgumentException("Enter a guitar track name.");
        if (options.Tempo is < 30 or > 320) throw new ArgumentException("Tempo must be between 30 and 320 BPM.");
        if (options.Measures is < 1 or > 128) throw new ArgumentException("Starting measures must be between 1 and 128.");
        if (options.Numerator is < 1 or > 12 || !new[] { 2, 4, 8, 16 }.Contains(options.Denominator)) throw new ArgumentException("Choose a valid time signature.");
        if (!Tunings.TryGetValue(options.Tuning, out var tuning)) throw new ArgumentException("Choose a guitar tuning.");
        if (options.Instrument is < 0 or > 127) throw new ArgumentException("Choose a valid guitar instrument.");
        var part = new XElement("part", new XAttribute("id", "P1"));
        for (int index = 0; index < options.Measures; index++)
        {
            var measure = new XElement("measure", new XAttribute("number", index + 1));
            if (index == 0)
            {
                var staff = new XElement("staff-details", new XElement("staff-lines", 6));
                for (int line = 1; line <= 6; line++)
                {
                    int note = tuning[6 - line];
                    string[] steps = ["C", "C", "D", "D", "E", "F", "F", "G", "G", "A", "A", "B"];
                    var pitch = new XElement("staff-tuning", new XAttribute("line", line), new XElement("tuning-step", steps[note % 12]));
                    if (new[] { 1, 3, 6, 8, 10 }.Contains(note % 12)) pitch.Add(new XElement("tuning-alter", 1));
                    pitch.Add(new XElement("tuning-octave", note / 12 - 1)); staff.Add(pitch);
                }
                measure.Add(new XElement("attributes", new XElement("divisions", 8), new XElement("key", new XElement("fifths", 0)), new XElement("time", new XElement("beats", options.Numerator), new XElement("beat-type", options.Denominator)), new XElement("clef", new XElement("sign", "G"), new XElement("line", 2)), staff));
                measure.Add(new XElement("direction", new XElement("direction-type", new XElement("metronome", new XElement("beat-unit", "quarter"), new XElement("per-minute", options.Tempo))), new XElement("sound", new XAttribute("tempo", options.Tempo))));
            }
            measure.Add(new XElement("note", new XElement("rest", new XAttribute("measure", "yes")), new XElement("duration", options.Numerator * 32 / options.Denominator), new XElement("voice", 1)));
            part.Add(measure);
        }
        var xml = new XDocument(new XElement("score-partwise", new XAttribute("version", "4.0"),
            new XElement("work", new XElement("work-title", options.Title.Trim())),
            new XElement("part-list", new XElement("score-part", new XAttribute("id", "P1"), new XElement("part-name", options.TrackName.Trim()), new XElement("score-instrument", new XAttribute("id", "I1"), new XElement("instrument-name", "Guitar")), new XElement("midi-instrument", new XAttribute("id", "I1"), new XElement("midi-channel", 1), new XElement("midi-program", options.Instrument + 1)))), part));
        var score = ScoreLoader.LoadScoreFromBytes(Encoding.UTF8.GetBytes(xml.ToString()), new Settings());
        score.Title = options.Title.Trim(); score.Artist = options.Artist.Trim(); score.Tracks[0].Name = options.TrackName.Trim();
        foreach (var guitar in score.Tracks[0].Staves) { guitar.ShowStandardNotation = false; guitar.ShowTablature = true; }
        return Encode(score);
    }
}
