/** Normalized difference pitch detection for a single sustained guitar note. */
export function detectPitch(samples: Float32Array, sampleRate: number): number | null {
  const size = Math.floor(samples.length / 2)
  let energy = 0
  for (const sample of samples) energy += sample * sample
  if (Math.sqrt(energy / samples.length) < 0.005) return null
  const minLag = Math.floor(sampleRate / 1200)
  const maxLag = Math.min(size - 1, Math.ceil(sampleRate / 60))
  const difference = new Float32Array(maxLag + 1)
  let total = 0
  for (let lag = 1; lag <= maxLag; lag++) {
    let sum = 0
    for (let i = 0; i < size; i++) sum += (samples[i] - samples[i + lag]) ** 2
    total += sum
    difference[lag] = total ? sum * lag / total : 1
  }
  for (let lag = minLag; lag < maxLag - 1; lag++) {
    if (difference[lag] >= 0.15) continue
    while (lag + 1 < maxLag && difference[lag + 1] < difference[lag]) lag++
    const before = difference[lag - 1], center = difference[lag], after = difference[lag + 1]
    const divisor = 2 * (2 * center - before - after)
    const refined = lag + (divisor ? (after - before) / divisor : 0)
    return sampleRate / refined
  }
  return null
}

export function describePitch(frequency: number, reference = 440) {
  const midi = Math.round(69 + 12 * Math.log2(frequency / reference))
  const target = reference * 2 ** ((midi - 69) / 12)
  return {
    name: `${['C', 'C♯', 'D', 'D♯', 'E', 'F', 'F♯', 'G', 'G♯', 'A', 'A♯', 'B'][((midi % 12) + 12) % 12]}${Math.floor(midi / 12) - 1}`,
    cents: 1200 * Math.log2(frequency / target),
  }
}
