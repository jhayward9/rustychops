using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AlphaTab.Model;
using Microsoft.Win32;
using System.Windows.Threading;
using AlphaTab;
using AlphaTab.Synth;
using ScoreView = AlphaTab.Wpf.AlphaTab;

namespace Rustychops;

public sealed class ScoreEditorPage : Grid
{
    private readonly ScoreLibrary _library;
    private LibraryScore? _entry;
    public ScoreDocument? Document { get; private set; }
    private readonly ComboBox _track = Choice(), _staff = Choice(), _bar = Choice(), _voice = Choice(), _beat = Choice();
    private readonly ComboBox _duration = Choice(), _dots = Choice(), _numerator = Choice(), _denominator = Choice(), _key = Choice(), _mode = Choice();
    private readonly TextBox _title = Input(230), _artist = Input(190), _tempo = Input(70), _beatText = Input(260);
    private readonly CheckBox _palmMute = new() { Content = "Palm mute", Foreground = Brushes.White, Margin = new Thickness(10) };
    private readonly TextBlock _status = Label("", 13), _rhythm = Label("", 13), _heading = Label("Score editor", 28);
    private readonly ScoreView _notation = new();
    private readonly StackPanel _timeline = new();
    private readonly Border _inspector = new();
    private readonly TextBlock _position = Label("", 12);
    private readonly Button _play;
    private Score? _renderedScore;
    private int _renderedTrack = -1;
    private bool _notationInitialized;
    private bool _notationRendered;
    public bool NotationReady => _notationRendered && _notation.Api?.Score != null;
    public AlphaTabApiBase<ScoreView>? NotationApi => _notation.Api;
    private readonly WrapPanel _fretFields = new();
    private readonly List<TextBox> _frets = [];
    private readonly Button _undo, _redo;
    private int _trackIndex, _staffIndex, _barIndex, _voiceIndex, _beatIndex;
    private bool _loading, _beatPending, _detailsPending, _measurePending;
    public event Action<LibraryScore>? Saved;
    public event Action<LibraryScore>? ViewRequested;
    public event Action? LibraryRequested;
    public event Action<Score>? PlayRequested;
    public event Action? StopRequested;
    public LibraryScore? Entry => _entry;
    public void SetStatus(string message) => _status.Text = message;
    public void SetPlaying(bool playing) => _play.Content = playing ? "▶ Restart" : "▶ Play";

