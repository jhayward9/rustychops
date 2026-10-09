import { useEffect, useRef, useState } from 'react'
import { describePitch, detectPitch } from './pitch'
import MidiInput from './MidiInput'

type Props = { output: string; onOutput: (device: MediaDeviceInfo | null) => Promise<void> }
type Session = { stream: MediaStream; context: AudioContext; frame: number }

export default function AudioSettings({ output, onOutput }: Props) {
  const [devices, setDevices] = useState<MediaDeviceInfo[]>([])
  const [input, setInput] = useState('')
  const [channel, setChannel] = useState(0)
  const [channelCount, setChannelCount] = useState(1)
  const [running, setRunning] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('Enable audio to reveal device names, then select your guitar input.')
  const [level, setLevel] = useState(-60)
  const [clipping, setClipping] = useState(false)
  const [frequency, setFrequency] = useState<number | null>(null)
  const [reference, setReference] = useState(440)
  const session = useRef<Session | null>(null)
  const generation = useRef(0)
  const active = useRef(true)
  const selectedChannel = useRef(channel)
  useEffect(() => { selectedChannel.current = channel }, [channel])
  const supportsOutput = typeof AudioContext !== 'undefined' && 'setSinkId' in AudioContext.prototype

  function stop() {
    generation.current++
    const current = session.current
    session.current = null
    if (current) {
      cancelAnimationFrame(current.frame)
      current.stream.getTracks().forEach((track) => track.stop())
      void current.context.close().catch(() => {})
    }
    if (active.current) { setRunning(false); setBusy(false); setLevel(-60); setClipping(false); setFrequency(null) }
  }

  useEffect(() => {
    active.current = true
    const refresh = () => {
      void navigator.mediaDevices?.enumerateDevices().then((list) => {
        if (active.current) setDevices(list.filter((device) => device.kind === 'audioinput' || device.kind === 'audiooutput'))
      }).catch(() => { if (active.current) setMessage('Could not list audio devices. Check browser permissions.') })
    }
    refresh()
    navigator.mediaDevices?.addEventListener('devicechange', refresh)
    return () => { active.current = false; stop(); navigator.mediaDevices?.removeEventListener('devicechange', refresh) }
  }, [])

  async function start(deviceId = input) {
    stop()
    const request = generation.current
    setBusy(true)
    setMessage('Waiting for audio access. Allow microphone access in the browser prompt to reveal your USB audio devices.')
    let stream: MediaStream | null = null
    let context: AudioContext | null = null
    try {
      if (!navigator.mediaDevices?.getUserMedia) throw new Error('Audio capture is unavailable. Open in Chrome or Edge on localhost or HTTPS.')
      stream = await navigator.mediaDevices.getUserMedia({ audio: {
        ...(deviceId ? { deviceId: { exact: deviceId } } : {}),
        echoCancellation: false, noiseSuppression: false, autoGainControl: false,
        channelCount: { ideal: 2 },
      } })
      if (!active.current || request !== generation.current) { stream.getTracks().forEach((track) => track.stop()); return }
      context = new AudioContext()
      await context.resume()
      if (!active.current || request !== generation.current) { stream.getTracks().forEach((track) => track.stop()); await context.close(); return }
      const source = context.createMediaStreamSource(stream)
      const count = Math.max(1, stream.getAudioTracks()[0].getSettings().channelCount ?? 1)
      const splitter = context.createChannelSplitter(count)
      source.connect(splitter)
      const analysers = Array.from({ length: count }, (_, index) => {
        const analyser = context!.createAnalyser()
        analyser.fftSize = 4096
        splitter.connect(analyser, index)
        return analyser
      })
      const muted = context.createGain()
      muted.gain.value = 0
      analysers.forEach((analyser) => analyser.connect(muted))
      muted.connect(context.destination)
      const current: Session = { stream, context, frame: 0 }
      session.current = current
      setChannelCount(count); setChannel(0); selectedChannel.current = 0
      setRunning(true)
      setMessage(`Listening to ${stream.getAudioTracks()[0].label || 'audio input'}. Play one string at a time.`)
      stream.getAudioTracks()[0].addEventListener('ended', () => {
        if (session.current === current) { stop(); setMessage('Audio input disconnected. Reconnect and enable audio again.') }
      })
      const samples = new Float32Array(4096)
      let lastUpdate = 0
      const tick = (time: number) => {
        if (session.current !== current) return
        if (time - lastUpdate >= 90) {
          lastUpdate = time
          analysers[Math.min(selectedChannel.current, count - 1)].getFloatTimeDomainData(samples)
          let sum = 0, peak = 0
          for (const sample of samples) { sum += sample * sample; peak = Math.max(peak, Math.abs(sample)) }
          setLevel(Math.max(-60, 20 * Math.log10(Math.sqrt(sum / samples.length) || 0.000001)))
          setClipping(peak >= 0.98)
          setFrequency(detectPitch(samples, current.context.sampleRate))
        }
        current.frame = requestAnimationFrame(tick)
      }
      current.frame = requestAnimationFrame(tick)
      const list = await navigator.mediaDevices.enumerateDevices()
      if (active.current && request === generation.current) setDevices(list.filter((device) => device.kind === 'audioinput' || device.kind === 'audiooutput'))
    } catch (error) {
      stream?.getTracks().forEach((track) => track.stop())
      if (context && context.state !== 'closed') void context.close().catch(() => {})
      if (active.current && request === generation.current) {
        stop()
        setMessage(error instanceof DOMException && error.name === 'NotAllowedError'
          ? 'Audio permission denied. Allow microphone access in your browser site settings.'
          : error instanceof DOMException && ['NotFoundError', 'OverconstrainedError'].includes(error.name)
            ? 'The selected input is unavailable. Choose another device or reconnect it.'
            : error instanceof Error ? error.message : 'Could not start audio input.')
      }
    } finally { if (active.current) setBusy(false) }
  }

  async function chooseOutput(id: string) {
    setBusy(true)
    try {
      await onOutput(devices.find((device) => device.deviceId === id && device.kind === 'audiooutput') ?? null)
      if (active.current) setMessage('Score playback output updated.')
    } catch { if (active.current) setMessage('Could not change playback output. Check browser permissions or use Windows audio settings.') }
    finally { if (active.current) setBusy(false) }
  }

  async function testOutput() {
    setBusy(true)
    const context = new AudioContext()
    try {
      if (output) await (context as AudioContext & { setSinkId: (id: string) => Promise<void> }).setSinkId(output)
      await context.resume()
      const oscillator = context.createOscillator(), gain = context.createGain()
      oscillator.frequency.value = 440
      gain.gain.setValueAtTime(0, context.currentTime)
      gain.gain.linearRampToValueAtTime(0.08, context.currentTime + 0.03)
      gain.gain.linearRampToValueAtTime(0, context.currentTime + 0.5)
      oscillator.connect(gain); gain.connect(context.destination)
      oscillator.start(); oscillator.stop(context.currentTime + 0.55)
      await new Promise<void>((resolve) => { oscillator.onended = () => resolve() })
    } catch { if (active.current) setMessage('Output test failed. Check your selected device and permissions.') }
    finally { await context.close(); if (active.current) setBusy(false) }
  }

  const pitch = frequency ? describePitch(frequency, reference) : null
  return <div className="audio-settings">
    <header className="topbar"><div className="song-heading"><span className="eyebrow">SETUP</span><h1>Configure Guitar &amp; Audio Settings</h1><p>Select your USB audio interface, check its signal, and tune your guitar.</p></div></header>
    <section className="card audio-device-card">
      <div className="card-heading"><h2>Audio devices</h2><button className="midi-enable" disabled={busy} onClick={() => void start()}>{busy ? 'Connecting…' : running ? 'Restart audio' : 'Enable audio'}</button></div>
      <div className="settings-body"><p role="status">{message}</p>
        <div className="device-grid">
          <label>Guitar audio input<select value={input} disabled={busy} onChange={(event) => { setInput(event.target.value); if (running) void start(event.target.value) }}><option value="">System default input</option>{devices.filter((device) => device.kind === 'audioinput' && device.deviceId).map((device, index) => <option key={device.deviceId} value={device.deviceId}>{device.label || `Audio input ${index + 1}`}</option>)}</select></label>
          <label>Playback audio output<select value={output} disabled={busy || !supportsOutput} onChange={(event) => void chooseOutput(event.target.value)}><option value="">System default output</option>{devices.filter((device) => device.kind === 'audiooutput' && device.deviceId).map((device, index) => <option key={device.deviceId} value={device.deviceId}>{device.label || `Audio output ${index + 1}`}</option>)}</select></label>
        </div>
        {!supportsOutput && <p>This browser uses the Windows default playback device. Change it in Windows audio settings.</p>}
        {running && channelCount > 1 && <label>Input channel<select value={channel} onChange={(event) => { setChannel(Number(event.target.value)); setFrequency(null) }}>{Array.from({ length: channelCount }, (_, index) => <option key={index} value={index}>Channel {index + 1}</option>)}</select></label>}
        <div className="audio-actions"><button className="midi-enable" disabled={busy} onClick={() => void testOutput()}>Test output tone</button><button className="midi-enable" disabled={!running && !busy} onClick={() => { stop(); setMessage('Audio listening stopped.') }}>{busy && !running ? 'Cancel connection' : 'Stop listening'}</button></div>
        <p>Choose the HeadRush USB audio input if it appears. Use a clean guitar tone for tuning. Input is analysed locally and is not played back through speakers.</p>
      </div>
    </section>
    <div className="lower-grid">
      <section className="card"><div className="card-heading"><h2>Input volume</h2><span>{running ? 'Listening' : 'Stopped'}</span></div><div className="settings-body"><meter aria-label="Guitar input volume" min={-60} max={0} low={-40} high={-6} optimum={-18} value={level} /><div className="meter-scale"><span>−60 dBFS</span><strong>{level <= -60 ? 'No signal' : `${level.toFixed(1)} dBFS`}</strong><span>0 dBFS</span></div><p>{clipping ? 'Signal is clipping. Lower the interface input gain.' : 'Adjust input gain on your interface. Play a string and watch the meter.'}</p></div></section>
      <section className="card"><div className="card-heading"><h2>Guitar tuner</h2><span>A4 = {reference} Hz</span></div><div className="settings-body tuner"><div className="tuner-note">{pitch?.name ?? '—'}</div><p>{frequency ? `${frequency.toFixed(1)} Hz` : running ? 'Pluck one string and let it ring' : 'Enable audio to start tuning'}</p><div className="tuning-rail"><span style={{ left: `${50 + Math.max(-50, Math.min(50, pitch?.cents ?? 0))}%`, visibility: pitch ? 'visible' : 'hidden' }} /></div><div className="meter-scale"><span>Flat</span><strong>{pitch ? Math.abs(pitch.cents) <= 5 ? 'In tune' : `${Math.abs(pitch.cents).toFixed(0)} cents ${pitch.cents < 0 ? 'flat' : 'sharp'}` : 'Waiting for a clear note'}</strong><span>Sharp</span></div><p>Standard tuning: E2 · A2 · D3 · G3 · B3 · E4</p><label>Reference pitch<select value={reference} onChange={(event) => setReference(Number(event.target.value))}>{[432, 440, 442].map((value) => <option key={value} value={value}>A4 = {value} Hz</option>)}</select></label></div></section>
    </div>
    <MidiInput />
  </div>
}
