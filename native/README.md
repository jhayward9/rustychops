# Rustychops native Windows application

The current application is written in C# and WPF. Score rendering uses the native `AlphaTab.Windows` control and AlphaSkia; playback uses alphaTab's native NAudio output. Input capture uses Windows WASAPI through NAudio. No WebView, browser permissions, Node process, or HTTP server is required.

## Develop and package

From the repository root, with the .NET 10 SDK installed:

```powershell
npm run native:dev
npm run native:check
npm run native:build
```

The commands also work directly with `dotnet run --project native/Rustychops`, `dotnet run --project native/Rustychops.Checks -c Release`, and `dotnet publish native/Rustychops -c Release -r win-x64 --self-contained true -o artifacts/windows`.

The portable self-contained build is at `artifacts/windows/Rustychops.exe`. Keep the other files in that directory alongside it. A separately installed .NET runtime is not required for that build. The application runs without administrator privileges.

## Guitar setup

Click **Configure Guitar & Audio Settings**. Select a HeadRush Gigboard input and choose the channel carrying the guitar signal. **Line 1/2**, **Line 3/4**, and **Multichannel** endpoints may carry different signals depending on the Gigboard configuration. The input meter shows RMS level in dBFS and reports clipping. Adjust input gain on the interface. Choose a playback output for score playback.

The tuner analyzes one sustained note at a time. Use a clean sound, mute other strings, and pluck once. It displays note, frequency, and cents flat/sharp, with a five-cent in-tune window. A4 references are 432, 440, and 442 Hz. It covers 60–1200 Hz. Audio is processed locally and is not monitored back to speakers. Capture starts automatically and remains connected across screens and score imports until the app closes. Unavailable inputs are retried automatically.

Audio devices are checked throughout the app session; **Refresh devices** also refreshes the input and output selectors. Guitar input is captured directly through Windows audio endpoints.

## Persistence and compatibility

Settings, recent score paths, and practice regions are saved to `%LOCALAPPDATA%/Rustychops/settings.json`. Recent scores can be reopened directly. Saved regions use full score paths. The original browser prototype remains in the repository as reference; its localStorage data is separate and is not imported automatically.

The native app preserves score import, tablature rendering, synthesized playback, speed, metronome, count-in, loop selection, track mute/solo/volume/display, recent scores, and named practice regions. Progress retains its disabled “Soon” state.

## Score library

The sidebar **Library** dropdown contains **Open score**, **Add score**, and **New score**. Opening reads a score directly. Adding accepts multiple Guitar Pro, MusicXML, or Capella files, validates them, and copies them into `%LOCALAPPDATA%/Rustychops/Library`. Identical files are detected by SHA-256 and skipped. The library collection persists in settings, independently of recent file paths.

New score groups song details, composition settings, and guitar track settings, inspired by [TuxGuitar's track setup](https://www.tuxguitar.app/files/devel/desktop/help/editing_track.html). It creates a blank Guitar Pro file with full-measure rests, chosen tempo/time signature, 1–128 measures, a guitar instrument, and standard, Drop D, E-flat standard, or D standard tuning. The saved score opens in the editor. Library navigation and editing keep guitar capture connected.

Each library score has **View**, **Edit**, **Info**, and **Delete** actions. Info derives all time signatures and keys from the score and saves user notes in settings. Deleting requires confirmation and removes only validated managed-library files, including the retained managed import copy after editing; external original imports and exports remain intact.

## Score editor and export

Creation and Library Edit share a score-first layout inspired by the [Guitar Pro screenshot gallery](https://guitar-pro.uptodown.com/windows). Creation previews notation/tablature beside compact setup tabs. Editing renders native notation/tablature in the center, with a Note/Measure/Song inspector on the right and a clickable track/measure overview below. Clicking a rendered beat updates the inspector; Previous/Next and left/right arrows navigate beats. Inspector visibility can be toggled. The editor's render-only alphaTab instance has no separate audio engine; Play/Stop uses the existing native playback engine to audition a copy of current edits at their score tempo. Returning to Practice restores its speed, loop, and count-in controls. Preview copies keep display changes separate from the saved model.

The editor selects existing tracks/staves/voices and edits frets/chords, rests, durations (whole through 64th), up to two dots, palm mute, and beat text. UI strings use conventional high-to-low numbering; alphaTab's model numbers low-to-high. The editor also changes title/artist/initial tempo, per-measure time/key signatures, and adds/deletes beats and measures. Measure changes affect every track to keep the score aligned. Split a full-measure rest into time-signature beats before composing. Unsupported imported durations can be retained or replaced by an offered duration. Non-stringed and percussion tracks are retained, but fret entry applies to stringed tracks.

Undo/redo snapshots use Guitar Pro round trips to rebuild timing and note links. Saving/exporting rejects overfull measures; shorter measures are allowed while composing. Unsaved edits prompt on navigation/close. Save writes a managed `.gp` using temporary-file replacement and persists the updated library entry. Editing an imported format creates a separate managed GP copy; its original managed import copy is retained until library deletion.

Export writes a separate Guitar Pro 7 `.gp` file supported by both Guitar Pro and [TuxGuitar](https://www.tuxguitar.app/files/devel/desktop/help/file_formats.html); it does not produce native `.tg` files. Exports cannot replace managed library files. This editor supports basic guitar composition; it does not yet author advanced notation such as bends, ties, tuplets, or track creation. Imported features depend on alphaTab's Guitar Pro export support.

## Validation and dependency notes

The native checks cover synthetic guitar pitches and harmonics at 44.1/48 kHz, silence rejection, tuning references, JSON persistence, library copying and duplicate detection, new score export/import and form creation, editor chord/string pitches and dotted rests, undo/redo, multi-track measure changes, rhythm validation, info notes, safe deletion and edited exports, native score rendering, SoundFont loading, playback, output routing, and settings/library layouts. Add `-- --capture` to `dotnet run --project native/Rustychops.Checks -c Release` for a short real-device WASAPI capture check. Layout previews are generated under `native/artifacts/`.

alphaTab 1.8.4 has two WPF lifecycle issues handled by the host: playback/cursors start after the canvas is attached, and shutdown releases audio output directly rather than calling the worker cancellation path that throws an unhandled exception. Background synthesis workers end with the application process. Score playback currently uses alphaTab's native DirectSound output; audio input uses shared-mode WASAPI. ASIO and exclusive-mode low-latency input are not implemented.

Third-party components: AlphaTab/AlphaTab.Windows (MPL-2.0), NAudio (MIT), AlphaSkia (BSD-3-Clause), and the SONiVOX SoundFont with its supplied license. See the bundled asset notices and the [native alphaTab installation documentation](https://www.alphatab.net/docs/getting-started/installation-net).
