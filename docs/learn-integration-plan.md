# Virtual guitar instruction: integration plan

October 4, 2026. Planning deliverable only. Builds on [scale and curriculum research](learn-research.md) and inspection of the current native C#/WPF application.

## Product direction

Add **Learn** to the main navigation. Make it a complete practice-based instruction program, from never having held a guitar through advanced rock/metal rhythm and virtuoso-style lead playing. Lessons should teach music, listening, control and expression alongside technique. Progress at the user's pace; no calendar deadlines or promise that completing videos makes someone a virtuoso.

Two entry choices share one content catalog and practice workspace:

1. **Go at your own pace** — “Choose a skill, scale, riff or practice routine.” Browse freely, save favorites, create a practice queue and revisit any lesson.
2. **Guided rock & metal lessons** — “Build your skills step by step, at your pace.” Choose Rock, Metal, or both, then start from first contact or a suitable placement point.

The guided curriculum is limited to rock and metal for now. Shared theory and technique can draw on other traditions without presenting additional guided genre programs.

## User journey and screens

### Learn home: “What do you want to learn?”

Show the two choices above prominently. Returning learners also see **Continue learning**, their current topic and their last exercise. Do not force onboarding before browsing.

Optional setup after choosing Guided:

- Experience: “I've never played”, “I know a few chords/riffs”, “I play songs”, “I want advanced technique”.
- Goal: rhythm, lead, or both; Rock/Metal preference.
- Guitar: string count, tuning, available frets, handedness and tremolo availability. Reuse existing supported tuning presets initially.
- Session preference: short, medium, or custom; it changes a suggested practice session, not eligibility.

Offer a brief placement activity and an explicit skip. Recommend prerequisites when needed, but permit previewing advanced lessons. Keep “First time holding a guitar” immediately accessible.

### Self-paced catalog

Browse First steps, Rhythm & riffs, Scales & fretboard, Picking, Legato & tapping, Bends & expression, Arpeggios & sweeps, Theory & ear training, and Practice routines. Filter by level, tuning and equipment. Search names, aliases, techniques and intervals. Topic pages show prerequisites, useful next lessons and external reference material.

Avoid using artist names as the primary taxonomy. “String skipping” and “Neoclassical sequences” are useful even when the learner does not know an artist. Artist-inspired study collections can link to those skills.

### Guided path

Show **Your next lesson**, a compact module map and review suggestions. Each module mixes explanation, technique, a riff/phrase and musical application. Let learners repeat, revisit or change paths without losing progress. Advanced rhythm must receive as much deliberate coverage as lead.

### Lesson workspace

1. A concrete outcome: “Play an even palm-muted eighth-note riff.”
2. Concise explanation, fretboard diagram and an original demonstration when available.
3. Native notation/tab with fingerings, picking directions and technique markings.
4. Listen → play slowly → isolate a phrase → play in context.
5. Tempo, subdivisions, count-in, loop and repetitions; a suggested session length.
6. A readiness checklist and a personal note: “Needs work”, “Comfortable”, “Ready to move on”.
7. Next lesson and optional references, with provider links and any access requirement.

A lesson must remain useful offline without a third-party video. Launch external references in the browser initially. Build media playback only for original/licensed assets and only when enough authored media exists to justify it.

### Practice and progress

Track lessons visited, sessions practiced, settings used and user-assessed readiness separately. Show tempo together with subdivision and exercise identity; 120 BPM eighth notes is not the same as 120 BPM sixteenths. Track consistency and repertoire as well as speed. Offer local progress inside Learn first; a full replacement for the disabled global Progress page is a later integration decision.

## Curriculum from first contact to advanced application

