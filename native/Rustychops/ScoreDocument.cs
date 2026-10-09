using AlphaTab;
using AlphaTab.Importer;
using AlphaTab.Model;

namespace Rustychops;

/// <summary>Edits only the selected staff/voice, retaining other tracks and imported effects.</summary>
public sealed class ScoreDocument
{
    private readonly Stack<byte[]> _undo = new(), _redo = new();
    private byte[] _saved;
    public Score Score { get; private set; }
    public bool Dirty => !ScoreLibrary.Encode(Score).SequenceEqual(_saved);
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public ScoreDocument(Score score) { Score = score; _saved = ScoreLibrary.Encode(score); }
    public void MarkSaved() => _saved = ScoreLibrary.Encode(Score);
    private static Score Decode(byte[] bytes) => ScoreLoader.LoadScoreFromBytes(bytes, new Settings());
    private void Change(Action action)
    {
        var before = ScoreLibrary.Encode(Score);
        try
        {
            action();
            Reindex();
            // Re-import rebuilds links, indexes, timing and pitch caches after structural edits.
            var after = ScoreLibrary.Encode(Score);
            Score = Decode(after);
            if (!before.SequenceEqual(after)) { _undo.Push(before); _redo.Clear(); }
        }
        catch { Score = Decode(before); throw; }
    }
    private void Reindex()
    {
        for (int index = 0; index < Score.MasterBars.Count; index++)
        {
            var bar = Score.MasterBars[index]; bar.Index = index;
            bar.PreviousMasterBar = index > 0 ? Score.MasterBars[index - 1] : null!;
            bar.NextMasterBar = index + 1 < Score.MasterBars.Count ? Score.MasterBars[index + 1] : null!;
        }
        foreach (var track in Score.Tracks) foreach (var staff in track.Staves)
        {
            for (int index = 0; index < staff.Bars.Count; index++)
            {
                var bar = staff.Bars[index]; bar.Index = index;
                bar.PreviousBar = index > 0 ? staff.Bars[index - 1] : null!;
                bar.NextBar = index + 1 < staff.Bars.Count ? staff.Bars[index + 1] : null!;
                foreach (var voice in bar.Voices) for (int beatIndex = 0; beatIndex < voice.Beats.Count; beatIndex++) { voice.Beats[beatIndex].Index = beatIndex; voice.Beats[beatIndex].Voice = voice; }
            }
        }
    }
    public void Undo() { if (_undo.Count == 0) return; _redo.Push(ScoreLibrary.Encode(Score)); Score = Decode(_undo.Pop()); }
    public void Redo() { if (_redo.Count == 0) return; _undo.Push(ScoreLibrary.Encode(Score)); Score = Decode(_redo.Pop()); }
    public Voice Voice(int track, int staff, int bar, int voice) => Score.Tracks[track].Staves[staff].Bars[bar].Voices[voice];

