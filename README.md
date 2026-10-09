# Rustychops

Rustychops is a native Windows guitar practice workstation written in C# and WPF. It imports Guitar Pro and MusicXML scores, renders tablature, synthesizes playback, supports practice loops and track controls, and provides native audio device selection, continuous guitar capture, a guitar input meter, and a tuner.

## Run the Windows application

Double-click `Rustychops.exe` in the repository root to launch the current native build. This stable iteration launcher opens `artifacts/windows/Rustychops.exe`, or brings the existing app window forward. Keep it beside the `artifacts` folder. Rebuilding the application updates what the launcher opens. Recreate the launcher with `npm run native:launcher` if needed. A proper MSI installer is planned for the finished application.

Rustychops opens maximized with the standard Windows title bar and minimize, maximize/restore, and close buttons. Restored window dimensions respect the available desktop work area and Windows display scaling.

The portable build is at `artifacts/windows/Rustychops.exe`. Run that executable and keep the other files in the same folder. It includes the .NET runtime and does not need a browser, Node.js, or a local server.

For development, install the .NET 10 SDK and run:

```powershell
dotnet run --project native/Rustychops
```

To validate and package:

```powershell
dotnet run --project native/Rustychops.Checks -c Release
dotnet publish native/Rustychops -c Release -r win-x64 --self-contained true -o artifacts/windows
```

Equivalent npm shortcuts are `npm run native:dev`, `npm run native:check`, and `npm run native:build`.

## Configure your guitar

The app starts listening automatically and keeps your guitar connected across screens until you close it. Open **Configure Guitar & Audio Settings** from the sidebar to select your USB audio interface and the channel carrying your guitar signal. The live meter shows input level and clipping. Pluck one clean string at a time to use the tuner. Select a playback output for score playback.

The native build detects Windows audio endpoints directly, without browser permissions. On this PC it detected the HeadRush Gigboard inputs and outputs and successfully captured audio.

## Learn and Profile

Open **Learn** from the sidebar to choose **Go at your own pace** or **Guided rock & metal lessons**. The first release includes five lessons: guitar setup and first notes, your first rock riff, metal rhythm foundations, minor pentatonic box 1, and the twelve-key pentatonic ladder from root fret 1 through 12. Each lesson contains individually tracked parts, instructions, native tablature and synthesized examples with adjustable tempo and looping.

Use **Start practice timer** while playing and pause for breaks. Changing parts or screens, minimizing the app, or closing it pauses and saves the session. **Mark this part complete** records your own readiness assessment; the tuner does not automatically score these exercises. Return with **Continue** to your last lesson part.

**Profile** shows your local display name, total lesson practice time, time and completion for every lesson part, and six achievements for practice milestones, lesson completion and all twelve pentatonic keys. Earned achievements remain unlocked when marking a completed part for review. Learning data is saved separately in `%LOCALAPPDATA%/Rustychops/learn.json`, without an account or uploads. The guided course currently covers foundations; advanced modules are planned.

## Practice features

- Guitar Pro and MusicXML import, with native alphaTab notation and tablature rendering
- Synthesized playback, cursor, 25–150% speed, metronome, and count-in
- Interactive playback-range selection and looping
- Track mute, solo, volume, and display controls
- Named practice regions and recent scores saved locally
- Expandable Library menu with Open score, Add score, and New score
- Local score library with duplicate detection and a blank Guitar Pro score setup page
- Library View, Edit, Info, and Delete actions; user notes saved locally
- Guitar tablature editor with chords, rests, durations, measure settings, and undo/redo
- Guitar Pro `.gp` export for Guitar Pro and TuxGuitar
- Native input/output device selection and channel selection
- Continuous guitar capture, input volume meter, and chromatic guitar tuner with 432/440/442 Hz reference

Expand **Library** in the sidebar. **Open score** opens a file directly, **Add score** copies selected files into your local library, and **New score** creates a blank `.gp` score and opens the editor. Click the Library heading to browse saved scores. Every score has **View**, **Edit**, **Info**, and **Delete** buttons. Info lists time signatures, keys, tempo, and a section for your own notes. Delete asks for confirmation and removes managed library copies; original imports and exported files are retained. Progress remains marked “Soon”.

**New score** and **Edit** use a score workspace inspired by the [Guitar Pro screenshots](https://guitar-pro.uptodown.com/windows): a white notation/tablature page, compact toolbar, side inspector, and track/measure overview. New score shows a blank-score preview with Song, Measure, and Track setup tabs; **Update preview** applies setup changes, and **Create score** opens the editor.

In **Edit**, click a beat on the score or use Previous/Next beat. Use the bottom overview to change tracks and measures. The **Note** inspector edits high-to-low string frets (blank = silent, 0 = open), durations/dots, palm mute, and annotation; **Apply beat** updates the score. The **Measure** tab changes time/key signatures and adds/deletes beats/measures, including **Split full-measure rest**. **Song** edits title/artist/tempo and selects staff/voice. The toolbar provides undo/redo, local **Save**, **Save & view**, and **Play/Stop** for auditioning edits without leaving the workspace. **Export** saves a separate Guitar Pro 7 `.gp` file, which [TuxGuitar also opens](https://www.tuxguitar.app/files/devel/desktop/help/file_formats.html). Native `.tg` export is not included.

Settings and practice metadata live in `%LOCALAPPDATA%/Rustychops/settings.json`; library files live beside it in the `Library` folder. Scores and audio stay on your PC.

See [native implementation and validation notes](native/README.md) for details, device setup, dependency workarounds, and current limitations. Browser localStorage is separate from native settings and is not migrated automatically.

## Repository layout

`native/Rustychops/` is the current application; `native/Rustychops.Checks/` contains native checks. `artifacts/windows/` contains the self-contained portable build. The original React/Vite frontend and Tauri wrapper remain as legacy reference sources; their existing web commands still work but are not required by the native application.

## Third-party components

Native score rendering and synthesis use AlphaTab.Windows 1.8.4 (MPL-2.0), AlphaSkia (BSD-3-Clause), and NAudio (MIT). The bundled SONiVOX SoundFont license is included in the application assets. See [third-party notices](native/Rustychops/THIRD-PARTY-NOTICES.md).

## Clean-room scope

Rustychops does not contain code, branding, assets, exercise content, or proprietary file formats from ShredForge. Users are responsible for importing scores they have the right to use.
