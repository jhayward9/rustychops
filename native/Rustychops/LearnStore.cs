using System.IO;
using System.Text.Json;

namespace Rustychops;

public sealed record LessonPart(string Id, string Title, string Instruction, string? Tab = null);
public sealed record GuitarLesson(string Id, string Title, string Level, string Outcome, LessonPart[] Parts);
public sealed record LearningAchievement(string Id, string Title, string Detail, double Current, double Target);

public sealed class LearnStore
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Guitarist";
    public string? LastLesson { get; set; }
    public string? LastPart { get; set; }
    public HashSet<string> Completed { get; set; } = [];
    public Dictionary<string, double> PracticeSeconds { get; set; } = [];
    public Dictionary<string, DateTimeOffset> Unlocked { get; set; } = [];
    public double TotalSeconds => PracticeSeconds.Values.Sum();
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rustychops", "learn.json");
    public static string Key(string lesson, string part) => lesson + "/" + part;
    public bool IsComplete(GuitarLesson lesson) => lesson.Parts.All(part => Completed.Contains(Key(lesson.Id, part.Id)));
    public void Complete(string lesson, string part)
    {
        if (!LessonCatalog.All.Any(item => item.Id == lesson && item.Parts.Any(step => step.Id == part))) throw new ArgumentException("Unknown lesson part.");
        Completed.Add(Key(lesson, part)); Evaluate();
    }
    public void AddTime(string lesson, string part, double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentException("Invalid practice duration.");
        if (!LessonCatalog.All.Any(item => item.Id == lesson && item.Parts.Any(step => step.Id == part))) throw new ArgumentException("Unknown lesson part.");
        var key = Key(lesson, part); PracticeSeconds[key] = PracticeSeconds.GetValueOrDefault(key) + seconds; Evaluate();
    }
    public LearningAchievement[] Achievements =>
    [
        new("first-part", "First steps", "Complete your first lesson part", Completed.Count, 1),
        new("first-lesson", "A lesson learned", "Complete every part of one lesson", LessonCatalog.All.Count(IsComplete), 1),
        new("ten-minutes", "Finding your rhythm", "Practice for 10 minutes", TotalSeconds, 600),
        new("one-hour", "Putting in the work", "Practice for one hour", TotalSeconds, 3600),
        new("three-lessons", "Building foundations", "Complete three lessons", LessonCatalog.All.Count(IsComplete), 3),
        new("all-keys", "Around the neck", "Complete all twelve keys of the pentatonic ladder", LessonCatalog.Ladder.Parts.Count(part => Completed.Contains(Key(LessonCatalog.Ladder.Id, part.Id))), 12)
    ];
    private void Evaluate() { foreach (var item in Achievements.Where(item => item.Current >= item.Target)) Unlocked.TryAdd(item.Id, DateTimeOffset.UtcNow); }
    public static LearnStore Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return new();
        var value = JsonSerializer.Deserialize<LearnStore>(File.ReadAllText(path)) ?? throw new InvalidDataException("Learning profile is empty.");
        if (value.Version != 1 || value.Completed == null || value.PracticeSeconds == null || value.Unlocked == null || value.PracticeSeconds.Values.Any(seconds => !double.IsFinite(seconds) || seconds < 0)) throw new InvalidDataException("Learning profile is not supported or is damaged.");
        return value;
    }
    public void Save(string? path = null)
    {
        path ??= DefaultPath; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(temporary, path, true);
    }
}

