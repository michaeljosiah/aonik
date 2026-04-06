/**
 * Copies the AdminUI dist/ output into out/renderer/ for Electron production builds.
 * Run via: npm run copy-renderer (called automatically by prebuild).
 */
import { cpSync, existsSync, mkdirSync, rmSync } from 'fs'
import { resolve, dirname } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const adminUiDist = resolve(__dirname, '../../Aonik.AdminUi/dist')
const rendererOut = resolve(__dirname, '../out/renderer')

if (!existsSync(adminUiDist)) {
  console.warn(
    '⚠ AdminUI dist/ not found at %s.\n' +
    '  Run "npm run build" in src/Aonik.AdminUi first for production builds.\n' +
    '  Skipping renderer copy (dev mode uses the Vite dev server instead).',
    adminUiDist
  )
  process.exit(0)
}

// Clean and copy
if (existsSync(rendererOut)) {
  rmSync(rendererOut, { recursive: true })
}
mkdirSync(rendererOut, { recursive: true })
cpSync(adminUiDist, rendererOut, { recursive: true })

console.log('✓ Copied AdminUI dist/ → out/renderer/')