    public void SetBeat(int track, int staff, int bar, int voice, int index, int duration, int dots, int[] frets, bool palmMute, string text)
    {
        if (!new[] { 1, 2, 4, 8, 16, 32, 64 }.Contains(duration) || dots is < 0 or > 2) throw new ArgumentException("Choose a valid note duration.");
        var guitar = Score.Tracks[track].Staves[staff];
        if (guitar.IsPercussion || guitar.Tuning.Count == 0) throw new ArgumentException("Fret entry requires a stringed track.");
        if (frets.Length != guitar.Tuning.Count || frets.Any(fret => fret is < -1 or > 36)) throw new ArgumentException("Use frets 0–36, or leave a string blank.");
        Change(() =>
        {
            var beat = Voice(track, staff, bar, voice).Beats[index];
            foreach (var note in beat.Notes.ToArray()) if (note.String > 0 && frets[frets.Length - (int)note.String] < 0) beat.RemoveNote(note);
            for (int stringIndex = 0; stringIndex < frets.Length; stringIndex++)
            {
                if (frets[stringIndex] < 0) continue;
                // alphaTab numbers from the lowest string; guitar UI numbers from the highest.
                int internalString = frets.Length - stringIndex;
                var note = beat.Notes.FirstOrDefault(note => note.String == internalString);
                if (note == null) beat.AddNote(new Note { String = internalString, Fret = frets[stringIndex] });
                else note.Fret = frets[stringIndex];
            }
            beat.IsEmpty = false; beat.Duration = (Duration)duration; beat.Dots = dots; beat.IsPalmMute = palmMute; beat.Text = text;
            foreach (var note in beat.Notes) note.IsPalmMute = palmMute;
        });
    }
    public void AddBeat(int track, int staff, int bar, int voice, int after)
    {
        Change(() =>
        {
            var selected = Voice(track, staff, bar, voice);
            var beat = new Beat { Duration = Duration.Quarter, IsEmpty = false };
            beat.Voice = selected; selected.Beats.Insert(Math.Clamp(after + 1, 0, selected.Beats.Count), beat);
        });
    }
    public void RemoveBeat(int track, int staff, int bar, int voice, int index)
    {
        Change(() =>
        {
            var selected = Voice(track, staff, bar, voice);
            selected.Beats.RemoveAt(index);
            if (selected.Beats.Count == 0) selected.AddBeat(new Beat { Duration = Duration.Whole, IsEmpty = false });
        });
    }
    public void SplitRest(int track, int staff, int bar, int voice)
    {
        var selected = Voice(track, staff, bar, voice);
        if (selected.Beats.Count != 1 || !selected.Beats[0].IsFullBarRest) throw new ArgumentException("Select a measure containing one full-measure rest.");
        Change(() =>
        {
            var master = Score.MasterBars[bar]; selected.Beats.Clear();
            for (int index = 0; index < master.TimeSignatureNumerator; index++) selected.AddBeat(new Beat { Duration = (Duration)(int)master.TimeSignatureDenominator, IsEmpty = false });
        });
    }
    public void AppendMeasure()
    {
        Change(() =>
        {
            var previous = Score.MasterBars.Last();
            var master = new MasterBar { TimeSignatureNumerator = previous.TimeSignatureNumerator, TimeSignatureDenominator = previous.TimeSignatureDenominator };
            Score.AddMasterBar(master);
            foreach (var track in Score.Tracks) foreach (var staff in track.Staves)
            {
                var last = staff.Bars.Last();
                var bar = new Bar { Clef = last.Clef, KeySignature = last.KeySignature, KeySignatureType = last.KeySignatureType };
                staff.AddBar(bar);
                foreach (var previousVoice in last.Voices) { var voice = new Voice(); bar.AddVoice(voice); voice.AddBeat(new Beat { Duration = Duration.Whole, IsEmpty = previousVoice.Index > 0 }); }
            }
        });
    }
    public void RemoveMeasure(int index)
    {
        if (Score.MasterBars.Count <= 1) throw new ArgumentException("A score must contain at least one measure.");
        Change(() => { Score.MasterBars.RemoveAt(index); foreach (var track in Score.Tracks) foreach (var staff in track.Staves) staff.Bars.RemoveAt(index); });
    }
    public void SetDetails(string title, string artist, int tempo)
    {
        if (string.IsNullOrWhiteSpace(title) || tempo is < 30 or > 320) throw new ArgumentException("Enter a title and tempo between 30 and 320 BPM.");
        Change(() =>
        {
            Score.Title = title.Trim(); Score.Artist = artist.Trim();
            var first = Score.MasterBars[0];
            first.TempoAutomations.Clear(); first.TempoAutomations.Add(new Automation { Type = AutomationType.Tempo, Value = tempo, RatioPosition = 0 });
        });
    }
    public void SetMeasure(int bar, int numerator, int denominator, int key, bool minor)
    {
        if (numerator is < 1 or > 12 || !new[] { 2, 4, 8, 16 }.Contains(denominator) || key is < -7 or > 7) throw new ArgumentException("Choose a valid time and key signature.");
        Change(() =>
        {
            Score.MasterBars[bar].TimeSignatureNumerator = numerator; Score.MasterBars[bar].TimeSignatureDenominator = denominator;
            foreach (var track in Score.Tracks) foreach (var staff in track.Staves) { staff.Bars[bar].KeySignature = (KeySignature)key; staff.Bars[bar].KeySignatureType = minor ? KeySignatureType.Minor : KeySignatureType.Major; }
        });
    }
    public string RhythmSummary(int track, int staff, int bar, int voice)
    {
        var selected = Voice(track, staff, bar, voice);
        double ticks = selected.CalculateDuration(), expected = Score.MasterBars[bar].CalculateDuration(false);
        return Math.Abs(ticks - expected) < 1 ? "Measure rhythm is complete" : $"Measure uses {ticks / 960:0.###} of {expected / 960:0.###} quarter-note beats";
    }
    public void ValidateRhythm()
    {
        foreach (var track in Score.Tracks) foreach (var staff in track.Staves) foreach (var bar in staff.Bars) foreach (var voice in bar.Voices)
        {
            if (voice.IsEmpty || voice.Beats.All(beat => beat.GraceType != GraceType.None)) continue;
            if (voice.CalculateDuration() > bar.MasterBar.CalculateDuration(false) + 1 && !bar.MasterBar.IsFreeTime)
                throw new ArgumentException($"Measure {bar.Index + 1}, {track.Name}, voice {voice.Index + 1} has too many beats. Shorten durations or remove a beat before saving.");
        }
    }
}
