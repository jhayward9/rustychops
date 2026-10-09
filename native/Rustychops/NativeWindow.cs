using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using AlphaTab;
using AlphaTab.Model;
using AlphaTab.Synth;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using ScoreView = AlphaTab.Wpf.AlphaTab;

namespace Rustychops;

/// <summary>The application uses WPF controls throughout, including native score rendering.</summary>
public sealed class NativeWindow : Window
{
    private static readonly Brush BackgroundBrush = Brush("#0B0C0F"), PanelBrush = Brush("#121419"), Accent = Brush("#C9FF3D"), Muted = Brush("#8D95A3");
    private readonly PracticeStore _store;
    private readonly string? _storePath;
    private readonly ScoreLibrary _scoreLibrary;
    private readonly ScoreEditorPage _editor;
    private readonly LearnPage _learn;
    private Score? _beforeLesson;
    private Action? _restoreLessonPlayback;
    private int _lessonPlaybackVersion;
    private readonly AudioInput _audio = new();
    private readonly ScoreView _score = new();
    private readonly Grid _practice = new(), _settings = new(), _libraryPage = new();
    private readonly NewScorePage _newScorePage = new();
    private readonly Expander _libraryMenu = new() { Header = "◇ Library", Foreground = Brushes.White, Margin = new Thickness(4, 6, 4, 6) };
    private readonly StackPanel _libraryEntries = new();
    private readonly TextBlock _libraryStatus = Text("Scores you add or create are saved locally.", 12);
    private readonly StackPanel _recent = new(), _tracks = new(), _regions = new();
    private readonly TextBlock _title = Text("Practice Workbench", 32), _subtitle = Text("Local practice session", 12), _status = Text("Loading practice engine…", 12);
    private readonly TextBlock _audioStatus = Text("Connecting your guitar audio input…", 12), _meterText = Text("No signal", 14), _note = Text("—", 64), _tuning = Text("Waiting for guitar input", 14), _frequency = Text("", 12);
    private readonly TextBlock _time = Text("0:00 / 0:00", 14), _speedText = Text("75%", 14);
    private readonly Button _play = Button("▶ Play");
    private readonly ComboBox _input = new(), _output = new(), _channel = new(), _reference = new();
    private readonly ProgressBar _meter = new() { Minimum = -60, Maximum = 0, Value = -60, Height = 24, Foreground = Accent };
    private readonly Slider _tuner = new() { Minimum = -50, Maximum = 50, Value = 0, IsHitTestVisible = false, Focusable = false };
    private readonly Slider _speed = new() { Minimum = 25, Maximum = 150, Value = 75, TickFrequency = 5, IsSnapToTickEnabled = true, Width = 180 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(110) };
    private CheckBox? _loopToggle;
    private string _currentPath = "sample";
    private bool _initialized, _refreshing;
    private bool _settingsVisible;
    private bool _outputRestored;
    private bool _countInEnabled = true;
    private int _devicePollTicks;
    private int _editorPlaybackVersion;
    private string _deviceSignature = "";
    public bool ScoreReady => _score.Api?.IsReadyForPlayback == true;
    public string CurrentStatus => _status.Text;
    public bool GuitarConnected => _audio.Running;
    public int GuitarConnectionVersion => _audio.SessionVersion;
    public AlphaTabApiBase<ScoreView> PracticeApi => _score.Api ?? throw new InvalidOperationException("Score engine is not ready.");

