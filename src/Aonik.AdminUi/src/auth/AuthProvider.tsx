import { type ReactNode, useState, useEffect, useMemo } from 'react';
import { PublicClientApplication } from '@azure/msal-browser';
import { MsalProvider } from '@azure/msal-react';
import { Auth0Provider } from '@auth0/auth0-react';
import { AuthProvider as OidcAuthProvider } from 'react-oidc-context';
import { WebStorageStateStore } from 'oidc-client-ts';
import {
  getAuthProvider,
  msalConfig,
  auth0Config,
  keycloakConfig,
  validateAuthConfig,
  getProviderDisplayName,
  getRawAuthProvider,
} from './authConfig';
import {
  MsalAuthContextProvider,
  Auth0AuthContextProvider,
  KeycloakAuthContextProvider,
  MockAuthContextProvider,
  ElectronAuthContextProvider,
} from './useAuth';
import { AuthError, AuthErrors, type AuthErrorInfo } from '@/components/AuthError';
import { LoadingScreen } from '@/components/layout';
import { isElectron } from '@/lib/electron';

interface AuthProviderProps {
  children: ReactNode;
}

// Azure AD Provider wrapper with error handling
function AzureAdAuthProvider({ children }: AuthProviderProps) {
  const [msalInstance, setMsalInstance] = useState<PublicClientApplication | null>(null);
  const [isInitializing, setIsInitializing] = useState(true);
  const [error, setError] = useState<AuthErrorInfo | null>(null);

  useEffect(() => {
    const initMsal = async () => {
      try {
        const instance = new PublicClientApplication(msalConfig);
        await instance.initialize();
        setMsalInstance(instance);
      } catch (err) {
        console.error('MSAL initialization error:', err);
        setError(
          AuthErrors.initializationFailed(
            'Microsoft Entra ID',
            err instanceof Error ? err.message : 'Unknown error during initialization'
          )
        );
      } finally {
        setIsInitializing(false);
      }
    };

    initMsal();
  }, []);

  if (isInitializing) {
    return <LoadingScreen phase="authenticating" />;
  }

  if (error) {
    return <AuthError error={error} onRetry={() => window.location.reload()} />;
  }

  if (!msalInstance) {
    return (
      <AuthError 
        error={AuthErrors.initializationFailed('Microsoft Entra ID', 'MSAL instance not created')} 
        onRetry={() => window.location.reload()} 
      />
    );
  }

  return (
    <MsalProvider instance={msalInstance}>
      <MsalAuthContextProvider>{children}</MsalAuthContextProvider>
    </MsalProvider>
  );
}

// Auth0 Provider wrapper with error boundary
function Auth0AuthProvider({ children }: AuthProviderProps) {
  // Auth0 provider handles its own initialization
  // We just need to validate the config is present

  return (
    <Auth0Provider
      domain={auth0Config.domain}
      clientId={auth0Config.clientId}
      authorizationParams={auth0Config.authorizationParams}
      cacheLocation={auth0Config.cacheLocation}
      useRefreshTokens={auth0Config.useRefreshTokens}
      useRefreshTokensFallback={auth0Config.useRefreshTokensFallback}
      onRedirectCallback={(appState) => {
        // Handle redirect after login
        window.history.replaceState(
          {},
          document.title,
          appState?.returnTo || window.location.pathname
        );
      }}
    >
      <Auth0AuthContextProvider>{children}</Auth0AuthContextProvider>
    </Auth0Provider>
  );
}

// Keycloak Provider wrapper — Spec 029.
//
// Wraps react-oidc-context's <AuthProvider> with a UserManager configured for
// the operator's Keycloak realm. PKCE-only (public client, no secret), tokens
// kept in localStorage so refresh works across tabs and full page reloads
// (mirroring the Auth0 cacheLocation choice). After the OIDC callback we strip
// the `code` / `state` / `session_state` / `iss` query params from the URL so
// the address bar doesn't carry a one-shot auth code through the SPA route.
function KeycloakAuthProvider({ children }: AuthProviderProps) {
  // Memoise to keep the UserManager identity stable across re-renders —
  // re-creating it on every render would tear down silent-renew timers and
  // detach the iframe-based silent callback.
  const userStore = useMemo(
    () => new WebStorageStateStore({ store: window.localStorage }),
    [],
  );

  return (
    <OidcAuthProvider
      authority={keycloakConfig.authority}
      client_id={keycloakConfig.client_id}
      client_secret={keycloakConfig.client_secret}
      redirect_uri={keycloakConfig.redirect_uri}
      post_logout_redirect_uri={keycloakConfig.post_logout_redirect_uri}
      scope={keycloakConfig.scope}
      loadUserInfo={keycloakConfig.loadUserInfo}
      automaticSilentRenew={true}
      userStore={userStore}
      onSigninCallback={() => {
        // Strip OIDC response params so the SPA history doesn't carry the
        // single-use code/state. Without this, refreshing the page after
        // sign-in re-submits the (now-invalid) code and triggers an error.
        window.history.replaceState({}, document.title, window.location.pathname);
      }}
    >
      <KeycloakAuthContextProvider>{children}</KeycloakAuthContextProvider>
    </OidcAuthProvider>
  );
}

// Main AuthProvider that validates config and switches based on configuration
export function AuthProvider({ children }: AuthProviderProps) {
  const validation = validateAuthConfig();
  const provider = getAuthProvider();

  // Check for invalid provider
  if (validation.error) {
    const rawProvider = getRawAuthProvider();
    return (
      <AuthError 
        error={AuthErrors.invalidProvider(rawProvider)} 
      />
    );
  }

  // Check for missing configuration
  if (!validation.isValid) {
    return (
      <AuthError 
        error={AuthErrors.missingConfig(
          getProviderDisplayName(provider),
          validation.missingFields
        )} 
      />
    );
  }

  // Render the appropriate provider
  if (provider === 'mock') {
    return <MockAuthContextProvider>{children}</MockAuthContextProvider>;
  }

  // Desktop (Electron) uses the system-browser PKCE flow handled in the
  // main process — the @auth0/auth0-react SDK doesn't work cleanly under
  // file:// (no postMessage origin), so we bypass it entirely. The web
  // path keeps using Auth0Provider unchanged.
  if (provider === 'auth0' && isElectron) {
    return <ElectronAuthContextProvider>{children}</ElectronAuthContextProvider>;
  }

  if (provider === 'auth0') {
    return <Auth0AuthProvider>{children}</Auth0AuthProvider>;
  }

  if (provider === 'keycloak') {
    return <KeycloakAuthProvider>{children}</KeycloakAuthProvider>;
  }

  return <AzureAdAuthProvider>{children}</AzureAdAuthProvider>;
}
