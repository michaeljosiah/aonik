/**
 * System-browser PKCE auth flow for the desktop app.
 *
 * Why this file exists: under file:// the Auth0 SPA SDK's popup flow is
 * fragile (popup blockers, sandbox CSP, postMessage origin mismatch). The
 * idiomatic native-app pattern is to open the system browser, let Auth0
 * universal login handle credentials there, and bring the user back via a
 * custom protocol handler that exchanges an authorization code for tokens
 * over a back-channel POST.
 *
 * Lifecycle:
 *   renderer → ipcMain('auth:begin', loginHint?)
 *     ↓ main generates PKCE pair, stashes the verifier under a state key
 *     ↓ main opens https://{AUTH0_DOMAIN}/authorize?... in system browser
 *     ↓ user authenticates with Auth0
 *     ↓ Auth0 redirects to aonik://callback?code=...&state=...
 *     ↓ OS launches our app with the deep link; we call handleAuthCallback
 *     ↓ main looks up the verifier, POSTs to /oauth/token
 *     ↓ main sends 'auth:tokens' (or 'auth:error') to renderer
 *   renderer → ipcMain('auth:refresh', refreshToken) for token renewal
 *
 * State is kept in main-process memory only (Phase 5 will persist the
 * refresh token through Electron safeStorage). PKCE state entries expire
 * after 10 minutes so a stale browser tab can't be used to inject a code
 * after the user gave up.
 */

import { ipcMain, shell, type BrowserWindow } from 'electron'
import { randomBytes, createHash } from 'node:crypto'

// Declared by electron-vite define config.
declare const AUTH0_DOMAIN: string
declare const AUTH0_CLIENT_ID: string
declare const AUTH0_AUDIENCE: string

const REDIRECT_URI = 'aonik://callback'
const PKCE_TTL_MS = 10 * 60 * 1000

interface PendingAuth {
  state: string
  codeVerifier: string
  createdAt: number
  loginHint?: string
}

const pendingAuth = new Map<string, PendingAuth>()

function generatePkce(): { codeVerifier: string; codeChallenge: string; state: string } {
  const codeVerifier = randomBytes(32).toString('base64url')
  const codeChallenge = createHash('sha256').update(codeVerifier).digest('base64url')
  const state = randomBytes(16).toString('base64url')
  return { codeVerifier, codeChallenge, state }
}

function buildAuthorizeUrl(state: string, codeChallenge: string, loginHint?: string): string {
  const params = new URLSearchParams({
    response_type: 'code',
    client_id: AUTH0_CLIENT_ID,
    redirect_uri: REDIRECT_URI,
    scope: 'openid profile email offline_access',
    audience: AUTH0_AUDIENCE,
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    state
  })
  if (loginHint) {
    params.set('login_hint', loginHint)
  }
  return `https://${AUTH0_DOMAIN}/authorize?${params.toString()}`
}

function purgeExpired(now: number): void {
  for (const [key, value] of pendingAuth) {
    if (now - value.createdAt > PKCE_TTL_MS) {
      pendingAuth.delete(key)
    }
  }
}

/** Auth0 /oauth/token response shape (the fields we care about). */
export interface AuthTokenSet {
  access_token: string
  id_token?: string
  refresh_token?: string
  token_type: string
  expires_in: number
  scope?: string
}

async function exchangeCodeForTokens(code: string, codeVerifier: string): Promise<AuthTokenSet> {
  const response = await fetch(`https://${AUTH0_DOMAIN}/oauth/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'authorization_code',
      client_id: AUTH0_CLIENT_ID,
      code,
      code_verifier: codeVerifier,
      redirect_uri: REDIRECT_URI
    }).toString()
  })
  if (!response.ok) {
    const body = await response.text().catch(() => '')
    throw new Error(`Auth0 token exchange failed (${response.status}): ${body}`)
  }
  return (await response.json()) as AuthTokenSet
}

async function exchangeRefreshToken(refreshToken: string): Promise<AuthTokenSet> {
  const response = await fetch(`https://${AUTH0_DOMAIN}/oauth/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'refresh_token',
      client_id: AUTH0_CLIENT_ID,
      refresh_token: refreshToken
    }).toString()
  })
  if (!response.ok) {
    const body = await response.text().catch(() => '')
    throw new Error(`Auth0 refresh failed (${response.status}): ${body}`)
  }
  return (await response.json()) as AuthTokenSet
}

/**
 * Wire the auth IPC handlers and remember how to reach the renderer for
 * sending token deliveries / errors. Call once at app startup.
 */
export function registerAuthIpc(getMainWindow: () => BrowserWindow | null): void {
  ipcMain.handle('auth:begin', async (_event, loginHint?: string) => {
    const now = Date.now()
    purgeExpired(now)

    const { codeVerifier, codeChallenge, state } = generatePkce()
    pendingAuth.set(state, { state, codeVerifier, createdAt: now, loginHint })

    const url = buildAuthorizeUrl(state, codeChallenge, loginHint)
    await shell.openExternal(url)
    return { state }
  })

  ipcMain.handle('auth:refresh', async (_event, refreshToken: string) => {
    if (!refreshToken || typeof refreshToken !== 'string') {
      throw new Error('refresh_token is required')
    }
    return exchangeRefreshToken(refreshToken)
  })

  ipcMain.handle('auth:cancel', () => {
    pendingAuth.clear()
  })
}

/**
 * Handle an inbound aonik:// deep-link URL. Returns true if the URL was an
 * auth callback (so the caller knows not to forward it as a generic
 * deep-link to the renderer), false otherwise.
 */
export async function handleAuthDeepLink(
  rawUrl: string,
  getMainWindow: () => BrowserWindow | null
): Promise<boolean> {
  let url: URL
  try {
    url = new URL(rawUrl)
  } catch {
    return false
  }

  // The custom-protocol URL is `aonik://callback?...` — depending on host
  // platform parsing, the path may live on either `host` or `pathname`.
  const isCallback =
    rawUrl.startsWith('aonik://callback') ||
    url.hostname === 'callback' ||
    url.pathname === '/callback' ||
    url.pathname === 'callback'

  if (!isCallback) {
    return false
  }

  const mainWindow = getMainWindow()
  const error = url.searchParams.get('error')
  const errorDescription = url.searchParams.get('error_description')
  const code = url.searchParams.get('code')
  const state = url.searchParams.get('state')

  if (error) {
    mainWindow?.webContents.send('auth:error', {
      error,
      description: errorDescription ?? undefined
    })
    return true
  }

  if (!code || !state) {
    mainWindow?.webContents.send('auth:error', { error: 'invalid_callback' })
    return true
  }

  const pending = pendingAuth.get(state)
  pendingAuth.delete(state)
  if (!pending) {
    mainWindow?.webContents.send('auth:error', { error: 'invalid_state' })
    return true
  }

  try {
    const tokens = await exchangeCodeForTokens(code, pending.codeVerifier)
    mainWindow?.webContents.send('auth:tokens', tokens)
  } catch (err) {
    mainWindow?.webContents.send('auth:error', {
      error: 'token_exchange_failed',
      description: err instanceof Error ? err.message : String(err)
    })
  }

  return true
}
