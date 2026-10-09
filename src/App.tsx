import { useCallback, useEffect, useRef, useState } from 'react'
import * as alphaTab from '@coderline/alphatab'
import './App.css'
import AudioSettings from './AudioSettings'

type TrackState = { index: number; name: string; shortName: string; mute: boolean; solo: boolean; volume: number }
type SavedRegion = { id: string; fileName: string; name: string; startTick: number; endTick: number; speed: number; createdAt: string }
type RecentFile = { name: string; openedAt: string }

const SAMPLE_SCORE = String.raw`\title "Practice Workbench"
\subtitle "Open a Guitar Pro or MusicXML file to begin"
\tempo 100
\ts 4 4
.
:8 5.6 7.6 8.6 5.5 7.5 8.5 10.5 7.4 |
8.4 10.4 7.3 9.3 10.3 7.2 8.2 10.2 |
10.2 8.2 7.2 10.3 9.3 7.3 10.4 8.4 |
7.4 10.5 8.5 7.5 5.5 8.6 7.6 5.6 |`

const acceptedFormats = '.gp,.gpx,.gp3,.gp4,.gp5,.xml,.musicxml,.capx,.tex'

function readStored<T>(key: string, fallback: T): T {
  try {
    const value = localStorage.getItem(key)
    return value ? (JSON.parse(value) as T) : fallback
  } catch { return fallback }
}

function formatTime(milliseconds: number) {
  if (!Number.isFinite(milliseconds)) return '0:00'
  const totalSeconds = Math.max(0, Math.floor(milliseconds / 1000))
  return `${Math.floor(totalSeconds / 60)}:${String(totalSeconds % 60).padStart(2, '0')}`
}

