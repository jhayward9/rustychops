import { readFile } from 'node:fs/promises'
import { basename } from 'node:path'
import * as alphaTab from '@coderline/alphatab'

const files = process.argv.slice(2)
if (files.length === 0) {
  console.error('Usage: npm run verify:imports -- <score.gp5> [score.gpx ...]')
  process.exitCode = 2
} else {
  let failed = false
  for (const file of files) {
    try {
      const bytes = new Uint8Array(await readFile(file))
      const score = alphaTab.importer.ScoreLoader.loadScoreFromBytes(bytes)
      console.log(`PASS ${basename(file)}: ${score.title || 'Untitled'} (${score.tracks.length} tracks, ${score.masterBars.length} bars)`)
    } catch (error) {
      failed = true
      console.error(`FAIL ${basename(file)}:`, error instanceof Error ? error.message : error)
    }
  }
  if (failed) process.exitCode = 1
}
