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

declare global {
  interface Window {
    electronAPI: ElectronAPI
  }
}
