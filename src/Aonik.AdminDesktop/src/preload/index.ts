import { contextBridge, ipcRenderer } from 'electron'

export interface ElectronAPI {
  getAppVersion: () => Promise<string>
  getApiBaseUrl: () => Promise<string>
  showNotification: (title: string, body: string) => Promise<void>
  saveFile: (defaultName: string, content: string) => Promise<string | null>
  windowMinimize: () => void
  windowMaximize: () => void
  windowClose: () => void
  onDeepLink: (callback: (url: string) => void) => () => void
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
  onDeepLink: (callback: (url: string) => void) => {
    const handler = (_event: Electron.IpcRendererEvent, url: string) => callback(url)
    ipcRenderer.on('deep-link', handler)
    return () => ipcRenderer.removeListener('deep-link', handler)
  }
}

contextBridge.exposeInMainWorld('electronAPI', api)
