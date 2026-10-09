using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using Rustychops;

internal static class Program
{
 [STAThread]
 private static int Main(string[] args)
 {
  try
  {
   foreach (int rate in new[] { 44100, 48000 })
    foreach (double frequency in new[] { 65.406, 82.407, 110, 146.832, 195.998, 246.942, 329.628, 440 })
     foreach (double harmonic in new[] { 0d, .65 })
     {
      var samples = Enumerable.Range(0,4096).Select(i => (float)(.25*Math.Sin(2*Math.PI*frequency*i/rate)+harmonic*.25*Math.Sin(4*Math.PI*frequency*i/rate))).ToArray();
      var detected = PitchDetector.Detect(samples,rate);
      Require(detected != null && Math.Abs(1200*Math.Log2(detected.Value/frequency)) < 3, $"Pitch {frequency} at {rate}: {detected}");
     }
   Require(PitchDetector.Detect(new float[4096],48000) == null,"Silence rejected");
   Require(PitchDetector.Describe(82.407).Name == "E2", "Low E");
   Require(PitchDetector.Describe(329.628).Name == "E4", "High E");
   Require(Math.Abs(PitchDetector.Describe(432,432).Cents)<.01,"Reference pitch");
   Console.WriteLine("PASS native tuner: 32 guitar/harmonic cases, silence, octave labels and reference pitch");
   string artifacts = Path.GetFullPath("native/artifacts"); Directory.CreateDirectory(artifacts);
   string storePath = Path.Combine(artifacts,"check-settings.json");
   var store = new PracticeStore { Regions = [new PracticeRegion("test","sample","Loop",0,960,75)] };
   store.Save(storePath); Require(PracticeStore.Load(storePath).Regions.Single().Name=="Loop","Region persistence");
      Console.WriteLine("PASS local settings/region persistence");
   string learnPath = Path.Combine(artifacts,"model-learn.json");
   var learning = new LearnStore();
   var firstLesson = LessonCatalog.All[0];
   learning.Complete(firstLesson.Id,firstLesson.Parts[0].Id);
   learning.Complete(firstLesson.Id,firstLesson.Parts[0].Id);
   Require(learning.Completed.Count==1,"Lesson parts are completed idempotently");
   foreach(var part in firstLesson.Parts) learning.Complete(firstLesson.Id,part.Id);
   learning.AddTime(firstLesson.Id,firstLesson.Parts[0].Id,601);
   Require(learning.IsComplete(firstLesson) && learning.Unlocked.ContainsKey("first-lesson") && learning.Unlocked.ContainsKey("ten-minutes"),"Achievements reflect completion and practice time");
   foreach(var part in LessonCatalog.Ladder.Parts) learning.Complete(LessonCatalog.Ladder.Id,part.Id);
   Require(learning.Unlocked.ContainsKey("all-keys"),"Twelve-key achievement requires every key");
   learning.Save(learnPath); var restoredLearning=LearnStore.Load(learnPath);
   Require(restoredLearning.TotalSeconds==601 && restoredLearning.Completed.SetEquals(learning.Completed),"Per-part progress and duration persist");
   restoredLearning.Completed.Clear(); Require(restoredLearning.Unlocked.ContainsKey("first-lesson"),"Earned achievements remain unlocked during review");
   bool badTime=false;try { learning.AddTime(firstLesson.Id,firstLesson.Parts[0].Id,-1); } catch(ArgumentException) {badTime=true;} Require(badTime,"Negative practice time rejected");
   Require(LessonCatalog.All.Select(item=>item.Id).Distinct().Count()==LessonCatalog.All.Length && LessonCatalog.Ladder.Parts.Length==12,"Stable unique lesson identities");
   Console.WriteLine("PASS lesson progress, practice time, achievement rules and persistence");
   string importPath = Path.Combine(AppContext.BaseDirectory,"Fixtures","guitar.musicxml");
   var imported = AlphaTab.Importer.ScoreLoader.LoadScoreFromBytes(File.ReadAllBytes(importPath),new AlphaTab.Settings());
   Console.WriteLine($"IMPORTED title={imported.Title}, tracks={imported.Tracks.Count}, bars={imported.MasterBars.Count}"); Require(imported.Tracks.Count==1 && imported.MasterBars.Count==1 && imported.Title.Replace((char)160,' ')=="Native Guitar Import Check","MusicXML guitar import");
      Console.WriteLine("PASS native MusicXML guitar score import");
   var guitarPro = new AlphaTab.Exporter.Gp7Exporter().Export(imported,new AlphaTab.Settings());
   var roundtrip = AlphaTab.Importer.ScoreLoader.LoadScoreFromBytes(guitarPro,new AlphaTab.Settings());
   Require(roundtrip.Tracks.Count==1 && roundtrip.MasterBars.Count==1,"Guitar Pro import roundtrip");
   Console.WriteLine("PASS native Guitar Pro export/import roundtrip");
   var library = new ScoreLibrary(store,storePath);
   var added = library.Add(importPath);
   Require(added.Path != importPath && File.ReadAllBytes(added.Path).SequenceEqual(File.ReadAllBytes(importPath)), "Library owns an identical local copy");
   Require(library.Add(importPath).Path == added.Path && store.Library.Count == 1, "Duplicate imports reuse library entry");
   var options = new NewScoreOptions("New guitar song", "Test composer", "Drop D guitar", 123, 6, 8, 4, ScoreLibrary.Tunings.Keys.ElementAt(1), 27);
   var created = library.Create(options);
   var blank = AlphaTab.Importer.ScoreLoader.LoadScoreFromBytes(File.ReadAllBytes(created.Path),new AlphaTab.Settings());
   Require(blank.Title == options.Title && blank.Artist == options.Artist && blank.Tracks[0].Name == options.TrackName, "New score metadata survives Guitar Pro save");
   Require(blank.MasterBars.Count == 4 && blank.MasterBars.All(bar => bar.TimeSignatureNumerator == 6 && bar.TimeSignatureDenominator == 8), "New score measures and time signature");
   Require(blank.Tracks[0].Staves[0].Tuning.SequenceEqual(ScoreLibrary.Tunings[options.Tuning].Select(note => (double)note)), "New score Drop D tuning");
   Require(blank.Tracks[0].PlaybackInfo.Program == 27 && blank.Tempo == 123, "New score instrument and tempo");
   Require(blank.Tracks[0].Staves[0].Bars.All(bar => bar.Voices.All(voice => voice.Beats.All(beat => beat.IsRest))), "Blank score contains rests");
   Require(PracticeStore.Load(storePath).Library.Count == 2, "Library persists between launches");
   bool invalidRejected = false;
   try { library.Create(options with { Tempo = 0 }); } catch (ArgumentException) { invalidRejected = true; }
   Require(invalidRejected && store.Library.Count == 2,"Invalid new score leaves library intact");
   Console.WriteLine("PASS library copy, duplicate detection, persistence, new Guitar Pro score settings and validation");
   var editDocument = new ScoreDocument(library.Read(created));
   Require(!editDocument.Dirty,"Editor begins clean");
   editDocument.SplitRest(0,0,0,0);
   Require(editDocument.Voice(0,0,0,0).Beats.Count == 6,"6/8 full rest splits into six eighth beats");
   editDocument.SetBeat(0,0,0,0,0,8,0,[3,2,0,-1,-1,-1],true,"Opening chord");
   var editedBeat = editDocument.Voice(0,0,0,0).Beats[0];
   Require(editedBeat.Notes.Count == 3 && editedBeat.Notes.Any(note => note.String == 6 && note.Fret == 3 && note.RealValue == 67) && editedBeat.IsPalmMute && editedBeat.Text == "Opening chord","High-E string produces G4; chord frets, annotation and palm mute survive edit");
   Require(editDocument.Dirty && editDocument.CanUndo,"Changes are undoable"); editDocument.Undo(); Require(editDocument.Voice(0,0,0,0).Beats[0].IsRest,"Undo restores rest"); editDocument.Redo(); Require(editDocument.Voice(0,0,0,0).Beats[0].Notes.Count == 3,"Redo restores chord");
   editDocument.SetBeat(0,0,0,0,0,16,1,[-1,-1,-1,-1,-1,-1],false,""); Require(editDocument.Voice(0,0,0,0).Beats[0].IsRest && editDocument.Voice(0,0,0,0).Beats[0].Dots==1,"Dotted rests can replace a chord"); editDocument.Undo();
   editDocument.SetDetails("Edited guitar song","Local artist",142); editDocument.SetMeasure(0,6,8,2,false);
   Require(editDocument.Score.Tempo == 142 && ScoreInfo.Keys(editDocument.Score).Contains("D major"),"Tempo and key edits");
   editDocument.AppendMeasure(); Require(editDocument.Score.MasterBars.Count == 5,"Append measure across tracks"); editDocument.RemoveMeasure(1); Require(editDocument.Score.MasterBars.Count == 4 && editDocument.Score.Tracks[0].Staves[0].Bars.Count == 4,"Delete middle measure maintains structure");
   editDocument.ValidateRhythm();
   editDocument.AddBeat(0,0,0,0,0); bool overflowRejected=false; try { editDocument.ValidateRhythm(); } catch(ArgumentException) { overflowRejected=true; } Require(overflowRejected,"Overfull measures cannot be saved"); editDocument.Undo();
   created = library.SaveScore(created,editDocument.Score); editDocument.MarkSaved(); Require(!editDocument.Dirty,"Saving clears dirty status");
   created = library.SaveNotes(created,"Practice slowly. Use a clean tone.");
   Require(PracticeStore.Load(storePath).Library.Single(item=>item.Path==created.Path).Notes.Contains("Practice slowly"),"User notes persist");
   var exportPath = Path.Combine(artifacts,"edited-score.gp"); library.Export(editDocument.Score,exportPath);
   var exportScore = AlphaTab.Importer.ScoreLoader.LoadScoreFromBytes(File.ReadAllBytes(exportPath),new AlphaTab.Settings());
   Require(exportScore.Title == "Edited guitar song" && exportScore.Tracks[0].Staves[0].Bars[0].Voices[0].Beats[0].Notes.Count == 3,"Edited Guitar Pro export reopens");
   bool managedExportRejected=false; try { library.Export(exportScore,created.Path); } catch(ArgumentException) { managedExportRejected=true; } Require(managedExportRejected,"Exports cannot overwrite managed library data");
   var multiXml = System.Xml.Linq.XDocument.Load(importPath);
   var partDefinition = new System.Xml.Linq.XElement(multiXml.Root!.Element("part-list")!.Element("score-part")!); partDefinition.SetAttributeValue("id","P2"); partDefinition.Element("part-name")!.Value="Second guitar"; multiXml.Root.Element("part-list")!.Add(partDefinition);
   var extraPart = new System.Xml.Linq.XElement(multiXml.Root.Element("part")!); extraPart.SetAttributeValue("id","P2"); multiXml.Root.Add(extraPart);
   var multiScore = AlphaTab.Importer.ScoreLoader.LoadScoreFromBytes(System.Text.Encoding.UTF8.GetBytes(multiXml.ToString()),new AlphaTab.Settings());
   var multi = new ScoreDocument(multiScore); multi.AppendMeasure(); multi.AppendMeasure(); multi.RemoveMeasure(1);
   Require(multi.Score.Tracks.Count==2 && multi.Score.Tracks.All(track=>track.Staves[0].Bars.Count==2),"Measure changes preserve multiple tracks");
   var unchangedNotes = multi.Score.Tracks[1].Staves[0].Bars[0].Voices[0].Beats[0].Notes.Select(note=>(note.String,note.Fret)).ToArray();
   multi.SetBeat(0,0,0,0,0,4,0,[7,-1,-1,-1,-1,-1],false,""); Require(multi.Score.Tracks[1].Staves[0].Bars[0].Voices[0].Beats[0].Notes.Select(note=>(note.String,note.Fret)).SequenceEqual(unchangedNotes),"Editing one track preserves the other");
   var converted = library.SaveScore(added,library.Read(added)); Require(Path.GetExtension(converted.Path)==".gp" && File.Exists(importPath),"Editing an imported score saves a separate managed GP file");
   library.Delete(converted); Require(store.Library.Count == 1 && File.Exists(importPath) && !File.Exists(converted.Path) && !File.Exists(added.Path),"Delete removes library copies and preserves imported source");
   added = library.Add(importPath);
   Console.WriteLine("PASS editor chords, rests, durations, undo/redo, tempo/key, measures, local save, notes, export and safe deletion");
   foreach (var device in AudioInput.Devices(DataFlow.Capture)) Console.WriteLine($"INPUT {device.Name} [{device.Id}]");
   foreach (var device in AudioInput.Devices(DataFlow.Render)) Console.WriteLine($"OUTPUT {device.Name} [{device.Id}]");
   if (args.Contains("--capture"))
   {
    using var capture = new AudioInput();
    var guitar = AudioInput.Devices(DataFlow.Capture).FirstOrDefault(d=>d.Name.Contains("Head",StringComparison.OrdinalIgnoreCase)||d.Name.Contains("Gigboard",StringComparison.OrdinalIgnoreCase));
    capture.Start(guitar?.Id); Thread.Sleep(800); Console.WriteLine($"PASS WASAPI input capture: {capture.ChannelCount} channels, {capture.Read().Decibels:0.0} dBFS"); capture.Stop(); Require(!capture.Running,"Capture released");
   }
   var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
   store.InputDevice = AudioInput.Devices(DataFlow.Capture).FirstOrDefault(device => device.Name.Contains("HeadRush", StringComparison.OrdinalIgnoreCase))?.Id; var window = new NativeWindow(store,storePath) { ShowInTaskbar = false };
   var deadline = DateTime.UtcNow.AddSeconds(40); int phase = 0; bool failed = false; int guitarVersion = 0;
   var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
   app.DispatcherUnhandledException += (_,eventArgs) => { Console.Error.WriteLine(eventArgs.Exception); eventArgs.Handled=true; failed=true; timer.Stop(); window.Close(); Require(!window.GuitarConnected, "Closing app releases guitar capture"); Console.WriteLine("PASS app close releases guitar capture"); app.Shutdown(); };
   timer.Tick += async (_,_) =>
   {
    try
    {
     if(DateTime.UtcNow>deadline) throw new Exception($"Native startup timeout: {window.CurrentStatus}");
     if(!window.ScoreReady) return;
     if(phase==0)
     {
      Require(window.GuitarConnected, "Guitar capture starts automatically"); guitarVersion = window.GuitarConnectionVersion; timer.Stop(); phase=1; Console.WriteLine("PASS native score render + SoundFont + playback engine ready");
      Snapshot(window,Path.Combine(artifacts,"native-practice.png"));
      foreach(var device in await window.PlaybackDevices()) Console.WriteLine($"PLAYBACK {device}");
            var playbackDevice = AudioInput.Devices(DataFlow.Render).FirstOrDefault(d => d.Name.Contains("HeadRush",StringComparison.OrdinalIgnoreCase));
      if (playbackDevice != null) { await window.SetPlaybackOutput(playbackDevice.Id); var actual = await window.PracticeApi.GetOutputDevice(); Require(actual != null && NativeWindow.OutputIdsMatch(actual.DeviceId,playbackDevice.Id), "Playback output routed"); Console.WriteLine("PASS native playback routed to HeadRush output"); }
      window.PracticeApi.PlaybackSpeed = .5; window.PracticeApi.IsLooping = true; window.PracticeApi.PlaybackRange = new AlphaTab.Synth.PlaybackRange { StartTick = 0, EndTick = 960 };
      Require(window.PracticeApi.PlaybackSpeed == .5 && window.PracticeApi.PlaybackRange.EndTick == 960, "Practice transport settings");
      await Task.Delay(300); window.PracticeApi.Play(); await Task.Delay(300); Require(window.PracticeApi.PlayerState == AlphaTab.Synth.PlayerState.Playing,"Playback starts"); window.PracticeApi.Stop();
      Console.WriteLine("PASS native playback, speed, loop and region controls");
      window.ShowSettings(true); timer.Start();
     }
     else if(phase==1)
     {
      Require(window.GuitarConnected && window.GuitarConnectionVersion == guitarVersion, "Settings navigation preserves capture"); phase=2; Snapshot(window,Path.Combine(artifacts,"native-settings.png")); Console.WriteLine("PASS native settings screen and audio device selectors");
      Descendants(window).OfType<System.Windows.Controls.Expander>().Single(expander=>expander.Header is System.Windows.Controls.Button).IsExpanded = true; phase=2;
     }
     else if(phase==2)
     {
      Require(window.GuitarConnected && window.GuitarConnectionVersion == guitarVersion,"Library navigation preserves capture"); Snapshot(window,Path.Combine(artifacts,"native-library.png")); window.ShowNewScore(); phase=3;
     }
     else if(phase==3)
     {
      Require(window.GuitarConnected && window.GuitarConnectionVersion == guitarVersion,"New score navigation preserves capture"); Snapshot(window,Path.Combine(artifacts,"native-new-score.png"));
      Descendants(window).OfType<System.Windows.Controls.Button>().Single(button => Equals(button.Content,"Create score")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
      var editor = Descendants(window).OfType<ScoreEditorPage>().Single();
      Require(store.Library.Count == 3 && editor.Document?.Score.Title == "Untitled score" && editor.Visibility == Visibility.Visible, "Create score form opens new score in editor"); phase=4;
     }
     else if(phase==4)
     {
      var editor = Descendants(window).OfType<ScoreEditorPage>().Single();
      if (!editor.NotationReady) return;
      Require(window.GuitarConnected && window.GuitarConnectionVersion == guitarVersion,"Editing preserves guitar capture");
      var inspectorTabs = Descendants(editor).OfType<System.Windows.Controls.TabControl>().Single(); inspectorTabs.SelectedIndex=1; window.UpdateLayout();
      Descendants(editor).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"Split full-measure rest")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
      inspectorTabs.SelectedIndex=0; window.UpdateLayout();
      var fretBoxes = Descendants(editor).OfType<System.Windows.Controls.TextBox>().Where(box => box.ToolTip is string text && text.StartsWith("String ")).ToList();
      Require(fretBoxes.Count == 6,"Editor exposes six guitar strings"); fretBoxes[0].Text="5"; fretBoxes[1].Text="3";
      Descendants(editor).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"Apply beat")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
      Require(editor.Document!.Score.Tracks[0].Staves[0].Bars[0].Voices[0].Beats[0].Notes.Count==2,"Editor UI applies chord fret entry");
      Require(editor.Save(),"Editor saves through UI"); phase=5;
     }
     else if(phase==5)
     {
      var editor = Descendants(window).OfType<ScoreEditorPage>().Single(); if(!editor.NotationReady) return;
      Require(editor.NotationApi!.Score!.Tracks[0].Staves[0].Bars[0].Voices[0].Beats[0].Notes.Count==2,"Native notation preview displays edited chord");
      Snapshot(window,Path.Combine(artifacts,"native-editor.png"));
      timer.Stop();
      Descendants(editor).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"▶ Play")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
      await Task.Delay(1500); Require(window.PracticeApi.PlayerState==AlphaTab.Synth.PlayerState.Playing,"Playback starts inside score workspace");
      Descendants(editor).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"■ Stop")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
      var infoWindow = new ScoreInfoWindow(window,library,created);
      infoWindow.Loaded += (_,_) =>
      {
       var notesBox = Descendants(infoWindow).OfType<System.Windows.Controls.TextBox>().Single(box=>box.AcceptsReturn); notesBox.Text="Saved through the Info screen.";
       Snapshot(infoWindow,Path.Combine(artifacts,"native-score-info.png"));
       Descendants(infoWindow).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"Save info")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
      };
      Require(infoWindow.ShowDialog()==true && PracticeStore.Load(storePath).Library.Single(item=>item.Path==created.Path).Notes=="Saved through the Info screen.","Info UI persists user notes");
      window.ShowLibrary(); Snapshot(window,Path.Combine(artifacts,"native-library.png"));
      Descendants(window).OfType<System.Windows.Controls.Button>().Where(button=>Equals(button.Content,"Edit")).Last().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); phase=6; timer.Start();
     }
     else if(phase==6)
     {
      var editor = Descendants(window).OfType<ScoreEditorPage>().Single(); if(!editor.NotationReady) return;
      Require(editor.Entry?.Path==created.Path && editor.NotationApi?.Score?.Title=="Edited guitar song","Library Edit opens the shared notation workspace");
      Snapshot(window,Path.Combine(artifacts,"native-library-editor.png"));
      Descendants(editor).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"Next beat")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
      Require(!editor.Document!.Dirty,"Beat navigation does not alter saved score");
      window.LoadScore(created.Path); Require(window.PracticeApi.Score?.Title == "Edited guitar song","Created library score opens for practice");
      window.LoadScore(importPath); Require(window.PracticeApi.Score?.Title.Replace((char)160,' ') == "Native Guitar Import Check", "Native open-score workflow"); Console.WriteLine("PASS native library, new score and open-score workflows"); Require(window.GuitarConnected && window.GuitarConnectionVersion == guitarVersion, "Practice navigation and score imports preserve capture"); Console.WriteLine("PASS guitar connection remains open across screens and imports");
      timer.Stop(); window.ShowLearn(); Snapshot(window,Path.Combine(artifacts,"native-learn-home.png"));
      foreach(var lesson in LessonCatalog.All)
       foreach(var part in lesson.Parts.Where(part=>part.Tab!=null))
       {
        window.PracticeApi.Tex(LearnPage.ScoreText(lesson,part),null!);
        Require(window.PracticeApi.Score?.Title==lesson.Title && window.PracticeApi.Score.Tracks[0].Staves[0].Bars.Count>0,"Lesson exercise parses: "+lesson.Id+"/"+part.Id);
        if(lesson.Id==LessonCatalog.Ladder.Id)
        {
         int root=int.Parse(part.Id.Split('-')[1]);
         var notes=window.PracticeApi.Score!.Tracks[0].Staves[0].Bars.SelectMany(bar=>bar.Voices).SelectMany(voice=>voice.Beats).SelectMany(beat=>beat.Notes).ToArray();
         Require(notes.Length==23 && notes.All(note=>new[]{0,3,5,7,10}.Contains((int)((note.RealValue-40-root)%12+12)%12)),"Ladder pitches and turnaround: "+part.Id);
        }
       }
      var learnPage=Descendants(window).OfType<LearnPage>().Single();
      learnPage.OpenLesson(LessonCatalog.Ladder,"fret-1"); await Task.Delay(700); Snapshot(window,Path.Combine(artifacts,"native-learn-lesson.png"));
      learnPage.Store.Completed.Remove(LearnStore.Key(LessonCatalog.Ladder.Id,"fret-1")); learnPage.OpenLesson(LessonCatalog.Ladder,"fret-1");
      Descendants(learnPage).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"▶ Listen to example")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
      await Task.Delay(1500); Require(window.PracticeApi.PlayerState==AlphaTab.Synth.PlayerState.Playing,"Learn example plays through native engine");
      Descendants(learnPage).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"Start practice timer")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
      await Task.Delay(1200);
      Descendants(learnPage).OfType<System.Windows.Controls.Button>().Single(button=>Equals(button.Content,"Mark this part complete")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
      Require(learnPage.Store.Completed.Contains(LearnStore.Key(LessonCatalog.Ladder.Id,"fret-1")),"Completion button records the selected lesson part");
      window.ShowLearn(true);
      Require(!learnPage.IsPracticing && learnPage.Store.TotalSeconds>=1,"Profile navigation pauses and records lesson practice");
      Snapshot(window,Path.Combine(artifacts,"native-profile.png"));
      for(int wait=0;wait<20 && window.PracticeApi.PlayerState==AlphaTab.Synth.PlayerState.Playing;wait++) await Task.Delay(50);
      Require(window.PracticeApi.PlayerState!=AlphaTab.Synth.PlayerState.Playing,"Profile stops lesson playback");
      window.ShowSettings(false); Require(window.PracticeApi.Score?.Title==LessonCatalog.Ladder.Title,"Leaving Learn restores the preceding practice score");
      Require(window.GuitarConnected && window.GuitarConnectionVersion==guitarVersion,"Learn and Profile preserve guitar capture");
      Console.WriteLine("PASS Learn navigation, Profile, practice timing and continuous capture");
      timer.Stop(); window.Close(); Require(!window.GuitarConnected, "Closing app releases guitar capture"); Console.WriteLine("PASS app close releases guitar capture"); app.Shutdown();
     }
    }
    catch(Exception error) { Console.Error.WriteLine(error); failed=true;timer.Stop();window.Close();app.Shutdown(); }
   };
   window.Show(); timer.Start(); app.Run(); return failed?1:0;
  }
  catch(Exception error) { Console.Error.WriteLine(error); return 1; }
 }
 private static void Require(bool valid,string detail) { if(!valid) throw new Exception(detail); }
 private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
 {
  for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
  {
   var child = System.Windows.Media.VisualTreeHelper.GetChild(parent,index); yield return child;
   foreach (var descendant in Descendants(child)) yield return descendant;
  }
 }
 private static void Snapshot(Window window,string path)
 {
  window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(window);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
 }
}
