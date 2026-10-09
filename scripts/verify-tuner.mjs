import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import ts from 'typescript'

const source = await readFile(new URL('../src/pitch.ts', import.meta.url), 'utf8')
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText
const { detectPitch, describePitch } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`)
for (const rate of [44100, 48000]) {
  for (const frequency of [65.406, 82.407, 110, 146.832, 195.998, 246.942, 329.628, 440]) {
    for (const harmonic of [0, 0.65]) {
      const samples = Float32Array.from({ length: 4096 }, (_, i) =>
        0.25 * Math.sin(2 * Math.PI * frequency * i / rate) + harmonic * 0.25 * Math.sin(4 * Math.PI * frequency * i / rate))
      const detected = detectPitch(samples, rate)
      assert.ok(detected, `Missing pitch ${frequency} Hz`)
      assert.ok(Math.abs(1200 * Math.log2(detected / frequency)) < 3, `Incorrect pitch ${frequency}: ${detected}`)
    }
  }
  assert.equal(detectPitch(new Float32Array(4096), rate), null)
  const quiet = Float32Array.from({ length: 4096 }, (_, i) => 0.001 * Math.sin(2 * Math.PI * 110 * i / rate))
  assert.equal(detectPitch(quiet, rate), null)
}
assert.equal(describePitch(82.407).name, 'E2')
assert.equal(describePitch(329.628).name, 'E4')
assert.ok(Math.abs(describePitch(440).cents) < 0.01)
assert.ok(Math.abs(describePitch(432, 432).cents) < 0.01)
assert.ok(describePitch(445).cents > 0)
assert.ok(describePitch(435).cents < 0)
console.log('PASS tuner: guitar notes, harmonics, silence, low signal, cents, and reference pitch at 44.1/48 kHz')
