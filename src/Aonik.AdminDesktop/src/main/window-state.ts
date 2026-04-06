import { app, type BrowserWindow } from 'electron'
import { join } from 'path'
import { readFileSync, writeFileSync, mkdirSync } from 'fs'

interface WindowState {
  width: number
  height: number
  x?: number
  y?: number
  isMaximized: boolean
}

const DEFAULT_STATE: WindowState = {
  width: 1400,
  height: 900,
  isMaximized: false
}

function getStateFilePath(): string {
  return join(app.getPath('userData'), 'window-state.json')
}

export function loadWindowState(): WindowState {
  try {
    const data = readFileSync(getStateFilePath(), 'utf-8')
    return { ...DEFAULT_STATE, ...JSON.parse(data) }
  } catch {
    return DEFAULT_STATE
  }
}

let saveTimeout: ReturnType<typeof setTimeout> | null = null

export function saveWindowState(window: BrowserWindow): void {
  // Debounce saves to avoid excessive disk writes during resize/move
  if (saveTimeout) clearTimeout(saveTimeout)

  saveTimeout = setTimeout(() => {
    const bounds = window.getBounds()
    const state: WindowState = {
      width: bounds.width,
      height: bounds.height,
      x: bounds.x,
      y: bounds.y,
      isMaximized: window.isMaximized()
    }

    try {
      const dir = app.getPath('userData')
      mkdirSync(dir, { recursive: true })
      writeFileSync(getStateFilePath(), JSON.stringify(state, null, 2))
    } catch {
      // Ignore write errors
    }
  }, 500)
}
