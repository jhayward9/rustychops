using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rustychops;

public sealed class NewScorePage : Grid
{
    private readonly TextBox _title = Input("Untitled score"), _artist = Input(""), _track = Input("Guitar"), _tempo = Input("100"), _measures = Input("8");
    private readonly ComboBox _numerator = Choice(Enumerable.Range(1, 12).Cast<object>(), 4), _denominator = Choice(new object[] { 2, 4, 8, 16 }, 4);
    private readonly ComboBox _tuning = Choice(ScoreLibrary.Tunings.Keys.Cast<object>(), ScoreLibrary.Tunings.Keys.First());
    private readonly ComboBox _instrument = Choice(new object[] { "Acoustic · steel string", "Acoustic · nylon string", "Electric · clean", "Electric · overdriven", "Electric · distortion" }, "Acoustic · steel string");
    private readonly TextBlock _status = Text("", 12);
    public event Action<NewScoreOptions>? CreateRequested;

    public event Action? CancelRequested;
    private readonly AlphaTab.Wpf.AlphaTab _preview = new();
    private bool _previewReady;
    public NewScorePage()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition()); RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new StackPanel(); header.Children.Add(Text("SCORE WORKSPACE", 10, Brushes.GreenYellow)); header.Children.Add(Text("New score", 24));
        var commands = new WrapPanel(); var create = new Button { Content = "Create score", Padding = new Thickness(12, 6, 12, 6) }; commands.Children.Add(create);
        var update = new Button { Content = "Update preview", Padding = new Thickness(12, 6, 12, 6) }; update.Click += (_, _) => Preview(); commands.Children.Add(update);
        var cancel = new Button { Content = "Library", Padding = new Thickness(12, 6, 12, 6) }; cancel.Click += (_, _) => CancelRequested?.Invoke(); commands.Children.Add(cancel); header.Children.Add(commands); Children.Add(header);
        var workspace = new DockPanel(); Grid.SetRow(workspace, 1); Children.Add(workspace);
        var tabs = new TabControl { Background = new SolidColorBrush(Color.FromRgb(20,24,31)), BorderThickness = new Thickness(0) };
        foreach (var input in new[] { _title, _artist, _track, _tempo, _measures }) { input.MinWidth = 0; input.Width = 220; }
        var signature = new StackPanel { Orientation = Orientation.Horizontal }; signature.Children.Add(_numerator); signature.Children.Add(Text("/", 20)); signature.Children.Add(_denominator);
        var song = new StackPanel(); song.Children.Add(Field("Title", _title)); song.Children.Add(Field("Artist / composer", _artist));
        var composition = new StackPanel(); composition.Children.Add(Field("Tempo (30–320 BPM)", _tempo)); composition.Children.Add(Field("Time signature", signature)); composition.Children.Add(Field("Starting measures (1–128)", _measures));
        var guitar = new StackPanel(); guitar.Children.Add(Field("Track name", _track)); guitar.Children.Add(Field("Instrument", _instrument)); guitar.Children.Add(Field("Tuning · low → high", _tuning));
        foreach (var (name, content) in new[] { ("Song", song), ("Measure", composition), ("Track", guitar) }) tabs.Items.Add(new TabItem { Header = name, Foreground = Brushes.Black, Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        var inspector = new Border { Width = 265, Padding = new Thickness(10), Margin = new Thickness(10,0,0,0), BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray, Background = new SolidColorBrush(Color.FromRgb(20,24,31)), Child = tabs }; DockPanel.SetDock(inspector,Dock.Right); workspace.Children.Add(inspector);
        var settings = new AlphaTab.Settings(); settings.Core.Engine = "skia"; settings.Core.UseWorkers = false; settings.Display.Scale = .85;
        settings.Player.PlayerMode = AlphaTab.PlayerMode.Disabled; settings.Player.EnableCursor = false; settings.Player.EnableUserInteraction = false;
#pragma warning disable CS0618
        settings.Display.StaveProfile = AlphaTab.StaveProfile.ScoreTab;
#pragma warning restore CS0618
        _preview.Settings = settings; _preview.Background = Brushes.White; _preview.Foreground = Brushes.Black;
        _preview.LayoutUpdated += (_, _) =>
        {
            if (!_previewReady && _preview.IsVisible && _preview.Api != null) { _previewReady = true; Dispatcher.BeginInvoke(Preview, System.Windows.Threading.DispatcherPriority.ContextIdle); }
        };
        workspace.Children.Add(new Border { Background = Brushes.White, Child = _preview });
        _status.Text = "Choose song, measure and track settings, then Create score to start editing. Saved on this PC."; Grid.SetRow(_status, 2); Children.Add(_status);
        create.Click += (_, _) => { try { CreateRequested?.Invoke(Options()); } catch (Exception error) { _status.Text = error.Message; } };
    }
    private NewScoreOptions Options()
    {
        if (!int.TryParse(_tempo.Text, out var tempo) || !int.TryParse(_measures.Text, out var measures)) throw new ArgumentException("Enter whole numbers for tempo and starting measures.");
        int[] programs = [25, 24, 27, 29, 30];
        return new NewScoreOptions(_title.Text, _artist.Text, _track.Text, tempo, (int)_numerator.SelectedItem, (int)_denominator.SelectedItem, measures, (string)_tuning.SelectedItem, programs[_instrument.SelectedIndex]);
    }
    private void Preview()
    {
        if (!_previewReady || _preview.Api == null) return;
        try
        {
            var score = AlphaTab.Importer.ScoreLoader.LoadScoreFromBytes(ScoreLibrary.BuildScore(Options()), new AlphaTab.Settings());
            foreach (var staff in score.Tracks[0].Staves) { staff.ShowStandardNotation = true; staff.ShowTablature = true; }
            _preview.Api.RenderScore(score, null!, null!); _status.Text = "Preview updated · Create score opens this score in the editor.";
        }
        catch (Exception error) { _status.Text = error.Message; }
    }
    private static TextBox Input(string value) => new() { Text = value, Padding = new Thickness(9), MinWidth = 280, Margin = new Thickness(0, 4, 0, 14) };
    private static ComboBox Choice(IEnumerable<object> values, object selected) => new() { ItemsSource = values.ToList(), SelectedItem = selected, MinWidth = 85 };
    private static StackPanel Field(string label, UIElement input) { var field = new StackPanel(); field.Children.Add(Text(label, 12)); field.Children.Add(input); return field; }
    private static TextBlock Text(string value, double size, Brush? color = null) => new() { Text = value, FontSize = size, Foreground = color ?? Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 10) };
    private static Border Section(string title, params UIElement[] fields)
    {
        var panel = new StackPanel(); panel.Children.Add(Text(title, 20)); foreach (var field in fields) panel.Children.Add(field);
        return new Border { Child = panel, Background = new SolidColorBrush(Color.FromRgb(18, 20, 25)), BorderBrush = new SolidColorBrush(Color.FromRgb(41, 45, 53)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(18), Margin = new Thickness(0, 14, 12, 0) };
    }
}