    public ScoreEditorPage(ScoreLibrary library)
    {
        _library = library;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _heading.FontSize = 20;
        var header = new DockPanel(); var heading = new StackPanel(); heading.Children.Add(Label("SCORE WORKSPACE", 10)); heading.Children.Add(_heading);
        var commands = new WrapPanel();
        commands.Children.Add(Action("Save", () => Save()));
        commands.Children.Add(Action("Save & view", () => { if (Save()) ViewRequested?.Invoke(_entry!); }));
        commands.Children.Add(Action("Export…", Export));
        _undo = Action("Undo", () => History(false)); _redo = Action("Redo", () => History(true)); commands.Children.Add(_undo); commands.Children.Add(_redo);
        commands.Children.Add(Action("Library", () => LibraryRequested?.Invoke())); DockPanel.SetDock(commands, Dock.Bottom); header.Children.Add(commands); header.Children.Add(heading);
        Children.Add(header);
        var selectors = new WrapPanel();
        _play = Action("▶ Play", () => { if (ApplyPending()) { Refresh(); PlayRequested?.Invoke(Document!.Score); } }); selectors.Children.Add(_play);
        selectors.Children.Add(Action("■ Stop", () => StopRequested?.Invoke())); selectors.Children.Add(_position);
        selectors.Children.Add(Action("Previous beat", () => SelectBeat(_barIndex, _beatIndex - 1)));
        selectors.Children.Add(Action("Next beat", () => SelectBeat(_barIndex, _beatIndex + 1)));
        selectors.Children.Add(Action("Inspector", () => _inspector.Visibility = _inspector.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible));
        Grid.SetRow(selectors, 1); Children.Add(selectors);
        var workspace = new DockPanel(); Grid.SetRow(workspace, 2); Children.Add(workspace);
        var overview = new Border { Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(8), Margin = new Thickness(0, 8, 0, 0), Child = new ScrollViewer { Content = _timeline, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 110 } };
        DockPanel.SetDock(overview, Dock.Bottom); workspace.Children.Add(overview);
        var inspectorTabs = new TabControl { Background = Panel, BorderThickness = new Thickness(0), Foreground = Brushes.White };
        _inspector.Width = 245; _inspector.Background = Panel; _inspector.BorderBrush = Line; _inspector.BorderThickness = new Thickness(1); _inspector.Padding = new Thickness(8); _inspector.Margin = new Thickness(10, 0, 0, 0); _inspector.Child = inspectorTabs;
        DockPanel.SetDock(_inspector, Dock.Right); workspace.Children.Add(_inspector);
        var scoreSettings = new Settings(); scoreSettings.Core.Engine = "skia"; scoreSettings.Core.UseWorkers = false;
        scoreSettings.Display.Scale = .85;
        scoreSettings.Player.PlayerMode = PlayerMode.Disabled; scoreSettings.Player.EnableCursor = false; scoreSettings.Player.EnableUserInteraction = false;
#pragma warning disable CS0618
        scoreSettings.Display.StaveProfile = StaveProfile.ScoreTab;
#pragma warning restore CS0618
        _notation.Settings = scoreSettings; _notation.Background = Brushes.White; _notation.Foreground = Brushes.Black;
        _notation.SelectionFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(65, 150, 200, 30));
        _notation.Loaded += (_, _) => Dispatcher.BeginInvoke(InitializeNotation, DispatcherPriority.ContextIdle);
        _notation.LayoutUpdated += (_, _) =>
        {
            if (_notation.IsVisible && _notation.Api != null && !_notationInitialized) Dispatcher.BeginInvoke(InitializeNotation, DispatcherPriority.ContextIdle);
        };
        _notation.IsVisibleChanged += (_, _) => { if (_notation.IsVisible) Dispatcher.BeginInvoke(() => { InitializeNotation(); RenderNotation(); }, DispatcherPriority.ContextIdle); };
        workspace.Children.Add(new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), Child = _notation });
        var structural = new WrapPanel();
        structural.Children.Add(Action("Split full-measure rest", () => Edit(() => Document!.SplitRest(_trackIndex, _staffIndex, _barIndex, _voiceIndex))));
        structural.Children.Add(Action("Add beat after selected", () => Edit(() => { Document!.AddBeat(_trackIndex, _staffIndex, _barIndex, _voiceIndex, _beatIndex); _beatIndex++; })));
        structural.Children.Add(Action("Delete beat", () => Edit(() => Document!.RemoveBeat(_trackIndex, _staffIndex, _barIndex, _voiceIndex, _beatIndex))));
        structural.Children.Add(Action("Add measure", () => Edit(() => { Document!.AppendMeasure(); _barIndex = Document.Score.MasterBars.Count - 1; _beatIndex = 0; })));
        structural.Children.Add(Action("Delete measure", () =>
        {
            if (MessageBox.Show(Window.GetWindow(this), "Delete this measure from every track? You can undo this change.", "Delete measure", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                Edit(() => Document!.RemoveMeasure(_barIndex));
        }));
        var beatEditor = new StackPanel(); beatEditor.Children.Add(Label("Fret entry", 16)); beatEditor.Children.Add(Label("High string → low string. Blank = silent; 0 = open.", 11)); beatEditor.Children.Add(_fretFields);
        _duration.ItemsSource = new[] { "Whole (1)", "Half (1/2)", "Quarter (1/4)", "Eighth (1/8)", "Sixteenth (1/16)", "Thirty-second (1/32)", "Sixty-fourth (1/64)" };
        _dots.ItemsSource = new[] { "No dots", "One dot", "Two dots" };
        _beatText.Width = 205;
        beatEditor.Children.Add(Field("Duration", _duration)); beatEditor.Children.Add(Field("Dots", _dots)); beatEditor.Children.Add(_palmMute); beatEditor.Children.Add(Field("Beat annotation", _beatText));
        beatEditor.Children.Add(_rhythm);
        var measureEditor = new StackPanel(); measureEditor.Children.Add(Label("Measure settings", 16)); measureEditor.Children.Add(Label("Applies to every track at this measure.", 11));
        _numerator.ItemsSource = Enumerable.Range(1, 12).ToArray(); _denominator.ItemsSource = new[] { 2, 4, 8, 16 };
        _key.ItemsSource = Enumerable.Range(-7, 15).Select(key => $"{ScoreInfo.MajorKeys[key + 7]} major / {ScoreInfo.MinorKeys[key + 7]} minor").ToArray(); _mode.ItemsSource = new[] { "Major", "Minor" };
        measureEditor.Children.Add(Field("Time numerator", _numerator)); measureEditor.Children.Add(Field("Time denominator", _denominator)); measureEditor.Children.Add(Field("Key signature", _key)); measureEditor.Children.Add(Field("Key mode", _mode));
        measureEditor.Children.Add(Action("Apply measure settings", () => { if (ApplyPending()) Refresh(); }));
        structural.Orientation = Orientation.Vertical; measureEditor.Children.Add(structural);
        var songEditor = new StackPanel(); _title.Width = _artist.Width = 205;
        songEditor.Children.Add(Field("Title", _title)); songEditor.Children.Add(Field("Artist / composer", _artist)); songEditor.Children.Add(Field("Tempo (BPM)", _tempo));
        songEditor.Children.Add(Field("Staff", _staff)); songEditor.Children.Add(Field("Voice", _voice)); songEditor.Children.Add(Field("Measure", _bar)); songEditor.Children.Add(Field("Beat", _beat));
        songEditor.Children.Add(Action("Apply song details", () => { if (ApplyPending()) Refresh(); }));
        foreach (var (name, panel) in new[] { ("Note", beatEditor), ("Measure", measureEditor), ("Song", songEditor) })
        {
            var content = new DockPanel();
            if (name == "Note") { var apply = Action("Apply beat", () => { if (ApplyPending()) Refresh(); }); DockPanel.SetDock(apply, Dock.Bottom); content.Children.Add(apply); }
            content.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            inspectorTabs.Items.Add(new TabItem { Header = name, Foreground = Brushes.Black, Content = content });
        }
        Grid.SetRow(_status, 3); Children.Add(_status);
        foreach (var box in new[] { _title, _artist, _tempo }) box.TextChanged += (_, _) => { if (!_loading) _detailsPending = true; };
        _beatText.TextChanged += (_, _) => { if (!_loading) _beatPending = true; };
        foreach (var box in new[] { _duration, _dots }) box.SelectionChanged += (_, _) => { if (!_loading) _beatPending = true; };
        _palmMute.Click += (_, _) => _beatPending = true;
        foreach (var box in new[] { _numerator, _denominator, _key, _mode }) box.SelectionChanged += (_, _) => { if (!_loading) _measurePending = true; };
        _track.SelectionChanged += (_, _) => Select(() => { _trackIndex = _track.SelectedIndex; _staffIndex = _voiceIndex = _beatIndex = 0; });
        _staff.SelectionChanged += (_, _) => Select(() => { _staffIndex = _staff.SelectedIndex; _voiceIndex = _beatIndex = 0; });
        _bar.SelectionChanged += (_, _) => Select(() => { _barIndex = _bar.SelectedIndex; _beatIndex = 0; });
        _voice.SelectionChanged += (_, _) => Select(() => { _voiceIndex = _voice.SelectedIndex; _beatIndex = 0; });
        _beat.SelectionChanged += (_, _) => Select(() => _beatIndex = _beat.SelectedIndex);
        PreviewKeyDown += (_, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                if (e.OriginalSource is TextBox || e.OriginalSource is ComboBox) return;
                if (e.Key == Key.Left) { SelectBeat(_barIndex, _beatIndex - 1); e.Handled = true; }
                else if (e.Key == Key.Right) { SelectBeat(_barIndex, _beatIndex + 1); e.Handled = true; }
                return;
            }
            if (e.Key == Key.S) { Save(); e.Handled = true; }
            else if (e.Key == Key.Z) { History(false); e.Handled = true; }
            else if (e.Key == Key.Y) { History(true); e.Handled = true; }
        };
    }

    public void Load(LibraryScore entry)
    {
        var document = new ScoreDocument(_library.Read(entry));
        _entry = entry; Document = document; _trackIndex = _staffIndex = _barIndex = _voiceIndex = _beatIndex = 0;
        _beatPending = _detailsPending = _measurePending = false; _status.Text = "Changes stay in the editor until you save. Export creates a separate .gp file for Guitar Pro or TuxGuitar."; Refresh();
    }
    private Voice SelectedVoice => Document!.Voice(_trackIndex, _staffIndex, _barIndex, _voiceIndex);
    private static readonly Brush Panel = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 24, 31)), Line = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 59, 71));
    private void InitializeNotation()
    {
        if (_notationInitialized || _notation.Api == null) return;
        _notationInitialized = true;
        _notation.Api.Settings.Player.EnableCursor = true; _notation.Api.Settings.Player.EnableUserInteraction = true; _notation.Api.UpdateSettings();
        _notation.Api.BeatMouseDown.On(beat => Dispatcher.BeginInvoke(() =>
        {
            if (!ApplyPending()) return;
            _trackIndex = (int)beat.Voice.Bar.Staff.Track.Index; _staffIndex = (int)beat.Voice.Bar.Staff.Index;
            _barIndex = (int)beat.Voice.Bar.Index; _voiceIndex = (int)beat.Voice.Index; _beatIndex = (int)beat.Index; Refresh();
        }));
        _notation.Api.RenderFinished.On(_ => Dispatcher.BeginInvoke(() => { _notationRendered = true; HighlightSelection(); }));
        RenderNotation();
    }
    private void RenderNotation()
    {
        if (!_notationInitialized || Document == null || !_notation.IsVisible || _notation.ActualWidth < 1) return;
        if (ReferenceEquals(_renderedScore, Document.Score) && _renderedTrack == _trackIndex) { HighlightSelection(); return; }
        var preview = AlphaTab.Importer.ScoreLoader.LoadScoreFromBytes(ScoreLibrary.Encode(Document.Score), new Settings());
        foreach (var track in preview.Tracks) foreach (var staff in track.Staves) { staff.ShowStandardNotation = true; staff.ShowTablature = true; }
        _renderedScore = Document.Score; _renderedTrack = _trackIndex;
        _notationRendered = false;
        _notation.Api!.RenderScore(preview, [(double)_trackIndex], null!);
    }
    private void HighlightSelection()
    {
        if (_notation.Api?.Score == null) return;
        var voice = _notation.Api.Score.Tracks[_trackIndex].Staves[_staffIndex].Bars[_barIndex].Voices[_voiceIndex];
        var beat = voice.Beats[Math.Min(_beatIndex, voice.Beats.Count - 1)];
        _notation.Api.HighlightPlaybackRange(beat, beat);
    }
    private void RefreshTimeline()
    {
        var score = Document!.Score; _timeline.Children.Clear();
        for (int trackIndex = 0; trackIndex < score.Tracks.Count; trackIndex++)
        {
            int selectedTrack = trackIndex; var track = score.Tracks[trackIndex]; var row = new StackPanel { Orientation = Orientation.Horizontal };
            var name = Action(track.Name, () => { if (!ApplyPending()) return; _trackIndex = selectedTrack; _staffIndex = _voiceIndex = _beatIndex = 0; Refresh(); }); name.Width = 135; row.Children.Add(name);
            for (int index = 0; index < score.MasterBars.Count; index++)
            {
                int measure = index; var button = Action((index + 1).ToString(), () => { if (!ApplyPending()) return; _trackIndex = selectedTrack; _staffIndex = _voiceIndex = _beatIndex = 0; _barIndex = measure; Refresh(); });
                button.Width = 36; button.Padding = new Thickness(4); button.Margin = new Thickness(1, 4, 1, 4);
                bool hasNotes = track.Staves.Any(staff => staff.Bars[index].Voices.Any(voice => voice.Beats.Any(beat => beat.Notes.Count > 0)));
                button.Background = trackIndex == _trackIndex && index == _barIndex ? Brushes.OliveDrab : hasNotes ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(49, 75, 88)) : Panel;
                button.ToolTip = $"{track.Name} · measure {index + 1} · {score.MasterBars[index].TimeSignatureNumerator}/{score.MasterBars[index].TimeSignatureDenominator}";
                row.Children.Add(button);
            }
            _timeline.Children.Add(row);
        }
    }
    public void SelectBeat(int measure, int beat)
    {
        if (!ApplyPending()) return;
        if (beat < 0 && measure > 0) { measure--; beat = Document!.Voice(_trackIndex, _staffIndex, measure, _voiceIndex).Beats.Count - 1; }
        else if (beat >= SelectedVoice.Beats.Count && measure + 1 < Document!.Score.MasterBars.Count) { measure++; beat = 0; }
        _barIndex = Math.Clamp(measure, 0, Document!.Score.MasterBars.Count - 1);
        _beatIndex = Math.Clamp(beat, 0, Document.Voice(_trackIndex, _staffIndex, _barIndex, _voiceIndex).Beats.Count - 1); Refresh();
    }
    private void Refresh()
    {
        if (Document == null) return;
        _loading = true;
        try
        {
            var score = Document.Score;
            _trackIndex = Math.Clamp(_trackIndex, 0, score.Tracks.Count - 1);
            var track = score.Tracks[_trackIndex]; _staffIndex = Math.Clamp(_staffIndex, 0, track.Staves.Count - 1);
            var staff = track.Staves[_staffIndex]; _barIndex = Math.Clamp(_barIndex, 0, staff.Bars.Count - 1);
            var bar = staff.Bars[_barIndex]; _voiceIndex = Math.Clamp(_voiceIndex, 0, bar.Voices.Count - 1);
            _beatIndex = Math.Clamp(_beatIndex, 0, SelectedVoice.Beats.Count - 1);
            _heading.Text = score.Title + (Document.Dirty ? " *" : "");
            _position.Text = $"{score.Tempo:0} BPM · M{_barIndex + 1} / {score.MasterBars.Count} · Beat {_beatIndex + 1}";
            _title.Text = score.Title; _artist.Text = score.Artist; _tempo.Text = score.Tempo.ToString("0");
            SetChoice(_track, score.Tracks.Select(item => item.Name), _trackIndex); SetChoice(_staff, track.Staves.Select((_, i) => $"Staff {i + 1}"), _staffIndex);
            SetChoice(_bar, score.MasterBars.Select((_, i) => $"Measure {i + 1}"), _barIndex); SetChoice(_voice, bar.Voices.Select((_, i) => $"Voice {i + 1}"), _voiceIndex);
            SetChoice(_beat, SelectedVoice.Beats.Select((_, i) => $"Beat {i + 1}"), _beatIndex);
            var beat = SelectedVoice.Beats[_beatIndex];
            _duration.SelectedIndex = Array.IndexOf(new[] { 1, 2, 4, 8, 16, 32, 64 }, (int)beat.Duration); _dots.SelectedIndex = (int)beat.Dots;
            _palmMute.IsChecked = beat.IsPalmMute; _beatText.Text = beat.Text ?? "";
            var master = score.MasterBars[_barIndex]; _numerator.SelectedItem = (int)master.TimeSignatureNumerator; _denominator.SelectedItem = (int)master.TimeSignatureDenominator;
            _key.SelectedIndex = (int)bar.KeySignature + 7; _mode.SelectedIndex = bar.KeySignatureType == KeySignatureType.Minor ? 1 : 0;
            _fretFields.Children.Clear(); _frets.Clear();
            for (int index = 0; index < staff.Tuning.Count; index++)
            {
                int stringNumber = index + 1; int internalString = staff.Tuning.Count - index; var note = beat.Notes.FirstOrDefault(note => note.String == internalString);
                var box = Input(45); box.Text = note?.Fret.ToString("0") ?? ""; box.ToolTip = $"String {stringNumber}: fret 0–36; blank removes note";
                box.TextChanged += (_, _) => { if (!_loading) _beatPending = true; }; _frets.Add(box);
                _fretFields.Children.Add(Field($"{stringNumber} · {PitchDetector.Describe(440 * Math.Pow(2, (staff.Tuning[index] - 69) / 12)).Name}", box));
            }
            RefreshTimeline(); RenderNotation();
            _rhythm.Text = Document.RhythmSummary(_trackIndex, _staffIndex, _barIndex, _voiceIndex);
            _undo.IsEnabled = Document.CanUndo; _redo.IsEnabled = Document.CanRedo;
        }
        finally { _loading = false; }
    }

    private void Select(Action select)
    {
        if (_loading || Document == null) return;
        if (ApplyPending()) select(); Refresh();
    }
    public bool ApplyPending()
    {
        if (Document == null) return false;
        try
        {
            if (_detailsPending)
            {
                if (!int.TryParse(_tempo.Text, out var tempo)) throw new ArgumentException("Tempo must be a whole number.");
                Document.SetDetails(_title.Text, _artist.Text, tempo); _detailsPending = false;
            }
            if (_beatPending)
            {
                var frets = _frets.Select(box => string.IsNullOrWhiteSpace(box.Text) ? -1 : int.TryParse(box.Text, out var fret) ? fret : throw new ArgumentException("Enter whole-number frets 0–36, or leave strings blank.")).ToArray();
                if (_duration.SelectedIndex < 0 || _dots.SelectedIndex < 0) throw new ArgumentException("Choose a supported duration and number of dots.");
                Document.SetBeat(_trackIndex, _staffIndex, _barIndex, _voiceIndex, _beatIndex, new[] { 1, 2, 4, 8, 16, 32, 64 }[_duration.SelectedIndex], _dots.SelectedIndex, frets, _palmMute.IsChecked == true, _beatText.Text);
                _beatPending = false;
            }
            if (_measurePending)
            {
                if (_numerator.SelectedItem is not int numerator || _denominator.SelectedItem is not int denominator || _key.SelectedIndex < 0) throw new ArgumentException("Choose valid measure settings.");
                Document.SetMeasure(_barIndex, numerator, denominator, _key.SelectedIndex - 7, _mode.SelectedIndex == 1); _measurePending = false;
            }
            return true;
        }
        catch (Exception error) { _status.Text = error.Message; return false; }
    }
    private void Edit(Action action)
    {
        if (!ApplyPending()) return;
        try { action(); _status.Text = "Unsaved changes · Save to keep this version locally."; Refresh(); }
        catch (Exception error) { _status.Text = error.Message; }
    }
    private void History(bool redo)
    {
        if (!ApplyPending()) return;
        if (redo) Document!.Redo(); else Document!.Undo(); Refresh(); _status.Text = redo ? "Redone" : "Undone";
    }
    public bool Save()
    {
        if (!ApplyPending() || _entry == null) return false;
        try { Document!.ValidateRhythm(); _entry = _library.SaveScore(_entry, Document.Score); Document.MarkSaved(); Refresh(); Saved?.Invoke(_entry); _status.Text = "Saved locally · " + _entry.Path; return true; }
        catch (Exception error) { _status.Text = "Could not save: " + error.Message; return false; }
    }
    public bool TryLeave()
    {
        if (Document == null || (!Document.Dirty && !_beatPending && !_detailsPending && !_measurePending)) return true;
        var answer = MessageBox.Show(Window.GetWindow(this), "Save your score changes before leaving the editor?", "Unsaved score", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) return false;
        if (answer == MessageBoxResult.Yes) return Save();
        if (_entry != null) Load(_entry); return true;
    }
    private void Export()
    {
        if (!ApplyPending()) return;
        try
        {
            Document!.ValidateRhythm();
            var name = string.Concat(Document.Score.Title.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            var picker = new SaveFileDialog { Title = "Export score", FileName = name + ".gp", DefaultExt = ".gp", Filter = "Guitar Pro 7 score (*.gp)|*.gp|TuxGuitar-compatible Guitar Pro score (*.gp)|*.gp", AddExtension = true };
            if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
            if (string.Equals(Path.GetFullPath(picker.FileName), Path.GetFullPath(_entry!.Path), StringComparison.OrdinalIgnoreCase)) { Save(); return; }
            _library.Export(Document.Score, picker.FileName); Refresh(); _status.Text = "Exported for Guitar Pro / TuxGuitar · " + picker.FileName;
        }
        catch (Exception error) { _status.Text = "Could not export: " + error.Message; }
    }
    private static ComboBox Choice() => new() { MinWidth = 85, Margin = new Thickness(4, 3, 4, 6), Padding = new Thickness(6) };
    private static TextBox Input(double width) => new() { Width = width, Padding = new Thickness(6), Margin = new Thickness(4, 3, 4, 6) };
    private static TextBlock Label(string text, double size) => new() { Text = text, FontSize = size, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = size <= 13 ? new Thickness(4, 3, 4, 4) : new Thickness(4, 6, 4, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
    private static StackPanel Field(string label, UIElement input) { var panel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) }; panel.Children.Add(Label(label, 11)); panel.Children.Add(input); return panel; }
    private static Button Action(string label, Action action) { var button = new Button { Content = label, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(3) }; button.Click += (_, _) => action(); return button; }
    private static void SetChoice(ComboBox box, IEnumerable<string> values, int selected) { box.ItemsSource = values.ToArray(); box.SelectedIndex = selected; }
}
