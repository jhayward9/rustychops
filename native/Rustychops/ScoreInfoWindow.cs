using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AlphaTab.Model;

namespace Rustychops;

public static class ScoreInfo
{
    public static readonly string[] MajorKeys = ["C♭", "G♭", "D♭", "A♭", "E♭", "B♭", "F", "C", "G", "D", "A", "E", "B", "F♯", "C♯"];
    public static readonly string[] MinorKeys = ["A♭", "E♭", "B♭", "F", "C", "G", "D", "A", "E", "B", "F♯", "C♯", "G♯", "D♯", "A♯"];
    public static string Key(KeySignature key, KeySignatureType type) => (type == KeySignatureType.Minor ? MinorKeys : MajorKeys)[Math.Clamp((int)key + 7, 0, 14)] + (type == KeySignatureType.Minor ? " minor" : " major");
    public static string TimeSignatures(Score score) => string.Join(", ", score.MasterBars.Select(bar => $"{bar.TimeSignatureNumerator}/{bar.TimeSignatureDenominator}").Distinct());
    public static string Keys(Score score) => string.Join(", ", score.Tracks.SelectMany(track => track.Staves).SelectMany(staff => staff.Bars).Select(bar => Key(bar.KeySignature, bar.KeySignatureType)).Distinct());
}

public sealed class ScoreInfoWindow : Window
{
    public ScoreInfoWindow(Window owner, ScoreLibrary library, LibraryScore entry)
    {
        var score = library.Read(entry);
        Owner = owner; Title = "Score info — " + entry.Title; Width = 650; Height = 580; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = owner.Background; Foreground = Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Label(entry.Title, 26));
        panel.Children.Add(Label($"Artist: {score.Artist}\nTime signature: {ScoreInfo.TimeSignatures(score)}\nKey: {ScoreInfo.Keys(score)}\nTempo: {score.Tempo} BPM\nMeasures: {score.MasterBars.Count} · Tracks: {score.Tracks.Count}", 14));
        panel.Children.Add(Label("Local file", 12));
        panel.Children.Add(new TextBox { Text = entry.Path, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(8) });
        panel.Children.Add(Label("Your notes / specific information", 14));
        var notes = new TextBox { Text = entry.Notes, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 140, Padding = new Thickness(10) };
        panel.Children.Add(notes);
        var status = Label("Notes are saved locally with this library entry.", 12); panel.Children.Add(status);
        var actions = new WrapPanel();
        var save = new Button { Content = "Save info", IsDefault = true, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 8, 12, 0) };
        save.Click += (_, _) => { try { library.SaveNotes(entry, notes.Text); DialogResult = true; } catch (Exception error) { status.Text = error.Message; } };
        var cancel = new Button { Content = "Close", IsCancel = true, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 8, 0, 0) };
        actions.Children.Add(save); actions.Children.Add(cancel); panel.Children.Add(actions);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    private static TextBlock Label(string text, double size) => new() { Text = text, FontSize = size, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
}