public static class LessonCatalog
{
    public static string ScaleTab(int root)
    {
        if (root is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(root));
        var notes = new List<string>();
        foreach (int guitarString in new[] { 6, 5, 4, 3, 2, 1 })
        {
            notes.Add($"{root}.{guitarString}"); notes.Add($"{root + (guitarString is 6 or 2 or 1 ? 3 : 2)}.{guitarString}");
        }
        var run = notes.Concat(notes.AsEnumerable().Reverse().Skip(1)).ToList(); run.Add("r");
        return string.Join(" | ", run.Chunk(8).Select(group => ":8 " + string.Join(" ", group))) + " |";
    }
    public static readonly GuitarLesson Ladder = new("pentatonic-ladder", "Pentatonic: all twelve keys", "Developing lead", "Move minor pentatonic box 1 from root fret 1 to 12, ascending and descending.",
        Enumerable.Range(1, 12).Select(fret => new LessonPart("fret-" + fret, $"Fret {fret} · {new[] { "F", "F♯ / G♭", "G", "A♭", "A", "B♭", "B", "C", "D♭", "D", "E♭", "E" }[fret - 1]} minor", $"Put your first finger at fret {fret} on low E. Ascend across six strings, then reverse without repeating the highest note. Play evenly and mute unused strings. The final eighth-note rest leaves room to restart. Complete this key when comfortable, then move one fret. Standard tuning; this pattern reaches fret {fret + 3}.", ScaleTab(fret))).ToArray());
    public static readonly GuitarLesson[] All =
    [
        new("first-contact", "Meet your guitar", "First steps", "Find your strings, tune up and play your first clear note.",
        [new("setup", "Hold and set up", "Sit comfortably with the guitar supported. Use a relaxed pick grip. Strings are numbered 1 (thin high E) through 6 (thick low E). A tab number tells you which fret to press; 0 means an open string."),
         new("tune", "Tune your strings", "Open Configure Guitar & Audio Settings and use the tuner with a clean sound. Standard tuning, thick to thin, is E A D G B E. Pluck one string at a time and mute the others. Return here when ready."),
         new("note", "Play a clear note", "Press low E just behind fret 1, pick once and let the note ring. Relax, release, and repeat. Compare with the open string. Use only enough pressure to sound clearly. Stop if playing hurts.", ":4 0.6 1.6 0.6 1.6 |")]),
        new("first-riff", "Your first rock riff", "Beginner rock", "Read a short tab and stay with a steady pulse.",
        [new("pulse", "Find the pulse", "Count 1 2 3 4 evenly. Clap once on each count. Listen to the example, then tap along before picking up your guitar.", ":4 0.6 0.6 0.6 0.6 |"),
         new("riff", "Play four notes", "Play open low E twice, fret 3 once, then fret 2. Each note lasts one beat. Repeat slowly; aim for equal note lengths before increasing speed.", ":4 0.6 0.6 3.6 2.6 |"),
         new("apply", "Make it musical", "Repeat the riff four times with a count-in. Keep counting through mistakes. Try a quieter first repeat and a louder second repeat while keeping the pulse steady.", ":4 0.6 0.6 3.6 2.6 | 0.6 0.6 3.6 2.6 |")]),
        new("metal-rhythm", "Metal rhythm foundations", "Beginner metal", "Control silence, palm muting and even eighth notes.",
        [new("mute", "Control the strings", "Rest the edge of your picking hand lightly near the bridge on low E. Move it slightly until you hear a short, clear muted sound. Use your fretting hand to silence other strings. Listen first, then compare your muted and ringing notes.", ":4 0.6{pm} 0.6{pm} 0.6 0.6 |"),
         new("eighths", "Alternate eighth notes", "Count 1 and 2 and 3 and 4 and. Pick down on numbers and up on 'and'. Begin slowly and keep your hand relaxed. Use the speed control to find a comfortable tempo.", ":8 0.6 0.6 0.6 0.6 0.6 0.6 0.6 0.6 |"),
         new("rests", "Make the rests count", "Play two eighth notes and leave the next beat silent. Stop the string with your hands during each rest. Silence is part of the riff; keep counting while you are not picking.", ":8 0.6 0.6 :4 r :8 3.6 2.6 :4 r |")]),
        new("pentatonic-box", "Minor pentatonic box 1", "Lead foundations", "Learn A minor pentatonic and identify its roots.",
        [new("shape", "Learn the shape", "Start at fret 5 of low E. Fret pairs from low to high are 5–8, 5–7, 5–7, 5–7, 5–8, 5–8. Use index/pinky for three-fret gaps and index/ring for two-fret gaps. Play each string pair slowly.", ScaleTab(5)),
         new("roots", "Hear the root", "The notes are A C D E G. A is the root: low E fret 5, D fret 7, and high E fret 5. Play those roots and sing the sound. Changing the whole box by one fret changes its key.", ":4 5.6 7.4 5.1 r |"),
         new("phrase", "Create a phrase", "Use just three or four scale notes. Pause between phrases and finish on A. Try answering a short phrase with a second phrase. A scale is a set of sounds you can make music with.", ":8 5.3 7.3 5.2 8.2 :4 5.1 r |")]),
        Ladder
    ];
}
