using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Rustychops;

public sealed class LearnPage : ScrollViewer
{
    private readonly LearnStore _store;
    private readonly string _path;
    private readonly StackPanel _body = new();
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock _elapsed = Label("");
    private readonly TextBlock _notice = Label("");
    private GuitarLesson? _lesson;
    private LessonPart? _part;
    private bool _guided;
    private bool _writable = true;
    private double _savedElapsed;
    public event Action<string>? ListenRequested;
    public event Action? StopRequested;
    public event Action? ProfileRequested;
    public event Action? SettingsRequested;
    public event Action<double, bool>? TransportChanged;
    public double ExampleSpeed { get; private set; } = 1;
    public bool ExampleLoop { get; private set; }
    public LearnStore Store => _store;
    public bool IsPracticing => _clock.IsRunning;
    public LearnPage(string? path = null)
    {
        _path = path ?? LearnStore.DefaultPath;
        try { _store = LearnStore.Load(_path); }
        catch (Exception error) { _store = new(); _writable = false; _notice.Text = "Your saved profile could not be read. It has been preserved. " + error.Message; }
        Content = _body; VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _timer.Tick += (_, _) => { UpdateClock(); if (_clock.Elapsed.TotalSeconds - _savedElapsed >= 10) Flush(); };
        _timer.Start(); ShowHome();
    }
    public void PausePractice() { _clock.Stop(); Flush(); UpdateClock(); StopRequested?.Invoke(); }
    public void Shutdown() { PausePractice(); _timer.Stop(); }
    private void Flush()
    {
        if (_lesson != null && _part != null)
        {
            var elapsed = _clock.Elapsed.TotalSeconds;
            _store.AddTime(_lesson.Id, _part.Id, Math.Max(0, elapsed - _savedElapsed)); _savedElapsed = elapsed;
        }
        Save();
    }
    private void Save()
    {
        if (!_writable) return;
        try { _store.Save(_path); _notice.Text = "Progress saved locally · Completion is self-assessed."; }
        catch (Exception error) { _notice.Text = "Progress could not be saved: " + error.Message; }
    }
    private void BeginPage(string title, string subtitle)
    {
        if (_elapsed.Parent is Panel parent) parent.Children.Remove(_elapsed);
        _body.Children.Clear(); _body.Children.Add(Label(title, 30)); _body.Children.Add(Label(subtitle, 14)); _body.Children.Add(_notice);
    }
    public void ShowHome()
    {
        PausePractice(); BeginPage("What do you want to learn?", "Build your guitar skills at your own pace.");
        _body.Children.Add(Action("Go at your own pace", () => ShowCatalog(false)));
        _body.Children.Add(Label("Choose a skill, riff, scale or practice routine."));
        _body.Children.Add(Action("Guided rock & metal lessons", () => ShowCatalog(true)));
        _body.Children.Add(Label("Start with the basics, then build rhythm and lead skills. This first release covers foundations."));
        if (LessonCatalog.All.FirstOrDefault(item => item.Id == _store.LastLesson) is GuitarLesson last)
            _body.Children.Add(Action("Continue: " + last.Title, () => OpenLesson(last, _store.LastPart)));
        _body.Children.Add(Action("View my profile & achievements", () => ProfileRequested?.Invoke()));
    }
    public void ShowCatalog(bool guided)
    {
        PausePractice(); _guided = guided;
        BeginPage(guided ? "Guided rock & metal lessons" : "Go at your own pace", guided ? "Follow the foundations in order, or revisit any lesson. Advanced modules will follow." : "Choose what you want to practice today.");
        _body.Children.Add(Action("← Learn home", ShowHome));
        foreach (var lesson in LessonCatalog.All)
        {
            int complete = lesson.Parts.Count(part => _store.Completed.Contains(LearnStore.Key(lesson.Id, part.Id)));
            var panel = new StackPanel(); panel.Children.Add(Label(lesson.Level.ToUpperInvariant(), 11)); panel.Children.Add(Label(lesson.Title, 21)); panel.Children.Add(Label(lesson.Outcome));
            panel.Children.Add(Label($"{complete}/{lesson.Parts.Length} parts completed")); panel.Children.Add(Action(_store.IsComplete(lesson) ? "Review lesson" : "Open lesson", () => OpenLesson(lesson)));
            _body.Children.Add(Card(panel));
        }
    }
    public void OpenLesson(GuitarLesson lesson, string? partId = null)
    {
        PausePractice(); _lesson = lesson;
        _part = lesson.Parts.FirstOrDefault(part => part.Id == partId) ?? lesson.Parts.FirstOrDefault(part => !_store.Completed.Contains(LearnStore.Key(lesson.Id, part.Id))) ?? lesson.Parts[0];
        _clock.Reset(); _savedElapsed = 0; _store.LastLesson = lesson.Id; _store.LastPart = _part.Id; Save(); RenderLesson();
    }
    private void RenderLesson()
    {
        var lesson = _lesson!; var part = _part!;
        BeginPage(lesson.Title, lesson.Outcome);
        var navigation = new WrapPanel(); navigation.Children.Add(Action("← Lessons", () => ShowCatalog(_guided))); navigation.Children.Add(Action("Profile", () => ProfileRequested?.Invoke())); _body.Children.Add(navigation);
        var parts = new WrapPanel();
        foreach (var step in lesson.Parts) parts.Children.Add(Action((_store.Completed.Contains(LearnStore.Key(lesson.Id, step.Id)) ? "✓ " : "") + step.Title, () => OpenLesson(lesson, step.Id)));
        _body.Children.Add(parts); _body.Children.Add(Label(part.Title, 24)); _body.Children.Add(Label(part.Instruction, 16));
        if (part.Tab != null)
        {
            var score = new AlphaTab.Wpf.AlphaTab(); var settings = new AlphaTab.Settings(); settings.Core.Engine = "skia"; settings.Core.UseWorkers = false; settings.Player.PlayerMode = AlphaTab.PlayerMode.Disabled; settings.Display.Scale = .85;
#pragma warning disable CS0618
            settings.Display.StaveProfile = AlphaTab.StaveProfile.Tab;
#pragma warning restore CS0618
            score.Settings = settings; score.Background = Brushes.White; score.Foreground = Brushes.Black; score.MinHeight = 180;
            score.Loaded += (_, _) => Dispatcher.BeginInvoke(() => { try { score.Api?.Tex(ScoreText(lesson, part), null!); } catch (Exception error) { _notice.Text = "Tab could not load: " + error.Message; } }, DispatcherPriority.ContextIdle);
            _body.Children.Add(Card(score));
            var playback = new WrapPanel(); playback.Children.Add(Action("▶ Listen to example", () => ListenRequested?.Invoke(ScoreText(lesson, part)))); playback.Children.Add(Action("■ Stop example", () => StopRequested?.Invoke())); _body.Children.Add(playback);
            var tempoText = Label($"{60 * ExampleSpeed:0} BPM");
            var tempo = new Slider { Minimum = 30, Maximum = 120, Value = 60 * ExampleSpeed, Width = 200, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(8), ToolTip = "Example tempo" };
            tempo.ValueChanged += (_, _) => { ExampleSpeed = tempo.Value / 60; tempoText.Text = $"{tempo.Value:0} BPM"; TransportChanged?.Invoke(ExampleSpeed, ExampleLoop); };
            var loop = new CheckBox { Content = "Loop example", IsChecked = ExampleLoop, Foreground = Brushes.White, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
            loop.Click += (_, _) => { ExampleLoop = loop.IsChecked == true; TransportChanged?.Invoke(ExampleSpeed, ExampleLoop); };
            var controls = new WrapPanel(); controls.Children.Add(tempoText); controls.Children.Add(tempo); controls.Children.Add(loop); _body.Children.Add(controls);
            _body.Children.Add(Label("Standard tuning. Listen first, then practice. Synthesized playback only counts toward practice when you explicitly start the timer."));
        }
        if (lesson.Id == "first-contact" && part.Id == "tune") _body.Children.Add(Action("Open guitar tuner", () => SettingsRequested?.Invoke()));
        var practice = new WrapPanel();
        var toggle = Action(_clock.IsRunning ? "Pause practice timer" : "Start practice timer", () => { if (_clock.IsRunning) PausePractice(); else _clock.Start(); RenderLesson(); });
        practice.Children.Add(toggle); practice.Children.Add(_elapsed); _body.Children.Add(practice); UpdateClock();
        _body.Children.Add(Label("Start the timer when you play. Pause for breaks. Leaving Learn or minimizing the app pauses it. Mark each part complete when you feel comfortable; this is not automatic pitch scoring."));
        bool complete = _store.Completed.Contains(LearnStore.Key(lesson.Id, part.Id));
        _body.Children.Add(Action(complete ? "✓ Completed · mark for review" : "Mark this part complete", () => { PausePractice(); if (complete) _store.Completed.Remove(LearnStore.Key(lesson.Id, part.Id)); else _store.Complete(lesson.Id, part.Id); Save(); RenderLesson(); }));
        int index = Array.IndexOf(lesson.Parts, part);
        if (index + 1 < lesson.Parts.Length) _body.Children.Add(Action("Next part →", () => OpenLesson(lesson, lesson.Parts[index + 1].Id)));
        else if (_guided && Array.IndexOf(LessonCatalog.All, lesson) + 1 < LessonCatalog.All.Length) _body.Children.Add(Action("Next lesson →", () => OpenLesson(LessonCatalog.All[Array.IndexOf(LessonCatalog.All, lesson) + 1])));
    }
    public static string ScoreText(GuitarLesson lesson, LessonPart part) => $"\\title \"{lesson.Title.Replace("\"", "") }\"\n\\tempo 60\n\\ts 4 4\n.\n{part.Tab}";
    private void UpdateClock() => _elapsed.Text = $"This session: {_clock.Elapsed:hh\\:mm\\:ss} · Total practice: {Duration(_store.TotalSeconds + Math.Max(0, _clock.Elapsed.TotalSeconds - _savedElapsed))}";
    public void ShowProfile()
    {
        PausePractice(); BeginPage("Your profile", "Your practice, lesson progress and achievements — saved on this computer.");
        var name = new TextBox { Text = _store.Name, MaxLength = 60, Width = 280, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(4, 8, 4, 8) }; _body.Children.Add(Label("Display name")); _body.Children.Add(name);
        _body.Children.Add(Action("Save name", () => { _store.Name = string.IsNullOrWhiteSpace(name.Text) ? "Guitarist" : name.Text.Trim(); Save(); }));
        _body.Children.Add(Label($"{Duration(_store.TotalSeconds)} practiced · {_store.Completed.Count} parts · {LessonCatalog.All.Count(_store.IsComplete)}/{LessonCatalog.All.Length} lessons completed", 20));
        _body.Children.Add(Action("Back to Learn", ShowHome));
        _body.Children.Add(Label("Achievements", 24));
        foreach (var achievement in _store.Achievements)
        {
            bool unlocked = _store.Unlocked.ContainsKey(achievement.Id);
            _body.Children.Add(Label((unlocked ? "★ " : "○ ") + achievement.Title, 18));
            _body.Children.Add(Label(achievement.Detail + (unlocked ? $" · Unlocked {_store.Unlocked[achievement.Id].ToLocalTime():d}" : $" · {Math.Min(100, achievement.Current / achievement.Target * 100):0}%")));
            _body.Children.Add(new ProgressBar { Minimum = 0, Maximum = achievement.Target, Value = Math.Min(achievement.Current, achievement.Target), Height = 7, Margin = new Thickness(4, 2, 4, 12), Foreground = Brushes.GreenYellow });
        }
        _body.Children.Add(Label("Lesson progress", 24));
        foreach (var lesson in LessonCatalog.All)
        {
            var panel = new StackPanel(); panel.Children.Add(Action(lesson.Title, () => OpenLesson(lesson)));
            foreach (var part in lesson.Parts) { var key = LearnStore.Key(lesson.Id, part.Id); panel.Children.Add(Label($"{(_store.Completed.Contains(key) ? "✓" : "○")} {part.Title} · {Duration(_store.PracticeSeconds.GetValueOrDefault(key))}")); }
            _body.Children.Add(Card(panel));
        }
        _body.Children.Add(Label("Achievements celebrate practice and self-reported completion. Earned achievements stay unlocked when you mark a part for review."));
    }
    public static string Duration(double seconds) => seconds < 60 ? $"{seconds:0}s" : seconds < 3600 ? $"{seconds / 60:0} min" : $"{(int)(seconds / 3600)}h {(int)(seconds % 3600 / 60)}m";
    private static TextBlock Label(string text, double size = 13) => new() { Text = text, FontSize = size, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 5, 4, 8) };
    private static Button Action(string text, Action action) { var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Left }; button.Click += (_, _) => action(); return button; }
    private static Border Card(UIElement child) => new() { Child = child, Background = new SolidColorBrush(Color.FromRgb(18, 20, 25)), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0, 10, 0, 10) };
}