    public NativeWindow(PracticeStore? store = null, string? storePath = null)
    {
        _storePath = storePath;
        _store = store ?? PracticeStore.Load();
        _scoreLibrary = new ScoreLibrary(_store, storePath);
        _editor = new ScoreEditorPage(_scoreLibrary);
        _learn = new LearnPage(storePath == null ? null : Path.ChangeExtension(Path.GetFullPath(storePath), ".learn.json"));
        Title = "Rustychops — Desktop Practice Lab";
        // WPF dimensions use logical pixels. Keep restored bounds inside the desktop
        // work area so display scaling cannot place the title bar off screen.
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(1440, workArea.Width); Height = Math.Min(940, workArea.Height);
        MinWidth = Math.Min(1000, workArea.Width); MinHeight = Math.Min(720, workArea.Height);
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        WindowState = WindowState.Maximized;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BackgroundBrush; Foreground = Brushes.White; FontFamily = new FontFamily("Segoe UI");
        Resources.Add(typeof(Button), ButtonStyle());
        Resources.Add(typeof(ComboBox), ComboStyle());
        var shell = new Grid(); shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) }); shell.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar = new DockPanel { Margin = new Thickness(20) };
        var brand = new StackPanel();
        brand.Children.Add(Text("Rustychops", 22, Accent)); brand.Children.Add(Text("Desktop practice lab", 11, Muted));
        brand.Children.Add(ActionButton("▤ Practice", () => ShowSettings(false)));
        var settingsButton = ActionButton("⚙ Configure Guitar & Audio Settings", () => ShowSettings(true));
        settingsButton.Content = new TextBlock { Text = "⚙ Configure Guitar &\nAudio Settings", TextWrapping = TextWrapping.Wrap };
        brand.Children.Add(settingsButton);
        var libraryOptions = new StackPanel { Margin = new Thickness(12, 4, 0, 4) };
        libraryOptions.Children.Add(ActionButton("Open score", OpenScore));
        libraryOptions.Children.Add(ActionButton("Add score", AddScore));
        libraryOptions.Children.Add(ActionButton("New score", ShowNewScore));
        var libraryHeader = ActionButton("◇ Library", () => { if (ShowLibrary()) _libraryMenu.IsExpanded = true; });
        libraryHeader.Margin = new Thickness(0); libraryHeader.MinWidth = 160;
        _libraryMenu.Header = libraryHeader;
        _libraryMenu.Content = libraryOptions; _libraryMenu.Expanded += (_, _) => ShowLibrary(); brand.Children.Add(_libraryMenu);
        brand.Children.Add(ActionButton("Learn", () => ShowLearn(false)));
        brand.Children.Add(ActionButton("Profile", () => ShowLearn(true)));
        brand.Children.Add(Text("RECENT SCORES", 11, Muted)); brand.Children.Add(_recent);
        DockPanel.SetDock(brand, Dock.Top); sidebar.Children.Add(brand);
        var local = Text("Local by design\nNo account. No uploads.\nNative Windows audio.", 12, Muted); DockPanel.SetDock(local, Dock.Bottom); sidebar.Children.Add(local);
        shell.Children.Add(new Border { Background = Brush("#0E1014"), BorderBrush = Brush("#292D35"), BorderThickness = new Thickness(0, 0, 1, 0), Child = sidebar });
        var workspace = new Grid { Margin = new Thickness(30, 24, 30, 24) }; Grid.SetColumn(workspace, 1); shell.Children.Add(workspace);
        BuildPractice(); BuildSettings(); BuildLibrary(); workspace.Children.Add(_practice); workspace.Children.Add(_settings); workspace.Children.Add(_libraryPage); workspace.Children.Add(_newScorePage); workspace.Children.Add(_editor);
        _settings.Visibility = _libraryPage.Visibility = _newScorePage.Visibility = _editor.Visibility = Visibility.Collapsed;
        workspace.Children.Add(_learn); _learn.Visibility = Visibility.Collapsed;
        _learn.ProfileRequested += () => ShowLearn(true);
        _learn.SettingsRequested += () => ShowSettings(true);
        _learn.StopRequested += () => { _lessonPlaybackVersion++; _score.Api?.Stop(); };
        _learn.TransportChanged += (speed, loop) => { if (_beforeLesson != null && _score.Api != null) { _score.Api.PlaybackSpeed = speed; _score.Api.IsLooping = loop; } };
        _learn.ListenRequested += async text =>
        {
            int version = ++_lessonPlaybackVersion;
            try
            {
                var api = PracticeApi;
                if (_beforeLesson == null)
                {
                    _beforeLesson = api.Score; var range = api.PlaybackRange; var speed = api.PlaybackSpeed; var loop = api.IsLooping; var countIn = api.CountInVolume;
                    _restoreLessonPlayback = () => { api.PlaybackRange = range; api.PlaybackSpeed = speed; api.IsLooping = loop; api.CountInVolume = countIn; };
                }
                api.Stop(); api.PlaybackRange = null!; api.IsLooping = _learn.ExampleLoop; api.PlaybackSpeed = _learn.ExampleSpeed; api.CountInVolume = .8;
                api.Tex(text, null!);
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    await Task.Delay(100);
                    if (version != _lessonPlaybackVersion || _learn.Visibility != Visibility.Visible) return;
                    if (api.IsReadyForPlayback) { api.Play(); return; }
                }
                MessageBox.Show(this, "Example is still loading. Try Listen again.", "Learn");
            }
            catch (Exception error) { MessageBox.Show(this, "Could not play example: " + error.Message, "Learn"); }
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) _learn.PausePractice(); };
        _newScorePage.CreateRequested += options => { var entry = _scoreLibrary.Create(options); RefreshLibrary(); ShowEditor(entry); };
        _editor.Saved += _ => { RefreshLibrary(); RefreshRecent(); };
        _editor.ViewRequested += entry => LoadScore(entry.Path);
        _editor.LibraryRequested += () => ShowLibrary();
        _newScorePage.CancelRequested += () => ShowLibrary();
        _editor.PlayRequested += async score =>
        {
            int version = ++_editorPlaybackVersion;
            try
            {
                var api = PracticeApi; api.Stop();
                api.PlaybackRange = null!; api.IsLooping = false; api.PlaybackSpeed = 1; api.CountInVolume = 0;
                var preview = AlphaTab.Importer.ScoreLoader.LoadScoreFromBytes(ScoreLibrary.Encode(score), api.Settings);
                api.RenderScore(preview, null!, null!);
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    await Task.Delay(100);
                    if (version != _editorPlaybackVersion || _editor.Visibility != Visibility.Visible) return;
                    if (!api.IsReadyForPlayback) continue;
                    api.Play(); _editor.SetStatus("Playing your current edits · Save to keep them locally."); return;
                }
                _editor.SetStatus("Playback is still loading. Try Play again.");
            }
            catch (Exception error) { _editor.SetStatus("Playback error: " + error.Message); }
        };
        _editor.StopRequested += () => { _editorPlaybackVersion++; _score.Api?.Stop(); _editor.SetStatus("Playback stopped."); };
        Content = shell;
        _audio.Stopped += message =>
        {
            var version = _audio.SessionVersion;
            Dispatcher.BeginInvoke(() => { if (version != _audio.SessionVersion) return; _audio.Stop(); _audioStatus.Text = $"{message} Retrying the guitar connection automatically."; });
        };
        Loaded += (_, _) => { RefreshDevices(); StartInput(); };
        _timer.Tick += (_, _) => ReadAudio(); _timer.Start();
        Closing += (_, args) =>
        {
            if (!CanLeaveEditor()) { args.Cancel = true; return; }
            _learn.Shutdown(); _timer.Stop(); _audio.Dispose();
            // alphaTab 1.8.4 throws from its worker cancellation on Destroy. Release native
            // playback explicitly; its background worker exits with the application process.
            _score.Api?.Stop(); _score.Api?.Player?.Output.Destroy(); Save();
        };
        RefreshRecent();
    }

    private void BuildPractice()
    {
        _practice.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _practice.RowDefinitions.Add(new RowDefinition());
        _practice.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _practice.RowDefinitions.Add(new RowDefinition { Height = new GridLength(210) });
        var heading = new StackPanel(); heading.Children.Add(Text("NOW PRACTICING", 10, Accent)); heading.Children.Add(_title); heading.Children.Add(_subtitle); heading.Children.Add(_status); _practice.Children.Add(heading);
        var scoreContainer = new DockPanel();
        var toolbar = new WrapPanel(); toolbar.Children.Add(Text("Score · Drag across beats to create a loop", 12)); toolbar.Children.Add(ActionButton("Save region", SaveRegion)); toolbar.Children.Add(ActionButton("Clear range", () => { if (_score.Api != null) { _score.Api.PlaybackRange = null!; _score.Api.ClearPlaybackRangeHighlight(); } }));
        DockPanel.SetDock(toolbar, Dock.Top); scoreContainer.Children.Add(toolbar);
        var settings = new Settings(); settings.Core.Engine = "skia"; settings.Core.UseWorkers = false;
        // The WPF facade attaches its canvas asynchronously. Enable cursors after that attachment.
        settings.Player.PlayerMode = PlayerMode.Disabled; settings.Player.EnableUserInteraction = false; settings.Player.EnableCursor = false;
        settings.Display.Scale = .85;
#pragma warning disable CS0618 // Compatibility setting matches the existing alphaTab 1.8 tablature view.
        settings.Display.StaveProfile = StaveProfile.Tab;
#pragma warning restore CS0618
        _score.Settings = settings; _score.Background = Brush("#F6F4EE"); _score.Foreground = Brushes.Black;
        _score.SelectionFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 100, 150, 30)); _score.BeatCursorFill = Accent;
        _score.Loaded += (_, _) => Dispatcher.BeginInvoke(InitializeScore, DispatcherPriority.ContextIdle); scoreContainer.Children.Add(_score);
        var scoreBorder = Card(scoreContainer); Grid.SetRow(scoreBorder, 1); _practice.Children.Add(scoreBorder);
        var transport = new WrapPanel { Margin = new Thickness(0, 12, 0, 12) };
        transport.Children.Add(ActionButton("■ Stop", () => _score.Api?.Stop())); _play.IsEnabled = false; _play.Click += (_, _) => _score.Api?.PlayPause(); transport.Children.Add(_play); transport.Children.Add(_time);
        transport.Children.Add(Text("SPEED", 11, Muted)); transport.Children.Add(ActionButton("−", () => _speed.Value -= 5)); transport.Children.Add(_speedText); transport.Children.Add(ActionButton("＋", () => _speed.Value += 5));
        _speed.ValueChanged += (_, _) => { _speedText.Text = $"{_speed.Value:0}%"; if (_score.Api != null) _score.Api.PlaybackSpeed = _speed.Value / 100; }; transport.Children.Add(_speed);
        _loopToggle = Toggle("Loop", false, value => { if (_score.Api != null) _score.Api.IsLooping = value; }); transport.Children.Add(_loopToggle);
        transport.Children.Add(Toggle("Metronome", true, value => { if (_score.Api != null) _score.Api.MetronomeVolume = value ? .8 : 0; }));
        transport.Children.Add(Toggle("Count-in", true, value => { _countInEnabled = value; if (_score.Api != null) _score.Api.CountInVolume = value ? .8 : 0; }));
        Grid.SetRow(transport, 2); _practice.Children.Add(transport);
        var lower = new Grid(); lower.ColumnDefinitions.Add(new ColumnDefinition()); lower.ColumnDefinitions.Add(new ColumnDefinition());
        var tracks = Section("MIXER", "Tracks", new ScrollViewer { Content = _tracks, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); lower.Children.Add(tracks);
        var regions = Section("FOCUS", "Practice regions", new ScrollViewer { Content = _regions, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Grid.SetColumn(regions, 1); lower.Children.Add(regions); Grid.SetRow(lower, 3); _practice.Children.Add(lower);
    }

    private void InitializeScore()
    {
        if (_initialized || _score.Api == null) return;
        _initialized = true;
        var api = _score.Api;
        api.ScoreLoaded.On(score => Ui(() => { foreach (var track in score.Tracks) foreach (var staff in track.Staves) { staff.ShowStandardNotation = false; staff.ShowTablature = true; } _title.Text = string.IsNullOrWhiteSpace(score.Title) ? "Untitled score" : score.Title; _subtitle.Text = $"{score.Artist} · {(_currentPath == "sample" ? "Getting Started" : Path.GetFileName(_currentPath))}"; BuildTracks(score); RefreshRegions(); }));
        api.PlayerReady.On(() => Ui(async () =>
        {
            _play.IsEnabled = true; _status.Text = "Ready to practice";
            if (!_outputRestored && !string.IsNullOrEmpty(_store.OutputDevice))
            {
                _outputRestored = true;
                try { await SetPlaybackOutput(_store.OutputDevice); }
                catch { _status.Text = "Saved output is unavailable. Choose an output in audio settings."; }
            }
        }));
        api.PlayerStateChanged.On(args => Ui(() => { _play.Content = args.State == PlayerState.Playing ? "Ⅱ Pause" : "▶ Play"; _editor.SetPlaying(args.State == PlayerState.Playing); }));
        api.PlayerPositionChanged.On(args => Ui(() => _time.Text = $"{Time(args.CurrentTime)} / {Time(args.EndTime)}"));
        api.Error.On(error => Ui(() => _status.Text = $"Score error: {error.Message}"));
        api.PlaybackRangeChanged.On(args => Ui(() => _status.Text = args.PlaybackRange == null ? "Ready to practice" : "Practice range selected. Save it for focused repetition."));
        api.Settings.Player.PlayerMode = PlayerMode.EnabledSynthesizer; api.Settings.Player.EnableUserInteraction = true; api.Settings.Player.EnableCursor = true; api.UpdateSettings();
        api.PlaybackSpeed = .75; api.MetronomeVolume = .8; api.CountInVolume = .8;
        api.LoadSoundFont(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "sonivox.sf2")), false);
        api.Tex(SampleScore, null!);
    }

    private void OpenScore()
    {
        var picker = new OpenFileDialog { Filter = "Scores|*.gp;*.gpx;*.gp3;*.gp4;*.gp5;*.xml;*.musicxml;*.capx;*.tex|All files|*.*" };
        if (picker.ShowDialog(this) == true) LoadScore(picker.FileName);
    }
    public void LoadScore(string path)
    {
        if (!CanLeaveEditor()) return;
        LeaveLearn();
        try
        {
            if (_score.Api == null) throw new InvalidOperationException("Score engine is starting. Try again shortly.");
            _score.Api.Stop();
            var bytes = File.ReadAllBytes(path);
            // Parse before replacing the current practice session, so failed imports leave it intact.
            if (Path.GetExtension(path).Equals(".tex", StringComparison.OrdinalIgnoreCase))
            {
                _currentPath = Path.GetFullPath(path); _score.Api.Tex(System.Text.Encoding.UTF8.GetString(bytes), null!);
            }
            else
            {
                var score = AlphaTab.Importer.ScoreLoader.LoadScoreFromBytes(bytes, _score.Settings);
                _currentPath = Path.GetFullPath(path); _score.Api.RenderScore(score, null!, null!);
            }
            _store.Recent.RemoveAll(item => item.Path.Equals(_currentPath, StringComparison.OrdinalIgnoreCase)); _store.Recent.Insert(0, new RecentScore(_currentPath, DateTime.Now)); _store.Recent = _store.Recent.Take(8).ToList(); Save(); RefreshRecent(); ShowSettings(false);
        }
        catch (Exception error) { _status.Text = $"Could not open score: {error.Message}"; }
    }
    private void BuildTracks(Score score)
    {
        _tracks.Children.Clear();
        foreach (var track in score.Tracks)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            row.Children.Add(ActionButton(track.Name.Length == 0 ? $"Track {track.Index + 1}" : track.Name, () => _score.Api?.RenderTracks([track], null!)));
            row.Children.Add(Toggle("M", false, value => _score.Api?.ChangeTrackMute([track], value)));
            row.Children.Add(Toggle("S", false, value => _score.Api?.ChangeTrackSolo([track], value)));
            var volume = new Slider { Minimum = 0, Maximum = 150, Value = 100, MinWidth = 80, VerticalAlignment = VerticalAlignment.Center, ToolTip = $"{track.Name} volume" };
            volume.ValueChanged += (_, _) => _score.Api?.ChangeTrackVolume([track], volume.Value / 100); row.Children.Add(volume); _tracks.Children.Add(row);
        }
    }
    private void SaveRegion()
    {
        var range = _score.Api?.PlaybackRange;
        if (range == null) { _status.Text = "Drag across the score to select a range first."; return; }
        var dialog = new Window { Title = "Name practice region", Owner = this, Width = 380, Height = 165, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = PanelBrush };
        var panel = new StackPanel { Margin = new Thickness(16) }; var name = new TextBox { Text = $"Loop {_store.Regions.Count(item => item.Path == _currentPath) + 1}", Margin = new Thickness(0, 0, 0, 12) }; panel.Children.Add(name); var save = ActionButton("Save region", () => dialog.DialogResult = true); save.IsDefault = true; panel.Children.Add(save); dialog.Content = panel;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(name.Text)) { _store.Regions.Insert(0, new PracticeRegion(Guid.NewGuid().ToString(), _currentPath, name.Text.Trim(), range.StartTick, range.EndTick, _speed.Value)); Save(); RefreshRegions(); }
    }
    private void RefreshRecent()
    {
        _recent.Children.Clear(); foreach (var recent in _store.Recent) { var button = ActionButton(Path.GetFileName(recent.Path), () => LoadScore(recent.Path)); button.ToolTip = recent.Path; _recent.Children.Add(button); }
    }
    private void RefreshRegions()
    {
        _regions.Children.Clear();
        foreach (var region in _store.Regions.Where(item => item.Path == _currentPath)) _regions.Children.Add(ActionButton($"{region.Name} · {region.Speed:0}% · Load →", () => { if (_score.Api is not { } api) return; api.PlaybackRange = new PlaybackRange { StartTick = region.StartTick, EndTick = region.EndTick }; api.IsLooping = true; if (_loopToggle != null) _loopToggle.IsChecked = true; _speed.Value = region.Speed; }));
        if (_regions.Children.Count == 0) _regions.Children.Add(Text("No saved regions yet.\nDrag across the score, then save the selected range.", 12, Muted));
    }

    private void BuildSettings()
    {
        var content = new StackPanel(); _settings.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        content.Children.Add(Text("SETUP", 10, Accent)); content.Children.Add(Text("Configure Guitar & Audio Settings", 30)); content.Children.Add(Text("Native Windows devices · Continuous guitar audio capture", 12, Muted));
        var deviceBody = new StackPanel(); deviceBody.Children.Add(_audioStatus);
        var selectors = new Grid(); selectors.ColumnDefinitions.Add(new ColumnDefinition()); selectors.ColumnDefinitions.Add(new ColumnDefinition());
        var inputPanel = new StackPanel(); inputPanel.Children.Add(Text("Guitar audio input", 12)); inputPanel.Children.Add(_input); selectors.Children.Add(inputPanel);
        var outputPanel = new StackPanel(); outputPanel.Children.Add(Text("Playback audio output", 12)); outputPanel.Children.Add(_output); Grid.SetColumn(outputPanel, 1); selectors.Children.Add(outputPanel); deviceBody.Children.Add(selectors);
        deviceBody.Children.Add(Text("Input channel", 12)); deviceBody.Children.Add(_channel);
        var actions = new WrapPanel(); actions.Children.Add(ActionButton("Refresh devices", RefreshDevices)); deviceBody.Children.Add(actions);
        deviceBody.Children.Add(Text("Your guitar input stays connected across all screens until Rustychops closes.", 12, Muted));
        deviceBody.Children.Add(Text("Use a clean guitar tone for tuning. Input is analysed locally and is not sent to speakers.", 12, Muted)); content.Children.Add(Section("AUDIO", "Audio devices", deviceBody));
        _input.SelectionChanged += (_, _) => { if (_refreshing) return; _store.InputDevice = (_input.SelectedItem as AudioDevice)?.Id; Save(); StartInput(); };
        _channel.SelectionChanged += (_, _) => { if (_refreshing) return; _store.InputChannel = Math.Max(0, _channel.SelectedIndex); _audio.ResetChannel(_store.InputChannel); Save(); };
        _output.SelectionChanged += async (_, _) =>
        {
            if (_refreshing || _score.Api == null) return;
            try
            {
                string? id = (_output.SelectedItem as AudioDevice)?.Id;
                await SetPlaybackOutput(id); _store.OutputDevice = id; Save(); _audioStatus.Text = "Playback output updated.";
            }
            catch (Exception error) { _audioStatus.Text = $"Output error: {error.Message}"; }
        };
        var meterBody = new StackPanel(); meterBody.Children.Add(_meter); meterBody.Children.Add(_meterText); meterBody.Children.Add(Text("−60 dBFS → 0 dBFS · Adjust gain on your interface.", 12, Muted)); content.Children.Add(Section("SIGNAL", "Input volume", meterBody));
        var tunerBody = new StackPanel(); _note.Foreground = Accent; _note.HorizontalAlignment = HorizontalAlignment.Center; tunerBody.Children.Add(_note); _frequency.HorizontalAlignment = HorizontalAlignment.Center; tunerBody.Children.Add(_frequency); tunerBody.Children.Add(_tuner); _tuning.HorizontalAlignment = HorizontalAlignment.Center; tunerBody.Children.Add(_tuning);
        tunerBody.Children.Add(Text("Flat                       In tune                       Sharp", 12, Muted)); tunerBody.Children.Add(Text("Standard tuning: E2 · A2 · D3 · G3 · B3 · E4", 14));
        _reference.ItemsSource = new[] { 432d, 440d, 442d }; _reference.SelectedItem = _store.TunerReference; _reference.SelectionChanged += (_, _) => { if (_reference.SelectedItem is double reference) { _store.TunerReference = reference; Save(); } }; tunerBody.Children.Add(Text("Reference pitch (A4 Hz)", 12)); tunerBody.Children.Add(_reference); content.Children.Add(Section("TUNE", "Guitar tuner", tunerBody));
    }

    public void ShowSettings(bool settings)
    {
        if (!CanLeaveEditor()) return;
        LeaveLearn();
        _editor.Visibility = Visibility.Collapsed;
        _libraryPage.Visibility = _newScorePage.Visibility = Visibility.Collapsed;
        _settingsVisible = settings; _settings.Visibility = settings ? Visibility.Visible : Visibility.Collapsed; _practice.Visibility = settings ? Visibility.Collapsed : Visibility.Visible;
        if (settings) { _score.Api?.Stop(); RefreshDevices(); }
        else if (_score.Api != null) { _score.Api.PlaybackSpeed = _speed.Value / 100; _score.Api.IsLooping = _loopToggle?.IsChecked == true; _score.Api.CountInVolume = _countInEnabled ? .8 : 0; }
    }
    public void ShowLearn(bool profile = false)
    {
        if (!CanLeaveEditor()) return;
        _score.Api?.Stop(); _settingsVisible = false;
        _practice.Visibility = _settings.Visibility = _libraryPage.Visibility = _newScorePage.Visibility = _editor.Visibility = Visibility.Collapsed;
        _learn.Visibility = Visibility.Visible;
        if (profile) _learn.ShowProfile(); else _learn.ShowHome();
    }
    private void LeaveLearn()
    {
        if (_learn.Visibility != Visibility.Visible) return;
        _learn.PausePractice(); _learn.Visibility = Visibility.Collapsed;
        if (_beforeLesson != null && _score.Api != null) { _score.Api.RenderScore(_beforeLesson, null!, null!); _beforeLesson = null; _restoreLessonPlayback?.Invoke(); _restoreLessonPlayback = null; }
    }
    private void BuildLibrary()
    {
        var content = new StackPanel();
        content.Children.Add(Text("YOUR SCORES", 10, Accent)); content.Children.Add(Text("Library", 32)); content.Children.Add(_libraryStatus);
        var actions = new WrapPanel(); actions.Children.Add(ActionButton("Add score", AddScore)); actions.Children.Add(ActionButton("New score", ShowNewScore)); content.Children.Add(actions);
        content.Children.Add(_libraryEntries); _libraryPage.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        RefreshLibrary();
    }
    private void RefreshLibrary()
    {
        _libraryEntries.Children.Clear();
        if (_store.Library.Count == 0) { _libraryEntries.Children.Add(Text("Your library is empty. Add an existing score or create a new one.", 14, Muted)); return; }
        foreach (var entry in _store.Library)
        {
            var row = new DockPanel(); var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var view = ActionButton("View", () => LoadScore(entry.Path)); var edit = ActionButton("Edit", () => ShowEditor(entry));
            var info = ActionButton("Info", () => ShowScoreInfo(entry)); var delete = ActionButton("Delete", () => DeleteScore(entry));
            view.IsEnabled = edit.IsEnabled = info.IsEnabled = File.Exists(entry.Path);
            actions.Children.Add(view); actions.Children.Add(edit); actions.Children.Add(info); actions.Children.Add(delete); DockPanel.SetDock(actions, Dock.Right); row.Children.Add(actions);
            var detail = new StackPanel(); detail.Children.Add(Text(entry.Title, 18)); detail.Children.Add(Text($"{Path.GetExtension(entry.Path).TrimStart('.').ToUpperInvariant()} · Added {entry.AddedAt:d}" + (File.Exists(entry.Path) ? "" : " · File unavailable"), 11, Muted)); row.Children.Add(detail); _libraryEntries.Children.Add(Card(row));
        }
    }
    private bool CanLeaveEditor()
    {
        if (_editor.Visibility != Visibility.Visible) return true;
        if (!_editor.TryLeave()) return false;
        _editorPlaybackVersion++; _score.Api?.Stop(); return true;
    }
    public void ShowEditor(LibraryScore entry)
    {
        if (!CanLeaveEditor()) return;
        LeaveLearn();
        try
        {
            _editor.Load(entry); _score.Api?.Stop(); _settingsVisible = false;
            _practice.Visibility = _settings.Visibility = _newScorePage.Visibility = _libraryPage.Visibility = Visibility.Collapsed; _editor.Visibility = Visibility.Visible;
        }
        catch (Exception error) { _libraryStatus.Text = "Could not edit score: " + error.Message; }
    }
    private void ShowScoreInfo(LibraryScore entry)
    {
        try { new ScoreInfoWindow(this, _scoreLibrary, entry).ShowDialog(); RefreshLibrary(); }
        catch (Exception error) { _libraryStatus.Text = "Could not load score info: " + error.Message; }
    }
    private void DeleteScore(LibraryScore entry)
    {
        if (MessageBox.Show(this, $"Delete ‘{entry.Title}’ and its notes from your local library? Your original imported file and exports will be kept.", "Delete score", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { _scoreLibrary.Delete(entry); _libraryStatus.Text = "Deleted from your local library."; RefreshLibrary(); RefreshRecent(); }
        catch (Exception error) { _libraryStatus.Text = "Could not delete score: " + error.Message; }
    }
    public bool ShowLibrary()
    {
        if (!CanLeaveEditor()) return false;
        LeaveLearn();
        _editor.Visibility = Visibility.Collapsed;
        _score.Api?.Stop(); _settingsVisible = false;
        _practice.Visibility = _settings.Visibility = _newScorePage.Visibility = Visibility.Collapsed; _libraryPage.Visibility = Visibility.Visible; RefreshLibrary();
        return true;
    }
    public void ShowNewScore()
    {
        if (!CanLeaveEditor()) return;
        LeaveLearn();
        _editor.Visibility = Visibility.Collapsed;
        _score.Api?.Stop(); _settingsVisible = false;
        _practice.Visibility = _settings.Visibility = _libraryPage.Visibility = Visibility.Collapsed; _newScorePage.Visibility = Visibility.Visible;
    }
    private void AddScore()
    {
        var picker = new OpenFileDialog { Title = "Add scores to Library", Multiselect = true, Filter = "Guitar Pro and MusicXML scores|*.gp;*.gpx;*.gp3;*.gp4;*.gp5;*.xml;*.musicxml;*.capx" };
        if (picker.ShowDialog(this) != true) return;
        int added = 0; var failures = new List<string>();
        foreach (var path in picker.FileNames)
        {
            try { int previous = _store.Library.Count; _scoreLibrary.Add(path); if (_store.Library.Count > previous) added++; }
            catch (Exception error) { failures.Add($"{Path.GetFileName(path)}: {error.Message}"); }
        }
        _libraryStatus.Text = $"Added {added} score(s). Identical files already in the library are skipped." + (failures.Count == 0 ? "" : "\n" + string.Join("\n", failures)); ShowLibrary();
    }
    private void RefreshDevices()
    {
        try
        {
            _refreshing = true;
            var inputs = new[] { new AudioDevice("", "System default input") }.Concat(AudioInput.Devices(DataFlow.Capture)).ToList();
            if (!string.IsNullOrEmpty(_store.InputDevice) && inputs.All(item => item.Id != _store.InputDevice)) inputs.Add(new AudioDevice(_store.InputDevice, "Saved guitar input (disconnected)"));
            var outputs = new[] { new AudioDevice("", "System default output") }.Concat(AudioInput.Devices(DataFlow.Render)).ToList();
            _input.ItemsSource = inputs; _input.SelectedItem = inputs.FirstOrDefault(item => item.Id == _store.InputDevice) ?? inputs[0];
            _output.ItemsSource = outputs; _output.SelectedItem = outputs.FirstOrDefault(item => item.Id == _store.OutputDevice) ?? outputs[0];
            _channel.ItemsSource = Enumerable.Range(1, _audio.ChannelCount).Select(index => $"Channel {index}").ToList(); _channel.SelectedIndex = Math.Min(_store.InputChannel, _audio.ChannelCount - 1);
            _deviceSignature = string.Join('|', inputs.Concat(outputs).Select(item => item.Id));
        }
        catch (Exception error) { _audioStatus.Text = $"Device discovery failed: {error.Message}"; }
        finally { _refreshing = false; }
    }
    private void StartInput()
    {
        try
        {
            _audio.Start((_input.SelectedItem as AudioDevice)?.Id); _audio.ResetChannel(_store.InputChannel);
            _refreshing = true; _channel.ItemsSource = Enumerable.Range(1, _audio.ChannelCount).Select(index => $"Channel {index}").ToList(); _channel.SelectedIndex = Math.Min(_store.InputChannel, _audio.ChannelCount - 1); _refreshing = false;
            _audioStatus.Text = "Listening through Windows WASAPI. Pluck one string and let it ring.";
        }
        catch (Exception error) { _refreshing = false; _audio.Stop(); _audioStatus.Text = $"Could not open input: {error.Message} Retrying automatically."; }
    }
    private void ReadAudio()
    {
        if (++_devicePollTicks >= 25)
        {
            _devicePollTicks = 0;
            try
            {
                var endpoints = new[] { new AudioDevice("", "") }.Concat(AudioInput.Devices(DataFlow.Capture)).Concat(new[] { new AudioDevice("", "") }).Concat(AudioInput.Devices(DataFlow.Render));
                if (string.Join('|', endpoints.Select(item => item.Id)) != _deviceSignature) RefreshDevices();
            }
            catch (Exception error) { _audioStatus.Text = $"Device update failed: {error.Message}"; }
            if (!_audio.Running) StartInput();
        }
        if (!_settingsVisible) return;
        var reading = _audio.Read(); _meter.Value = reading.Decibels;
        _meterText.Text = reading.Clipping ? "Clipping — lower your interface gain" : reading.Decibels <= -60 ? "No signal" : $"{reading.Decibels:0.0} dBFS";
        if (reading.Frequency is double frequency)
        {
            var pitch = PitchDetector.Describe(frequency, _store.TunerReference); _note.Text = pitch.Name; _frequency.Text = $"{frequency:0.0} Hz"; _tuner.Value = Math.Clamp(pitch.Cents, -50, 50); _tuning.Text = Math.Abs(pitch.Cents) <= 5 ? "In tune" : $"{Math.Abs(pitch.Cents):0} cents {(pitch.Cents < 0 ? "flat" : "sharp")}";
        }
        else { _note.Text = "—"; _frequency.Text = ""; _tuner.Value = 0; _tuning.Text = _audio.Running ? "Pluck one string and let it ring" : "Waiting for guitar input to reconnect"; }
    }
    private void Save() { try { _store.Save(_storePath); } catch (Exception error) { _status.Text = $"Could not save settings: {error.Message}"; } }
    public static bool OutputIdsMatch(string playbackId, string? endpointId) => endpointId != null && (playbackId == endpointId || Guid.TryParse(endpointId.Split('{').Last().TrimEnd('}'), out var id) && playbackId.Equals(id.ToString("N"), StringComparison.OrdinalIgnoreCase));
    public async Task SetPlaybackOutput(string? id)
    {
        var api = PracticeApi;
        var devices = await api.EnumerateOutputDevices();
        var selected = devices.FirstOrDefault(device => OutputIdsMatch(device.DeviceId, id));
        if (!string.IsNullOrEmpty(id) && selected == null) throw new InvalidOperationException("The playback engine cannot use that output. Refresh devices or choose the system default.");
        await api.SetOutputDevice(selected!);
    }
    public async Task<IReadOnlyList<string>> PlaybackDevices() => _score.Api == null ? [] : (await _score.Api.EnumerateOutputDevices()).Select(device => $"{device.DeviceId}: {device.Label}").ToList();
    private void Ui(Action action) { if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(action); }
    private static string Time(double milliseconds) => TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(@"m\:ss");
    private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
    private static TextBlock Text(string text, double size = 12, Brush? color = null) => new() { Text = text, FontSize = size, Foreground = color ?? Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 5, 4, 7), VerticalAlignment = VerticalAlignment.Center };
    private static Button Button(string title) => new() { Content = title };
    private static Button ActionButton(string title, Action action) { var button = Button(title); button.Click += (_, _) => action(); return button; }
    private static CheckBox Toggle(string title, bool initial, Action<bool> action) { var check = new CheckBox { Content = title, IsChecked = initial, Foreground = Brushes.White, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center }; check.Click += (_, _) => action(check.IsChecked == true); return check; }
    private static Border Card(UIElement child) => new() { Child = child, Background = PanelBrush, BorderBrush = Brush("#292D35"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(0, 12, 10, 0) };
    private static Border Section(string eyebrow, string title, UIElement body) { var dock = new DockPanel(); var heading = new StackPanel(); heading.Children.Add(Text(eyebrow, 10, Accent)); heading.Children.Add(Text(title, 20)); DockPanel.SetDock(heading, Dock.Top); dock.Children.Add(heading); dock.Children.Add(body); return Card(dock); }
    private static Style ButtonStyle() { var style = new Style(typeof(Button)); style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#20242B"))); style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White)); style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 9, 12, 9))); style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 6, 4, 6))); style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#3A404B"))); return style; }
    private static Style ComboStyle() { var style = new Style(typeof(ComboBox)); style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black)); style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8))); style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 6, 4, 12))); return style; }
    private const string SampleScore = """
        \title "Practice Workbench"
        \subtitle "Open a Guitar Pro or MusicXML file to begin"
        \tempo 100
        \ts 4 4
        .
        :8 5.6 7.6 8.6 5.5 7.5 8.5 10.5 7.4 |
        8.4 10.4 7.3 9.3 10.3 7.2 8.2 10.2 |
        10.2 8.2 7.2 10.3 9.3 7.3 10.4 8.4 |
        7.4 10.5 8.5 7.5 5.5 8.6 7.6 5.6 |
        """;
}
