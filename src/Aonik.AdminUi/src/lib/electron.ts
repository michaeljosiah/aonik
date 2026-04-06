/** Electron runtime detection. When running inside the Electron desktop shell,
 *  the preload script exposes `window.electronAPI`. In a regular browser this
 *  is simply `false` / `null` — no Electron code is bundled into the web app. */

export const isElectron = typeof window !== 'undefined' && 'electronAPI' in window;

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const electronAPI = isElectron ? (window as any).electronAPI : null;
