import { useEffect, useRef, useState } from 'react'

export default function MidiInput() {
  const [access, setAccess] = useState<MIDIAccess | null>(null)
  const [inputs, setInputs] = useState<MIDIInput[]>([])
  const [selected, setSelected] = useState('')
  const [pending, setPending] = useState(false)
  const [status, setStatus] = useState('Enable MIDI to find connected instruments.')
  const [activity, setActivity] = useState('Waiting for MIDI messages')
  const [notes, setNotes] = useState<number[]>([])
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false }
  }, [])

  async function enable() {
    if (!window.isSecureContext || !navigator.requestMIDIAccess) {
      setStatus('MIDI is unavailable here. Open this app in Chrome or Edge on localhost or HTTPS.')
      return
    }
    setPending(true)
    try {
      const nextAccess = await navigator.requestMIDIAccess({ sysex: false })
      if (mounted.current) { setAccess(nextAccess); setStatus('MIDI enabled. Choose an input and play a note.') }
    } catch (error) {
      if (mounted.current) setStatus(error instanceof DOMException && error.name === 'NotAllowedError'
        ? 'MIDI permission denied. Allow MIDI in your browser site settings, then try again.'
        : 'Could not access MIDI devices. Check the device connection and try again.')
    } finally {
      if (mounted.current) setPending(false)
    }
  }

  useEffect(() => {
    if (!access) return
    const refresh = () => {
      const connected = [...access.inputs.values()].filter((input) => input.state === 'connected')
      setInputs((current) => current.length === connected.length && current.every((input, index) => input === connected[index]) ? current : connected)
      setSelected((current) => connected.some((input) => input.id === current) ? current : connected[0]?.id ?? '')
    }
    refresh()
    access.addEventListener('statechange', refresh)
    return () => access.removeEventListener('statechange', refresh)
  }, [access])

  useEffect(() => {
    // Reset transient activity when synchronizing with a different external MIDI port.
    // oxlint-disable-next-line react/set-state-in-effect
    setNotes([])
    setActivity('Waiting for MIDI messages')
    const input = inputs.find((item) => item.id === selected)
    if (!input) return
    const receive = (event: MIDIMessageEvent) => {
      const data = event.data
      if (!data?.length) return
      const command = data[0] & 0xf0
      const channel = (data[0] & 0x0f) + 1
      const note = data[1]
      const velocity = data[2]
      if ((command === 0x90 || command === 0x80) && data.length >= 3) {
        const on = command === 0x90 && velocity > 0
        setNotes((current) => on ? [...new Set([...current, note])].sort((a, b) => a - b) : current.filter((value) => value !== note))
        setActivity(`${on ? 'Note on' : 'Note off'}: ${noteName(note)} · channel ${channel} · velocity ${velocity}`)
      } else {
        if (command === 0xb0 && data.length >= 3 && [120, 123].includes(data[1])) setNotes([])
        setActivity(`MIDI received: ${Array.from(data, (byte) => byte.toString(16).padStart(2, '0')).join(' ').toUpperCase()}`)
      }
    }
    input.addEventListener('midimessage', receive)
    void input.open().catch(() => { if (mounted.current) setStatus('Could not open the MIDI input. Reconnect the device and try again.') })
    return () => {
      input.removeEventListener('midimessage', receive)
      void input.close().catch(() => {})
    }
  }, [inputs, selected])

  return <section className="card midi-card" aria-label="Guitar MIDI input">
    <div className="card-heading"><div><span className="eyebrow">GUITAR INPUT</span><h2>USB MIDI</h2></div>
      {!access && <button className="midi-enable" disabled={pending} onClick={() => void enable()}>{pending ? 'Connecting…' : 'Enable MIDI'}</button>}
      {access && <button className="midi-enable" disabled={pending} onClick={() => void enable()}>Rescan MIDI</button>}
    </div>
    <div className="midi-body">
      <p role="status">{access && inputs.length === 0 ? 'No MIDI inputs detected. Check the USB connection and whether the device exposes a MIDI input.' : status}</p>
      {inputs.length > 0 && <label>Instrument<select value={selected} onChange={(event) => setSelected(event.target.value)}>{inputs.map((input) => <option key={input.id} value={input.id}>{input.name || 'Unnamed MIDI input'}{input.manufacturer ? ` — ${input.manufacturer}` : ''}</option>)}</select></label>}
      {selected && <><div className="midi-notes">{notes.length ? notes.map(noteName).join(' · ') : 'No notes held'}</div><p className="midi-activity">{activity}</p></>}
      <p>USB audio alone does not send MIDI notes. Your guitar needs a MIDI pickup or an audio-to-MIDI converter to report played notes. Pedal controls may send other MIDI messages.</p>
    </div>
  </section>
}

function noteName(note: number) {
  return `${['C', 'C♯', 'D', 'D♯', 'E', 'F', 'F♯', 'G', 'G♯', 'A', 'A♯', 'B'][note % 12]}${Math.floor(note / 12) - 1}`
}
