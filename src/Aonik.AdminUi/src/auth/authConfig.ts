import { type Configuration, LogLevel } from '@azure/msal-browser';
import { isElectron } from '@/lib/electron';

// Auth provider type - determined by environment variable.
//
// Spec 029 adds 'keycloak' as a third operator-choice provider. The backend
// has supported Keycloak-issued JWTs since Phase 1; this is the SPA-side
// login flow that closes the loop for "100% Keycloak deployments".
export type AuthProvider = 'azure-ad' | 'auth0' | 'keycloak' | 'mock';

// Validation result type
export interface ConfigValidationResult {
  isValid: boolean;
  provider: AuthProvider | null;
  missingFields: string[];
  error?: string;
}

// Get the raw auth provider value from environment
export const getRawAuthProvider = (): string => {
  return import.meta.env.VITE_AUTH_PROVIDER || '';
};

// Get the active auth provider from environment.
//
// Desktop (Electron) always uses Auth0 — the main process owns the
// system-browser PKCE flow and the renderer routes through
// ElectronAuthContextProvider. Env-var-driven selection only applies to
// web builds.
export const getAuthProvider = (): AuthProvider => {
  if (isElectron) return 'auth0';

  const provider = import.meta.env.VITE_AUTH_PROVIDER as string;
  if (provider === 'auth0') return 'auth0';
  if (provider === 'keycloak') return 'keycloak';
  if (provider === 'mock') return 'mock';
  return 'azure-ad'; // Default to Azure AD
};

// Validate the authentication configuration.
//
// Desktop bypasses renderer-env validation entirely. Auth0 domain /
// client_id / audience for the PKCE flow are baked into the main
// process at build time (src/Aonik.AdminDesktop/electron.vite.config.ts
// define block) and the renderer never instantiates an IdP SDK directly
// — so missing VITE_AUTH0_* in the renderer bundle is harmless on
// desktop. Surfacing the "Authentication Not Configured" page in that
// case is a dead-end for the user; treat the desktop config as always
// valid and let ElectronAuthContextProvider drive sign-in.
export const validateAuthConfig = (): ConfigValidationResult => {
  if (isElectron) {
    return { isValid: true, provider: 'auth0', missingFields: [] };
  }

  const rawProvider = getRawAuthProvider();
  const provider = getAuthProvider();
  const missingFields: string[] = [];

  // Check if provider is explicitly set
  if (!rawProvider) {
    missingFields.push('VITE_AUTH_PROVIDER (set to "azure-ad", "auth0", "keycloak", or "mock")');
  } else if (
    rawProvider !== 'azure-ad' &&
    rawProvider !== 'auth0' &&
    rawProvider !== 'keycloak' &&
    rawProvider !== 'mock'
  ) {
    return {
      isValid: false,
      provider: null,
      missingFields: [],
      error: `Invalid auth provider: "${rawProvider}". Must be "azure-ad", "auth0", "keycloak", or "mock".`,
    };
  }

  // Mock provider doesn't need any configuration
  if (provider === 'mock') {
    return {
      isValid: true,
      provider,
      missingFields: [],
    };
  }

  // Validate Azure AD config
  if (provider === 'azure-ad') {
    if (!import.meta.env.VITE_AZURE_AD_CLIENT_ID) {
      missingFields.push('VITE_AZURE_AD_CLIENT_ID');
    }
    if (!import.meta.env.VITE_AZURE_AD_TENANT_ID) {
      missingFields.push('VITE_AZURE_AD_TENANT_ID');
    }
  }

  // Validate Auth0 config
  if (provider === 'auth0') {
    if (!import.meta.env.VITE_AUTH0_DOMAIN) {
      missingFields.push('VITE_AUTH0_DOMAIN');
    }
    if (!import.meta.env.VITE_AUTH0_CLIENT_ID) {
      missingFields.push('VITE_AUTH0_CLIENT_ID');
    }
    if (!import.meta.env.VITE_AUTH0_AUDIENCE) {
      missingFields.push('VITE_AUTH0_AUDIENCE');
    }
  }

  // Validate Keycloak config — Spec 029.
  //
  // VITE_KEYCLOAK_AUTHORITY is the full realm URL
  // (e.g. https://keycloak.example.com/realms/aonik); discovery resolves the
  // authorization/token/userinfo endpoints from there. VITE_KEYCLOAK_AUDIENCE
  // is optional — if the realm's `aonik-spa` client emits the audience via a
  // protocol mapper, the SPA doesn't need to know about it, but a separate
  // backend `Auth.Keycloak.Audience` setting must still match the mapped value.
  if (provider === 'keycloak') {
    if (!import.meta.env.VITE_KEYCLOAK_AUTHORITY) {
      missingFields.push('VITE_KEYCLOAK_AUTHORITY');
    }
    if (!import.meta.env.VITE_KEYCLOAK_CLIENT_ID) {
      missingFields.push('VITE_KEYCLOAK_CLIENT_ID');
    }
  }

  return {
    isValid: missingFields.length === 0,
    provider,
    missingFields,
  };
};