function App() {
  const [screen, setScreen] = useState<'practice' | 'settings'>('practice')
  const [audioOutput, setAudioOutput] = useState('')
  const scoreHostRef = useRef<HTMLDivElement>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const apiRef = useRef<alphaTab.AlphaTabApi | null>(null)
  const currentFileRef = useRef('Getting Started')
  const [currentFile, setCurrentFile] = useState('Getting Started')
  const [songTitle, setSongTitle] = useState('Practice Workbench')
  const [songArtist, setSongArtist] = useState('Local practice session')
  const [tracks, setTracks] = useState<TrackState[]>([])
  const [isPlaying, setIsPlaying] = useState(false)
  const [isReady, setIsReady] = useState(false)
  const [speed, setSpeed] = useState(75)
  const [metronome, setMetronome] = useState(true)
  const [countIn, setCountIn] = useState(true)
  const [looping, setLooping] = useState(false)
  const [range, setRange] = useState<{ startTick: number; endTick: number } | null>(null)
  const [position, setPosition] = useState(0)
  const [duration, setDuration] = useState(0)
  const [status, setStatus] = useState('Loading practice engine…')
  const [recentFiles, setRecentFiles] = useState<RecentFile[]>(() => readStored('rustychops.recent', []))
  const [savedRegions, setSavedRegions] = useState<SavedRegion[]>(() => readStored('rustychops.regions', []))

  useEffect(() => { currentFileRef.current = currentFile }, [currentFile])
  useEffect(() => { localStorage.setItem('rustychops.recent', JSON.stringify(recentFiles)) }, [recentFiles])
  useEffect(() => { localStorage.setItem('rustychops.regions', JSON.stringify(savedRegions)) }, [savedRegions])

  useEffect(() => {
    if (!scoreHostRef.current || apiRef.current) return
    const api = new alphaTab.AlphaTabApi(scoreHostRef.current, {
      core: { tex: true, fontDirectory: '/font/' },
      display: { layoutMode: alphaTab.LayoutMode.Page, staveProfile: alphaTab.StaveProfile.Tab, scale: 0.85 },
      player: {
        playerMode: alphaTab.PlayerMode.EnabledSynthesizer,
        soundFont: '/soundfont/sonivox.sf3',
        enableUserInteraction: true,
        scrollMode: alphaTab.ScrollMode.Continuous,
        scrollElement: document.querySelector('.score-scroll') as HTMLElement,
        scrollOffsetY: -32
      }
    })
    apiRef.current = api
    api.playbackSpeed = speed / 100
    api.metronomeVolume = metronome ? 0.8 : 0
    api.countInVolume = countIn ? 0.8 : 0
    api.scoreLoaded.on((score) => {
      setSongTitle(score.title || currentFileRef.current.replace(/\.[^.]+$/, ''))
      setSongArtist(score.artist || score.album || 'Imported score')
      setTracks(score.tracks.map((track, index) => ({
        index, name: track.name || `Track ${index + 1}`, shortName: track.shortName || `T${index + 1}`,
        mute: false, solo: false, volume: 100
      })))
      setRange(null); setPosition(0)
      setStatus('Score loaded. Drag across the tab to select a practice loop.')
    })
    api.playerReady.on(() => { setIsReady(true); setDuration(api.endTime); setStatus('Ready to practice') })
    api.playerStateChanged.on((event) => setIsPlaying(event.state === alphaTab.synth.PlayerState.Playing))
    api.playerPositionChanged.on((event) => { setPosition(event.currentTime); setDuration(event.endTime) })
    api.playbackRangeChanged.on((event) => setRange(event.playbackRange ? { ...event.playbackRange } : null))
    api.error.on((error) => {
      console.error(error)
      setStatus('The score could not be loaded. Check that it is a supported Guitar Pro or MusicXML file.')
    })
    api.tex(SAMPLE_SCORE)
    return () => { api.destroy(); apiRef.current = null }
    // The alphaTab instance owns its configuration after creation.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const loadFile = useCallback(async (file: File) => {
    const api = apiRef.current
    if (!api) return
    setStatus(`Opening ${file.name}…`); setIsReady(false); currentFileRef.current = file.name; setCurrentFile(file.name)
    try {
      const bytes = new Uint8Array(await file.arrayBuffer())
      if (!api.load(bytes)) throw new Error('Unsupported score format')
      setRecentFiles((items) => [{ name: file.name, openedAt: new Date().toISOString() }, ...items.filter((item) => item.name !== file.name)].slice(0, 8))
    } catch (error) {
      console.error(error); setStatus(`Could not open ${file.name}. The file may be damaged or unsupported.`)
    }
  }, [])

  const updateSpeed = (nextSpeed: number) => {
    const clamped = Math.min(150, Math.max(25, nextSpeed)); setSpeed(clamped)
    if (apiRef.current) apiRef.current.playbackSpeed = clamped / 100
  }
  const toggleMetronome = () => {
    const next = !metronome; setMetronome(next)
    if (apiRef.current) apiRef.current.metronomeVolume = next ? 0.8 : 0
  }
  const toggleCountIn = () => {
    const next = !countIn; setCountIn(next)
    if (apiRef.current) apiRef.current.countInVolume = next ? 0.8 : 0
  }
  const toggleLoop = () => {
    const next = !looping; setLooping(next)
    if (apiRef.current) apiRef.current.isLooping = next
  }
  const clearRange = () => {
    const api = apiRef.current; if (!api) return
    api.playbackRange = null; api.clearPlaybackRangeHighlight(); setRange(null)
  }
  const saveRegion = () => {
    if (!range) return
    const fallback = `Loop ${savedRegions.filter((item) => item.fileName === currentFile).length + 1}`
    const name = window.prompt('Name this practice region:', fallback)?.trim()
    if (!name) return
    setSavedRegions((items) => [{ id: crypto.randomUUID(), fileName: currentFile, name, startTick: range.startTick, endTick: range.endTick, speed, createdAt: new Date().toISOString() }, ...items])
    setStatus(`Saved practice region “${name}”`)
  }
  const activateRegion = (region: SavedRegion) => {
    const api = apiRef.current
    if (!api || region.fileName !== currentFile) { setStatus(`Open ${region.fileName} before loading this saved region.`); return }
    api.playbackRange = { startTick: region.startTick, endTick: region.endTick }; api.isLooping = true
    setLooping(true); updateSpeed(region.speed); setRange({ startTick: region.startTick, endTick: region.endTick }); setStatus(`Loaded practice region “${region.name}”`)
  }
  const updateTrack = (index: number, change: Partial<TrackState>) => {
    const api = apiRef.current; const scoreTrack = api?.score?.tracks[index]
    if (!api || !scoreTrack) return
    if (typeof change.mute === 'boolean') api.changeTrackMute([scoreTrack], change.mute)
    if (typeof change.solo === 'boolean') api.changeTrackSolo([scoreTrack], change.solo)
    if (typeof change.volume === 'number') api.changeTrackVolume([scoreTrack], change.volume / 100)
    setTracks((items) => items.map((track) => track.index === index ? { ...track, ...change } : track))
  }
  const renderTrack = (index: number) => {
    const api = apiRef.current; const track = api?.score?.tracks[index]
    if (api && track) api.renderTracks([track])
  }
  const visibleRegions = savedRegions.filter((item) => item.fileName === currentFile)

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand"><div className="brand-mark">R</div><div><strong>Rustychops</strong><span>Desktop practice lab</span></div></div>
        <button className="open-button" onClick={() => fileInputRef.current?.click()}><span>＋</span> Open score</button>
        <input ref={fileInputRef} className="visually-hidden" type="file" accept={acceptedFormats} onChange={(event) => {
          const file = event.target.files?.[0]; if (file) void loadFile(file); event.target.value = ''
        }} />
        <nav className="nav-list" aria-label="Workspace">
          <button className={`nav-item ${screen === 'practice' ? 'active' : ''}`} onClick={() => setScreen('practice')}><span>▤</span> Practice</button>
          <button className={`nav-item ${screen === 'settings' ? 'active' : ''}`} onClick={() => { apiRef.current?.stop(); setScreen('settings') }}><span>⚙</span> Configure Guitar &amp; Audio Settings</button>
          <button className="nav-item" disabled><span>◇</span> Library <small>Soon</small></button>
          <button className="nav-item" disabled><span>↗</span> Progress <small>Soon</small></button>
        </nav>
        <div className="side-section">
          <div className="section-heading"><span>Recent scores</span><span>{recentFiles.length}</span></div>
          {recentFiles.length === 0 ? <p className="empty-copy">Files you open will appear here. Scores stay on this computer.</p> : recentFiles.map((file) => (
            <div className="recent-file" key={file.name}><span className="file-icon">GP</span><span><strong>{file.name}</strong><small>{new Date(file.openedAt).toLocaleDateString()}</small></span></div>
          ))}
        </div>
        <div className="privacy-note"><span>●</span><div><strong>Local by design</strong><small>No account. No uploads. No subscription.</small></div></div>
      </aside>

      <main className="workspace">
        {screen === 'settings' && <AudioSettings output={audioOutput} onOutput={async (device) => {
          if (!apiRef.current?.player?.isReady) throw new Error('Playback engine is not ready')
          await apiRef.current.setOutputDevice(device ? { deviceId: device.deviceId, label: device.label, isDefault: device.deviceId === 'default' } : null)
          setAudioOutput(device?.deviceId ?? '')
        }} />}
        <div hidden={screen !== 'practice'}>
        <header className="topbar">
          <div className="song-heading"><span className="eyebrow">NOW PRACTICING</span><h1>{songTitle}</h1><p>{songArtist} · {currentFile}</p></div>
          <div className="status-pill"><span className={isReady ? 'ready-dot' : 'loading-dot'} /> {status}</div>
        </header>
        <section className="score-panel">
          <div className="score-toolbar">
            <div><strong>Score</strong><span>{range ? 'Practice range selected' : 'Drag from one beat to another to create a loop'}</span></div>
            <div className="toolbar-actions">{range && <button onClick={saveRegion}>Save region</button>}{range && <button onClick={clearRange}>Clear range</button>}</div>
          </div>
          <div className="score-scroll"><div ref={scoreHostRef} className="alphatab-host" /></div>
        </section>

        <section className="transport" aria-label="Playback controls">
          <div className="transport-primary">
            <button className="icon-button" aria-label="Stop" onClick={() => apiRef.current?.stop()}>■</button>
            <button className="play-button" aria-label={isPlaying ? 'Pause' : 'Play'} disabled={!isReady} onClick={() => apiRef.current?.playPause()}>{isPlaying ? 'Ⅱ' : '▶'}</button>
            <div className="time-readout">{formatTime(position)} <span>/ {formatTime(duration)}</span></div>
          </div>
          <div className="transport-group speed-control"><span className="control-label">SPEED</span><button onClick={() => updateSpeed(speed - 5)}>−</button><strong>{speed}%</strong><button onClick={() => updateSpeed(speed + 5)}>＋</button><input aria-label="Playback speed" type="range" min="25" max="150" step="5" value={speed} onChange={(e) => updateSpeed(Number(e.target.value))} /></div>
          <div className="toggle-controls">
            <button className={looping ? 'toggle active' : 'toggle'} onClick={toggleLoop}><span>↻</span> Loop</button>
            <button className={metronome ? 'toggle active' : 'toggle'} onClick={toggleMetronome}><span>♩</span> Metronome</button>
            <button className={countIn ? 'toggle active' : 'toggle'} onClick={toggleCountIn}><span>2</span> Count-in</button>
          </div>
        </section>

        <div className="lower-grid">
          <section className="card mixer-card">
            <div className="card-heading"><div><span className="eyebrow">MIXER</span><h2>Tracks</h2></div><span>{tracks.length} loaded</span></div>
            <div className="track-list">{tracks.map((track) => (
              <div className="track-row" key={track.index}>
                <button className="track-name" onClick={() => renderTrack(track.index)} title="Show only this track"><span>{track.index + 1}</span><div><strong>{track.name}</strong><small>{track.shortName}</small></div></button>
                <button className={track.mute ? 'mini-toggle active' : 'mini-toggle'} onClick={() => updateTrack(track.index, { mute: !track.mute })}>M</button>
                <button className={track.solo ? 'mini-toggle solo active' : 'mini-toggle solo'} onClick={() => updateTrack(track.index, { solo: !track.solo })}>S</button>
                <input aria-label={`${track.name} volume`} type="range" min="0" max="150" value={track.volume} onChange={(e) => updateTrack(track.index, { volume: Number(e.target.value) })} />
                <span className="volume-value">{track.volume}%</span>
              </div>
            ))}</div>
          </section>
          <section className="card region-card">
            <div className="card-heading"><div><span className="eyebrow">FOCUS</span><h2>Practice regions</h2></div><span>{visibleRegions.length} saved</span></div>
            {visibleRegions.length === 0 ? <div className="empty-state"><div className="empty-icon">⌁</div><strong>No saved regions yet</strong><p>Drag across the score, then save the selected range for focused repetition.</p></div> : (
              <div className="region-list">{visibleRegions.map((region) => <button key={region.id} onClick={() => activateRegion(region)}><div><strong>{region.name}</strong><span>{region.speed}% starting speed</span></div><span>Load →</span></button>)}</div>
            )}
          </section>
        </div>
        </div>
      </main>
    </div>
  )
}

export default App