| Module | Shared instruction | Rock/metal application and outcome |
| --- | --- | --- |
| 0. Meet your guitar | Parts, setup, tuning, string/fret numbers, pick grip options, fretting, simple tab and pulse | First clear note and a two-note riff |
| 1. Play your first music | Open chords, basic rests, changes, rhythm and muting | A complete simple rock riff/chord piece |
| 2. Rhythm foundation | Power chords, palm muting, subdivisions, alternate/down picking, accents | Tight rock riffs; introductory metal gallops and stopped notes |
| 3. Lead foundation | Pentatonic box, root notes, slides, hammer-ons/pull-offs, bends, vibrato | A musical solo phrase; transpose the box into other keys |
| 4. Know the neck | Major/natural minor, five pentatonic positions, intervals, triads and ear work | Connect positions and choose notes over a backing progression |
| 5. Stronger rhythm | Syncopation, pedal riffs, gallops, tremolo picking, string changes, controlled stamina | Complete rock/metal rhythm etudes; alternate tunings introduced explicitly |
| 6. Efficient lead technique | Picking motions, synchronization, string crossing/skipping, legato and basic tapping | Clean multi-string phrases using a technique suited to the phrase |
| 7. Arpeggios and harmony | Triads, seventh chords, small sweeps, economy picking and modes in context | Short sweep phrases and chord-aware soloing |
| 8A. Expressive rock | Wide intervals, advanced legato/tapping, harmonics, articulation and optional whammy work | Original Vai-inspired expressive etude |
| 8B. Neoclassical metal | Harmonic minor, Phrygian dominant, diminished sounds, pedal tones and sequences | Original Malmsteen/Batio-inspired etude |
| 8C. Modern metal | Precise rhythmic articulation, dissonant vocabulary and extended arpeggios | Original Heafy/Loomis-inspired rhythm and lead studies |
| 8D. Melodic individuality | Unusual phrase shapes, bends, tension/resolution and selected additional scale colors | Original Friedman-inspired melodic study |
| 9. Performance and creation | Full arrangements, transcription, improvisation, composition and self-review | A complete demanding performance and an original piece/solo |

Shared modules are prerequisites by skill rather than rigid grade numbers. Learners can focus on rhythm or lead while retaining essential rhythm, hearing and muting work. Seven-string studies require later explicit instrument support; do not convert them silently to six strings.

### Practice design informed by classic videos and modern instruction

Keep the classic demonstration → explanation → exercise format, then make the exercises interactive. Break long videos' subject matter into short lessons with clear prerequisites and immediate musical application.

Teach alternate, economy, sweep and legato strategies as options with different requirements. Use Cracking the Code's current escape-motion vocabulary when discussing string changes: upstroke escape (USX), downstroke escape (DSX), double escape (DBX), and helper motions. Pick angle alone does not identify the motion. Provide simple wording first and optional mechanics detail. Avoid prescribing one grip or joint motion for everyone.

For speed development, distinguish learning notes slowly from finding a motion that works at speed. Include brief comfortable motion experiments and small bursts once fundamentals are established, then integrate clean phrases. “Increase the metronome forever” is insufficient instruction. Stop/reduce intensity if playing causes pain; do not transplant marathon workout durations into beginner sessions.

The app cannot infer picking motion from its current audio capture. First-release technique feedback is explanation and self-review, not an automated diagnosis.

### Featured routine: twelve-key pentatonic ladder

Default: standard tuning, minor pentatonic box 1, low-E root fret 1 → 12. At each root, ascend and descend; then shift one fret. Display F, F#/Gb, G, Ab, A, Bb, B, C, Db, D, Eb, E. Root fret 12 requires the pattern to extend to fret 15.

Provide starting/ending root frets, repetitions per key, ascending/descending direction, turnaround-note repeat, rest/count-in between keys and optional reverse journey. Label this **Change key at each fret**. A separate routine, **Connect positions in one key**, holds the tonic fixed.

Implement key changes as a defined score sequence initially so playback and the displayed exercise stay aligned. Persist the current step for resume. Later add circle-of-fourths and shuffled-key orders. The scale catalog stores formulas and aliases independently of fingering patterns and tuning.

## Reference library: VHS-era, DVD-era and contemporary materials

The rows below are references for curriculum design, not a claim that we watched every video or verified every chapter. Proposed lesson mappings are our synthesis. VHS-era titles often have DVD or online reissues; later materials are identified separately. No unverified YouTube reupload should become the canonical reference.