// Get provider display name
export const getProviderDisplayName = (provider: AuthProvider): string => {
  if (provider === 'azure-ad') return 'Microsoft Entra ID';
  if (provider === 'auth0') return 'Auth0';
  if (provider === 'keycloak') return 'Keycloak';
  return 'Mock (Development)';
};

// Azure AD (Entra ID) Configuration
export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_AZURE_AD_CLIENT_ID || '',
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_AZURE_AD_TENANT_ID || 'common'}`,
    redirectUri: import.meta.env.VITE_AZURE_AD_REDIRECT_URI || window.location.origin,
    postLogoutRedirectUri: window.location.origin,
    navigateToLoginRequestUrl: true,
  },
  cache: {
    cacheLocation: 'localStorage',
    storeAuthStateInCookie: false,
  },
  system: {
    loggerOptions: {
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) return;
        switch (level) {
          case LogLevel.Error:
            console.error(message);
            break;
          case LogLevel.Warning:
            console.warn(message);
            break;
          case LogLevel.Info:
            // console.info(message);
            break;
          case LogLevel.Verbose:
            // console.debug(message);
            break;
        }
      },
      logLevel: LogLevel.Warning,
    },
  },
};

// Azure AD login request scopes
export const msalLoginRequest = {
  scopes: [
    'openid',
    'profile',
    'email',
    import.meta.env.VITE_AZURE_AD_API_SCOPE || 'api://aonik/.default',
  ].filter(Boolean),
};

// Azure AD API token request
export const msalApiTokenRequest = {
  scopes: [import.meta.env.VITE_AZURE_AD_API_SCOPE || 'api://aonik/.default'].filter(Boolean),
};

// Auth0 Configuration
const auth0Audience = import.meta.env.VITE_AUTH0_AUDIENCE;

export const auth0Config = {
  domain: import.meta.env.VITE_AUTH0_DOMAIN || '',
  clientId: import.meta.env.VITE_AUTH0_CLIENT_ID || '',
  authorizationParams: {
    redirect_uri: import.meta.env.VITE_AUTH0_REDIRECT_URI || window.location.origin,
    ...(auth0Audience ? { audience: auth0Audience } : {}),
    scope: 'openid profile email offline_access',
  },
  cacheLocation: 'localstorage' as const,
  useRefreshTokens: true,
  useRefreshTokensFallback: true,
};

// Keycloak Configuration — Spec 029.
//
// Authority is the full realm URL (e.g. https://keycloak.example.com/realms/aonik).
// The oidc-client-ts UserManager fetches OIDC discovery from that authority and
// resolves authorization/token/userinfo/logout endpoints from the metadata, so
// none of those need to live in env vars. We request offline_access so refresh
// tokens are issued; the realm's `aonik-spa` client must enable that scope.
export const keycloakConfig = {
  authority: import.meta.env.VITE_KEYCLOAK_AUTHORITY || '',
  client_id: import.meta.env.VITE_KEYCLOAK_CLIENT_ID || '',
  // SPA client typically has no secret (public PKCE flow). If the operator
  // chose a confidential client for some reason, VITE_KEYCLOAK_CLIENT_SECRET
  // can carry one — generally NOT recommended for browser-resident SPAs.
  client_secret: import.meta.env.VITE_KEYCLOAK_CLIENT_SECRET || undefined,
  redirect_uri: import.meta.env.VITE_KEYCLOAK_REDIRECT_URI || window.location.origin,
  post_logout_redirect_uri:
    import.meta.env.VITE_KEYCLOAK_POST_LOGOUT_REDIRECT_URI || window.location.origin,
  scope: 'openid profile email offline_access',
  // Keep tokens in localStorage so refresh works across tabs and full reloads,
  // mirroring the Auth0 cacheLocation choice. react-oidc-context exposes this
  // through a WebStorageStateStore — see AuthProvider.tsx where the UserManager
  // options are passed.
  loadUserInfo: false,
};

// API Configuration
export const apiConfig = {
  baseUrl: import.meta.env.VITE_API_BASE_URL || '/api',
};
