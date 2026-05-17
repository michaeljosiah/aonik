/**
 * Renderer-side surface for the Electron desktop shell.
 *
 * The desktop build (`Aonik.AdminDesktop`) ships a preload script that calls
 * `contextBridge.exposeInMainWorld('electronAPI', api)`. In a regular browser
 * that global is absent, so `isElectron` is `false` and consumers can fall
 * back to web-only behaviour without any Electron code being bundled into the
 * web app.
 *
 * Keep this file the single point of contact: feature code should depend on
 * `isElectron` / the typed helpers exported here, never reach into
 * `window.electronAPI` directly. That keeps the desktop coupling auditable
 * and lets us evolve the bridge without grepping the codebase.
 */

/**
 * Renderer-visible contract for the desktop bridge. Mirrors the
 * `ElectronAPI` interface defined in
 * `src/Aonik.AdminDesktop/src/preload/index.ts` — the two are linked by
 * convention rather than by import (the preload lives in a separate package).
 */

export interface AuthTokenSet {
  access_token: string;
  id_token?: string;
  refresh_token?: string;
  token_type: string;
  expires_in: number;
  scope?: string;
}

export interface AuthErrorEvent {
  error: string;
  description?: string;
}

export interface AuthBridge {
  begin: (loginHint?: string) => Promise<{ state: string }>;
  refresh: (refreshToken: string) => Promise<AuthTokenSet>;
  cancel: () => Promise<void>;
  onTokens: (callback: (tokens: AuthTokenSet) => void) => () => void;
  onError: (callback: (event: AuthErrorEvent) => void) => () => void;
}

export interface ElectronBridge {
  getAppVersion: () => Promise<string>;
  getApiBaseUrl: () => Promise<string>;
  showNotification: (title: string, body: string) => Promise<void>;
  saveFile: (defaultName: string, content: string) => Promise<string | null>;
  windowMinimize: () => void;
  windowMaximize: () => void;
  windowClose: () => void;
  setTitleBarColor: (color: string, symbolColor: string) => void;
  onDeepLink: (callback: (url: string) => void) => () => void;
  auth: AuthBridge;
}

declare global {
  interface Window {
    electronAPI?: ElectronBridge;
  }
}

export const isElectron =
  typeof window !== 'undefined' && typeof window.electronAPI !== 'undefined';

/** Typed bridge handle, or `null` outside Electron. */
export const electronAPI: ElectronBridge | null = isElectron
  ? (window.electronAPI as ElectronBridge)
  : null;

// Tag the document root for CSS branching (title bar height, drag strip,
// `.min-h-screen` override). Done as a side-effect on first import so the
// class is present before React's first paint — App.tsx imports this
// module, so the side-effect runs ahead of any layout calculation.
if (isElectron && typeof document !== 'undefined') {
  document.documentElement.classList.add('is-electron');
}

/**
 * Single-flight resolver for the backend base URL exposed by the desktop
 * main process. The IPC call is async, so any consumer that constructs URLs
 * before it resolves would race against `/api` (the web-mode default) and
 * issue requests against `file:///api/...` under the file:// origin.
 *
 * Behaviour:
 *  - In a regular browser: returns `null` immediately.
 *  - In Electron: returns the URL the main process advertised, caching it
 *    after the first call. Concurrent callers share the same in-flight
 *    promise. If the IPC fails for any reason, falls back to `null` so
 *    callers can continue with their web-mode default rather than hang.
 */
let cachedApiBaseUrl: string | null = null;
let inFlight: Promise<string | null> | null = null;

export function getApiBaseUrlOnce(): Promise<string | null> {
  if (!electronAPI) return Promise.resolve(null);
  if (cachedApiBaseUrl) return Promise.resolve(cachedApiBaseUrl);
  if (inFlight) return inFlight;

  inFlight = electronAPI
    .getApiBaseUrl()
    .then((url) => {
      cachedApiBaseUrl = url || null;
      return cachedApiBaseUrl;
    })
    .catch(() => null)
    .finally(() => {
      inFlight = null;
    });

  return inFlight;
}

/**
 * Retint the Electron Window Controls Overlay (the strip with min/max/close
 * in the top-right). Pages call this on mount so the title bar visually
 * fuses with the page background.
 *
 * No-op outside Electron and on platforms where the bridge does not expose
 * the helper — older preloads bundled before this change won't have it,
 * and we don't want one missing version to break the renderer.
 */
export function setTitleBarColor(color: string, symbolColor: string): void {
  if (!electronAPI || typeof electronAPI.setTitleBarColor !== 'function') return;
  electronAPI.setTitleBarColor(color, symbolColor);
}