| Reference | Proposed use in Rustychops | Evidence / qualification |
| --- | --- | --- |
| Paul Gilbert — Intense Rock / Intense Rock II | Sequenced picking studies, musical phrasing and reusable practice cells | [Alfred: Intense Rock II](https://www.alfred.com/paul-gilbert-intense-rock-ii/p/00-YMVH00202/) and [publisher DVD catalog](https://images.alfred.com/pdf/MI/2009_Alfred_DVD_Catalog.pdf); verify exact first-volume edition before chapter mapping |
| Paul Gilbert — Guitars From Mars 1, Classical | Foundational explanation and improvisation reference candidate | [CDJapan product listing](https://www.cdjapan.co.jp/product/AMBG-90018) identifies a Young Guitar Presents instructional-video reissue; retailer evidence, not reviewed chapters |
| Paul Gilbert — Guitars From Mars 2, Rock | Rock instruction reference candidate paired with volume 1 | [Japanese release listing](https://books.rakuten.co.jp/rb/3793878/); contents require closer review before lesson attribution |
| Paul Gilbert — Terrifying Guitar Trip | Connect technique studies to performance and musical application | [Alfred publisher page](https://www.alfred.com/products/paul-gilbert-terrifying-guitar-trip-00-26484) |
| Marty Friedman — Exotic Metal Guitar | Melodic individuality and expanded lead vocabulary | [Hal Leonard's classic Hot Licks reissue](https://www.halleonard.com/product/322514/marty-friedman-exotic-metal-guitar?menuid=494). This is the verified publisher title closest to the requested “Exotic Lead Guitar”; do not assume a separate title/edition without confirmation |
| Michael Angelo Batio — Speed Kills series | Speed technique and progressively more demanding lead studies | [Metal Method](https://metalmethod.com/), [Cracking the Code's Batio material](https://troygrady.com/product/michael-angelo-batio-bundle/) confirms the historical video; current product pages could not be read reliably |
| Michael Angelo Batio — Speed Kills 2 | Sweep and hybrid-picking study candidates | [Arobas/Guitar Pro instructional article](https://www.guitar-pro.com/blog/p/12015-sweeping-michael-angelo-batio) explicitly presents two course exercises; link rather than copy them |
| Michael Angelo Batio — Speed Kills 3 / revised editions | Further advanced reference candidates | Exact current syllabus and edition ordering remain to be verified; do not treat the numbers as app grades |
| Michael Angelo Batio — Star Licks instructional | Historical picking reference, compare with modern motion analysis | [Cracking the Code's artist bundle](https://troygrady.com/product/michael-angelo-batio-bundle/) identifies this predecessor |
| John Petrucci — Rock Discipline | A balanced technique-study reference | [Alfred publisher page](https://www.alfred.com/products/rock-discipline-00-reh863) confirms the two-hour instructional program |
| Frank Gambale — Monster Licks & Speed Picking | Economy/sweep strategy branch | [Artist FAQ](https://www.frankgambale.com/faq.htm) documents the instructional video |
| Frank Gambale — Chopbuilder | Structured technique-session reference; use short learner-controlled blocks | [Artist biography](https://frankgambale.com/bio.htm) identifies the program |
| Frank Gambale — Modes: No More Mystery | Modal application reference | [Artist biography](https://frankgambale.com/bio.htm) identifies the program |
| Vinnie Moore — Advanced Lead Guitar Techniques | Advanced articulation and lead-technique reference | [Hal Leonard classic Hot Licks reissue](https://www.halleonard.com/product/393920/vinnie-moore-advanced-lead-guitar-techniquesvinnie-moore-advanced-lead-guitar-techniques) |
| Jason Becker — The Legendary Guitar of Jason Becker | Advanced musical lead-study reference candidate | [Publisher listing](https://www.halleonard.com/search/artist/221989/jason-becker); detailed mapping requires content review |
| Shawn Lane — Power Licks / Power Solos | Further historical lead-study candidates | Companion publication was located, but primary video pages were not verified in this pass; keep both in the review backlog |
| Richie Kotzen — Rock Chops; Yngwie Malmsteen — Play Loud | Further requested-era research candidates | Do not assign lesson claims until exact publisher editions and accessible official material are verified |
| Jeff Loomis — Extreme Lead Guitar / Super Shred Guitar Masterclass | Dissonant scales, arpeggios and advanced metal lead application | [Hal Leonard catalog](https://www.halleonard.com/dealers/bin/Catalog2011DVD.pdf) and [Alfred catalog](https://images.alfred.com/pdf/CatalogPage_2017/CompleteCatalog_2016.pdf); later DVD-era material |
| Matt Heafy — Modern Metal Masterclass | Rhythm and modern-metal study reference | [JTC official course](https://jtcguitar.com/store/package/modern-metal-masterclass), linked by [Heafy's official profile](https://linktr.ee/matthewkheafy); contemporary material |
| Matt Heafy — Modern Metal Guitar Technique | Supporting modern-metal curriculum reference | [Publisher book page](https://www.fundamental-changes.com/book/modern-metal-guitar-technique/); book rather than VHS |
| Steve Vai — Mini Lessons and advanced techniques course | Bends, vibrato, whammy, imagination and expressive control | [Artist's lessons](https://www.vai.com/mini-lessons-84/) and [Berklee advanced course](https://online.berklee.edu/courses/steve-vai-guitar-techniques) |
| Paul Gilbert — ArtistWorks | Compare a modern four-level progression with historical tapes | [Official curriculum](https://artistworks.com/paul-gilbert); archived exchanges, not assumed current individual instruction |
| Troy Grady — Cracking the Code and Pickslanting Primer | Explain picking motion and diagnose exercise-design/string-change constraints | [Official Primer](https://troygrady.com/pickslanting-primer/) and [Primary Escape reference](https://troygrady.com/reference/primary-motion-reference/primary-escape/); use these authoritative pages to resolve the YouTube series' terminology |

### Young Guitar as a substantial reference collection

Young Guitar is a publisher and collection of instructional features, not one VHS title. Its [official 2024 archive feature](https://youngguitar.jp/web/202407-secret-exercises) republishes bonus-DVD exercises from 27 artists across three parts:

- Part 1: Guthrie Govan, Joe Satriani, John Petrucci, Kiko Loureiro, Paul Gilbert, Richie Kotzen and Steve Vai.
- Part 2: Chris Impellitteri, Derek Trucks, Doug Aldrich, George Lynch, Joe Bonamassa, Mattias IA Eklundh, Mick Thomson, Nuno Bettencourt, TJ Helmerich and Zakk Wylde.
- Part 3: Jeff Loomis, Rodrigo Sanchez, Alex Hutchings, Oz Noy, Bruce Bouillet, John 5, Jennifer Batten, Reb Beach, Chris Broderick and Ron Thal.

Use this collection as the review queue for demonstration styles and exercise variety, prioritizing the rock/metal artists. Map individual examples only after reviewing the actual official clips and identifying the related notation/access requirements. The [2020 sweep-focused issue](https://youngguitar.jp/yg/yg-202006) is another focused reference, including Jeff Loomis. This provides much broader coverage than linking a handful of famous tape uploads.

### Source handling

Every external reference record needs title, artist, provider, canonical URL, edition/format, reviewed date, related skills, verification status and access notes. Distinguish “title confirmed” from “contents reviewed”. No timestamps should be invented from catalog listings. Add chapter/time ranges after a dedicated review of an official video.

Rustychops should supply original explanations, exercises, score files and demonstrations. Provider links are supplemental. Embedding, bundling footage, reproducing course tabs or using actual song excerpts requires appropriate permission/licensing and cannot be assumed from public availability. The initial release can proceed with original lessons and links.

## Native application integration

The active product is C#/WPF, not the retained browser prototype. Current reusable components include native alphaTab tab/playback, metronome, count-in, speed, playback ranges, score creation/export, persistent practice data and continuous WASAPI input/tuner.

| Area | Proposed implementation |
| --- | --- |
| Navigation | Add Learn in `NativeWindow.cs`; route Learn home, catalog, path and lesson workspace through the existing page host. Centralize page switching enough to preserve editor dirty-state checks and stop stale playback requests |
| Learn views | New native `LearnHomePage`, `LearnCatalogPage`, `LearnPathPage` and `LearnLessonPage`; common styles and keyboard navigation consistent with the current shell |
| Content | Versioned bundled JSON catalog plus original `.gp` exercises and optional media; validate on load and at build time |
| Scale engine | `ScaleDefinition`, `FingeringPattern` and `ExerciseGenerator`; interval formulas, roots and string pitches independent of display orientation |
| Playback | Shared `PracticeSessionController` owning the existing playback engine. Snapshot/restore current score, speed, range, loop, track state and count-in when entering/leaving a lesson; avoid a second audio engine |
| Rendering | Reuse a render-only native alphaTab view pattern like `ScoreEditorPage`; attach views before starting playback and cancel obsolete asynchronous load/play operations |
| Progress | Separate versioned `LearnStore` at `%LOCALAPPDATA%/Rustychops/learn.json`; stable lesson IDs, atomic saves and recovery. Existing audio settings remain in `PracticeStore` |
| User exercises | Built-in lessons stay immutable; “Add to my practice” creates a distinct practice entry, and optional editable copies go through `ScoreLibrary` |
| Audio input | Continue the existing capture session across navigation; input meter/tuner may support setup. Recordings and assessment are later opt-in capabilities |

Review actual alphaTab model support before authoring bends, tuplets, ties, tapping and other advanced markings. The current editor cannot author several of these; pre-authored imported scores may be usable, but each feature needs playback/export validation. Avoid making lesson delivery depend on expanding the editor first.

PitchDetector currently serves a sustained-note tuner with a documented 60–1200 Hz range. It is not ready to grade fast distorted runs, chords, high-register solos or picking mechanics. First-release completion and readiness are user-reported. Any future automatic feedback needs latency calibration, instrument/tuning range coverage and real-device validation.

### Minimum data contracts

- **Lesson**: stable ID/version, title, outcome, level, skill tags, genre tags, prerequisites, steps, exercise IDs, readiness prompts and reference IDs.
- **Exercise**: score asset or generator recipe, tuning/string count, fret limits, source tempo, subdivisions, techniques, loop bounds and completion rule.
- **GuidedPath**: modules, lesson order, required skills, branches and review recommendations.
- **ScaleDefinition**: interval set, aliases, parent/mode links and optional distinct ascent/descent formulas.
- **LessonProgress**: visited/practiced/readiness states, last step, settings, user notes and lesson version.
- **PracticeSession**: exercise/version, start/time practiced, tonic/root fret, tempo/subdivision and self-review. Watching a video is not playing time.
- **Reference**: bibliographic metadata and verification/access status described above.

## Delivery sequence and acceptance criteria

### Phase 1 — One complete learning slice

Build the Learn entry, two-choice home, a small catalog, a starter guided module, lesson playback, local progress and the pentatonic ladder. Include genuine first-contact lessons so “never touched a guitar” is supported on day one. Ship roughly 10–12 original lessons: setup/tuning, first note/tab, pulse, first riff, power chords, muting, picking, pentatonic box, roots/transposition, the ladder and musical phrasing. Use concise written/visual instruction and synthesized listening examples; add original filmed hand demonstrations as an explicit content task.

Acceptance: a new learner can navigate from first contact to a played riff; a returning player can choose and complete/resume the ladder. Lesson scores loop correctly, progress survives restart, practice state is restored, audio input stays connected, and navigation honors unsaved edits. Both home choices work; only the authored starter segment is presented as available guided content.

### Phase 2 — Rock and metal foundations

Author the rhythm/lead foundations, major/minor/pentatonic/blues lessons, position connections, ear/theory work, backing etudes, review queue and custom practice sessions. Separate Rock and Metal guided branches after shared fundamentals. Add key-order options and tune/fret compatibility checks.

Acceptance: each branch has coherent prerequisite coverage, complete musical studies and repeatable self-review outcomes. No incomplete advanced path is presented as a finished course.

### Phase 3 — Advanced technique and artist-inspired studies

Review the reference queue more deeply; author original picking-motion, sweep, legato, tapping, expressive, neoclassical and modern-metal modules. Add native demonstration media if authored assets are ready. Expand instrument support only alongside tested content.

Acceptance: advanced etudes render/play accurately with their techniques and rhythms; prerequisites and equipment needs are clear; tempo/subdivision tracking is meaningful; provider references use verified official URLs and honest access labels.

### Phase 4 — Optional feedback and broader progress

Explore local session recording, playback comparison and calibrated single-note timing/pitch feedback. Prototype against clean and processed real guitar inputs before making scoring claims. Integrate a global Progress screen when its scope includes both Learn and existing practice sessions.

Acceptance: feedback has measured error rates and documented supported inputs; ambiguous results are presented as uncertain rather than marking the learner wrong. This phase is independent of delivering the instruction program.

## Verification plan

Extend `Rustychops.Checks` with scale/pattern membership, enharmonic aliases, fret bounds, tuning/string numbering, ascend/descend ordering, key-cycle coverage and score round trips. Validate catalog IDs, references, prerequisites and asset existence. Verify cancellation/state restoration when changing lessons and pages, persistent progress migrations and resumed ladder steps.

Render meaningful sample lesson pages at supported window sizes and Windows scaling. Manually play representative riffs, scale sequences and advanced scores, checking beat alignment, tempo changes, count-in, loops and track balance. Use subject-matter review for lesson explanations and fingerings; passing software checks does not validate pedagogy.

## Recommended first implementation scope

Start with Phase 1 on the current native application: Learn home, both paths, authentic beginner lessons, shared lesson playback and the requested twelve-key pentatonic routine. Keep the broader curriculum visible in planning documents and introduce its modules as they are authored and verified. No development or application changes are included in this planning deliverable.
