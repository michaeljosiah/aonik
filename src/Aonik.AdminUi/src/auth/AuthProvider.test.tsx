// @vitest-environment jsdom
import { act, type ReactNode } from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { expect, it, vi } from 'vitest';
import { AuthProvider } from './AuthProvider';

const oidc = vi.hoisted(() => ({ callback: undefined as undefined | ((state: { returnTo: string }) => void) }));
vi.mock('@auth0/auth0-react', () => ({ Auth0Provider: ({ children, onRedirectCallback }: { children: ReactNode; onRedirectCallback: typeof oidc.callback }) => {
  oidc.callback = onRedirectCallback; return children;
} }));
vi.mock('./authConfig', () => ({
  getAuthProvider: () => 'auth0', validateAuthConfig: () => ({ isValid: true }),
  getRawAuthProvider: () => 'auth0', getProviderDisplayName: () => 'Auth0',
  auth0Config: { domain: 'example.test', clientId: 'test', authorizationParams: {} }, msalConfig: {}, keycloakConfig: {},
}));
vi.mock('./useAuth', () => ({ Auth0AuthContextProvider: ({ children }: { children: ReactNode }) => children }));
vi.mock('@/lib/electron', () => ({ isElectron: false }));
vi.mock('@/components/layout', () => ({ LoadingScreen: () => null }));
vi.mock('@/components/AuthError', () => ({ AuthError: () => null, AuthErrors: {} }));

function Location() { const location = useLocation(); return <output>{location.pathname}{location.search}</output>; }

it.each(['/signin?invite=one-shot&tenant=tenant-a', '//outside.example', '/\\outside.example'])(
  'updates the router after Auth0 returns, accepting only a local path: %s', async returnTo => {
    vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
    const element = document.createElement('div'); const root = createRoot(element);
    try {
      await act(async () => root.render(<MemoryRouter initialEntries={['/callback']}><AuthProvider><Location /></AuthProvider></MemoryRouter>));
      await act(async () => oidc.callback!({ returnTo }));
      expect(element.textContent).toBe(returnTo.startsWith('/signin') ? returnTo : window.location.pathname);
    } finally { await act(async () => root.unmount()); vi.unstubAllGlobals(); }
  },
);
