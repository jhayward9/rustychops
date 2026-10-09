import { existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'

const repository = fileURLToPath(new URL('../', import.meta.url))
const windows = process.env.WINDIR
if (!windows) throw new Error('The launcher must be compiled on Windows.')
const compiler = ['Framework64', 'Framework']
  .map((folder) => join(windows, 'Microsoft.NET', folder, 'v4.0.30319', 'csc.exe'))
  .find(existsSync)
if (!compiler) throw new Error('The Windows .NET Framework C# compiler is unavailable.')
const output = join(repository, 'Rustychops.exe')
const result = spawnSync(compiler, [
  '/nologo', '/target:winexe', '/optimize+', '/platform:anycpu',
  '/reference:System.Windows.Forms.dll', `/out:${output}`,
  join(repository, 'native', 'Rustychops.Launcher', 'Launcher.cs'),
], { stdio: 'inherit', windowsHide: true })
if (result.error) throw result.error
if (result.status !== 0) process.exit(result.status ?? 1)
console.log(`Created ${output}`)
