import { app, BrowserWindow, Menu, ipcMain, dialog, Notification, shell } from 'electron'
import { join } from 'path'
import { is } from '@electron-toolkit/utils'
import { loadWindowState, saveWindowState } from './window-state'
import { registerAuthIpc, handleAuthDeepLink } from './auth'
import { writeFile } from 'fs/promises'

// Declared by electron-vite define config
declare const ADMIN_UI_DEV_URL: string
declare const AONIK_API_DEFAULT_URL: string

const PROTOCOL = 'aonik'

// Single instance lock
const gotTheLock = app.requestSingleInstanceLock()
if (!gotTheLock) {
  app.quit()
}

let mainWindow: BrowserWindow | null = null

function createWindow(): void {
  const windowState = loadWindowState()

  mainWindow = new BrowserWindow({
    width: windowState.width,
    height: windowState.height,
    x: windowState.x,
    y: windowState.y,
    minWidth: 1024,
    minHeight: 680,
    // Use the OS native title bar so the window is reliably draggable on every
    // screen (login, loading, setup) without each route having to declare its
    // own `-webkit-app-region: drag` zone.
    title: 'Aonik Admin',
    webPreferences: {
      preload: join(__dirname, '../preload/index.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    },
    show: false
  })

  if (windowState.isMaximized) {
    mainWindow.maximize()
  }

  // Save window state on changes
  mainWindow.on('resize', () => saveWindowState(mainWindow!))
  mainWindow.on('move', () => saveWindowState(mainWindow!))
  mainWindow.on('maximize', () => saveWindowState(mainWindow!))
  mainWindow.on('unmaximize', () => saveWindowState(mainWindow!))

  // Show window when ready
  mainWindow.on('ready-to-show', () => {
    mainWindow!.show()
  })

  // Open auth-provider popups inside Electron so window.open() returns a
  // real handle (required by Auth0 loginWithPopup / MSAL popup). All other
  // external links go to the OS default browser.
  //
  // Auth0 SDK opens window.open('', 'auth0:authorize:popup') first and only
  // afterwards sets popup.location.href to the authorize URL — so we have to
  // match on the frame name rather than the URL.
  const isAuthPopupFrame = (frameName: string): boolean =>
    frameName === 'auth0:authorize:popup' || frameName.startsWith('msal.')

  const isAuthPopupUrl = (url: string): boolean => {
    if (!url || url === 'about:blank') return false
    try {
      const host = new URL(url).hostname
      return host.endsWith('.auth0.com') || host === 'login.microsoftonline.com'
    } catch {
      return false
    }
  }

  mainWindow.webContents.setWindowOpenHandler(({ url, frameName }) => {
    if (isAuthPopupFrame(frameName) || isAuthPopupUrl(url)) {
      return {
        action: 'allow',
        overrideBrowserWindowOptions: {
          width: 480,
          height: 700,
          modal: true,
          parent: mainWindow ?? undefined,
          autoHideMenuBar: true,
          webPreferences: {
            contextIsolation: true,
            nodeIntegration: false,
            sandbox: true
          }
        }
      }
    }
    shell.openExternal(url)
    return { action: 'deny' }
  })

  // NOTE: Previously we installed a webRequest.onHeadersReceived CSP override
  // here. That hook fires for every HTTP(S) response in the default session,
  // which meant it also rewrote the Auth0 popup's CSP and broke its own
  // scripts/styles. The renderer is loaded via file:// (no HTTP headers to
  // override anyway), so CSP for our pages belongs in the renderer's
  // index.html as a <meta http-equiv> tag, not here.

  // Load the app
  if (is.dev && ADMIN_UI_DEV_URL) {
    mainWindow.loadURL(ADMIN_UI_DEV_URL)
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
  }
}

// Register custom protocol for deep links
if (process.defaultApp) {
  if (process.argv.length >= 2) {
    app.setAsDefaultProtocolClient(PROTOCOL, process.execPath, [
      '--',
      process.argv[1]
    ])
  }
} else {
  app.setAsDefaultProtocolClient(PROTOCOL)
}

const getMainWindow = (): BrowserWindow | null => mainWindow

async function dispatchDeepLink(rawUrl: string): Promise<void> {
  // Auth callbacks are intercepted in main; everything else (future deep
  // links like aonik://invite/...) is forwarded to the renderer.
  const handled = await handleAuthDeepLink(rawUrl, getMainWindow)
  if (!handled) {
    mainWindow?.webContents.send('deep-link', rawUrl)
  }
}

// Handle second instance (Windows deep-link handler)
app.on('second-instance', (_event, commandLine) => {
  if (mainWindow) {
    if (mainWindow.isMinimized()) mainWindow.restore()
    mainWindow.focus()

    const deepLinkUrl = commandLine.find((arg) => arg.startsWith(`${PROTOCOL}://`))
    if (deepLinkUrl) {
      void dispatchDeepLink(deepLinkUrl)
    }
  }
})

// macOS deep-link handler
app.on('open-url', (_event, url) => {
  void dispatchDeepLink(url)
})

// IPC Handlers
function registerIpcHandlers(): void {
  ipcMain.handle('get-app-version', () => app.getVersion())

  ipcMain.handle('get-api-base-url', () => {
    return (
      process.env.AONIK_API_URL ||
      process.env.services__api__https__0 ||
      process.env.services__api__http__0 ||
      AONIK_API_DEFAULT_URL
    )
  })

  ipcMain.handle('show-notification', (_event, title: string, body: string) => {
    new Notification({ title, body }).show()
  })

  ipcMain.handle('save-file', async (_event, defaultName: string, content: string) => {
    if (!mainWindow) return null

    const result = await dialog.showSaveDialog(mainWindow, {
      defaultPath: defaultName,
      filters: [
        { name: 'All Files', extensions: ['*'] },
        { name: 'JSON', extensions: ['json'] },
        { name: 'CSV', extensions: ['csv'] }
      ]
    })

    if (result.canceled || !result.filePath) return null

    await writeFile(result.filePath, content, 'utf-8')
    return result.filePath
  })

  ipcMain.on('window-minimize', () => mainWindow?.minimize())
  ipcMain.on('window-maximize', () => {
    if (mainWindow?.isMaximized()) {
      mainWindow.unmaximize()
    } else {
      mainWindow?.maximize()
    }
  })
  ipcMain.on('window-close', () => mainWindow?.close())
}

// App lifecycle
app.whenReady().then(() => {
  // Remove Electron's default "File / Edit / View / Window / Help" menu.
  // We expose the relevant actions inside the React UI (window controls in
  // the title bar, keyboard shortcuts for cut/copy/paste come from the web
  // platform itself, and devtools is wired through F12 / Ctrl+Shift+I on
  // dev builds). On macOS this leaves the system's default app menu intact.
  Menu.setApplicationMenu(null)

  registerIpcHandlers()
  registerAuthIpc(getMainWindow)
  createWindow()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow()
    }
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit()
  }
})
