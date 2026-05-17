import { contextBridge, ipcRenderer } from 'electron'

export interface AuthTokenSet {
  access_token: string
  id_token?: string
  refresh_token?: string
  token_type: string
  expires_in: number
  scope?: string
}

export interface AuthErrorEvent {
  error: string
  description?: string
}

export interface AuthBridge {
  /**
   * Kick off a PKCE login by opening the system browser at Auth0's
   * universal login. The returned promise resolves once the browser has
   * been launched; tokens arrive asynchronously via `onTokens`. The user
   * may cancel by closing the browser tab — call `cancel()` if you want
   * to invalidate the pending PKCE state.
   */
  begin: (loginHint?: string) => Promise<{ state: string }>
  /** Exchange a refresh token for a new access (+ optionally rotated refresh) token. */
  refresh: (refreshToken: string) => Promise<AuthTokenSet>
  /** Drop any in-flight PKCE state in main (e.g. on logout). */
  cancel: () => Promise<void>
  onTokens: (callback: (tokens: AuthTokenSet) => void) => () => void
  onError: (callback: (event: AuthErrorEvent) => void) => () => void
}

export interface ElectronAPI {
  getAppVersion: () => Promise<string>
  getApiBaseUrl: () => Promise<string>
  showNotification: (title: string, body: string) => Promise<void>
  saveFile: (defaultName: string, content: string) => Promise<string | null>
  windowMinimize: () => void
  windowMaximize: () => void
  windowClose: () => void
  /**
   * Retint the Window Controls Overlay (the strip with min/max/close in the
   * top-right). Called by the renderer when navigating between pages with
   * different background colours, so the bar visually fuses with the page.
   * No-op on macOS — that platform uses traffic-light buttons on host chrome.
   */
  setTitleBarColor: (color: string, symbolColor: string) => void
  onDeepLink: (callback: (url: string) => void) => () => void
  auth: AuthBridge
}

const auth: AuthBridge = {
  begin: (loginHint?: string) => ipcRenderer.invoke('auth:begin', loginHint),
  refresh: (refreshToken: string) => ipcRenderer.invoke('auth:refresh', refreshToken),
  cancel: () => ipcRenderer.invoke('auth:cancel'),
  onTokens: (callback) => {
    const handler = (_event: Electron.IpcRendererEvent, tokens: AuthTokenSet) => callback(tokens)
    ipcRenderer.on('auth:tokens', handler)
    return () => ipcRenderer.removeListener('auth:tokens', handler)
  },
  onError: (callback) => {
    const handler = (_event: Electron.IpcRendererEvent, payload: AuthErrorEvent) => callback(payload)
    ipcRenderer.on('auth:error', handler)
    return () => ipcRenderer.removeListener('auth:error', handler)
  }
}

const api: ElectronAPI = {
  getAppVersion: () => ipcRenderer.invoke('get-app-version'),
  getApiBaseUrl: () => ipcRenderer.invoke('get-api-base-url'),
  showNotification: (title: string, body: string) =>
    ipcRenderer.invoke('show-notification', title, body),
  saveFile: (defaultName: string, content: string) =>
    ipcRenderer.invoke('save-file', defaultName, content),
  windowMinimize: () => ipcRenderer.send('window-minimize'),
  windowMaximize: () => ipcRenderer.send('window-maximize'),
  windowClose: () => ipcRenderer.send('window-close'),
  setTitleBarColor: (color: string, symbolColor: string) =>
    ipcRenderer.send('title-bar:set-color', { color, symbolColor }),
  onDeepLink: (callback: (url: string) => void) => {
    const handler = (_event: Electron.IpcRendererEvent, url: string) => callback(url)
    ipcRenderer.on('deep-link', handler)
    return () => ipcRenderer.removeListener('deep-link', handler)
  },
  auth
}

contextBridge.exposeInMainWorld('electronAPI', api)
