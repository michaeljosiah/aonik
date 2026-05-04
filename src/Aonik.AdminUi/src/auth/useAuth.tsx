import { createContext, useContext, useEffect, useState, useCallback, type ReactNode } from 'react';
import { useMsal, useIsAuthenticated as useMsalIsAuthenticated } from '@azure/msal-react';
import { useAuth0 } from '@auth0/auth0-react';
import { InteractionStatus } from '@azure/msal-browser';
import { msalLoginRequest, msalApiTokenRequest, auth0Config, type AuthProvider } from './authConfig';
import { isElectron } from '@/lib/electron';
import { clearSelectedTenant } from '@/lib/tenantContext';

// Unified user type
export interface AuthUser {
  id: string;
  email: string;
  name: string;
  picture?: string;
  roles?: string[];
  roleSource?: 'claims' | 'api';
}

export interface LoginOptions {
  // Forwarded to the IdP as `login_hint` (Auth0) / `loginHint` (MSAL). The
  // mock provider ignores it. Captured up-front by the login page so the
  // user doesn't have to retype their email after the IdP redirect.
  loginHint?: string;
}

// Unified auth context type
export interface AuthContextType {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthUser | null;
  accessToken: string | null;
  provider: AuthProvider;
  authError: Error | null;
  login: (options?: LoginOptions) => Promise<void>;
  logout: () => Promise<void>;
  getAccessToken: () => Promise<string | null>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

// Hook to use auth context
export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

// Mock Auth Hook for development/testing
function useMockAuth(): AuthContextType {
  const [isAuthenticated, setIsAuthenticated] = useState(true);
  const [isLoading] = useState(false);

const mockUser: AuthUser = {
  id: 'mock-user-123',
  email: 'admin@aonik.dev',
  name: 'Dev Admin',
  picture: undefined,
  roles: ['Admin', 'PlatformAdmin'],
  roleSource: 'claims',
};


  const login = useCallback(async (_options?: LoginOptions) => {
    setIsAuthenticated(true);
  }, []);

  const logout = useCallback(async () => {
    clearSelectedTenant();
    setIsAuthenticated(false);
  }, []);

  const getAccessToken = useCallback(async (): Promise<string | null> => {
    return 'mock-access-token-for-development';
  }, []);

  return {
    isAuthenticated,
    isLoading,
    user: isAuthenticated ? mockUser : null,
    accessToken: isAuthenticated ? 'mock-access-token-for-development' : null,
    provider: 'mock',
    authError: null,
    login,
    logout,
    getAccessToken,
  };
}

// Azure AD Auth Hook
function useMsalAuth(): AuthContextType {
  const { instance, accounts, inProgress } = useMsal();
  const isAuthenticated = useMsalIsAuthenticated();
  const [accessToken, setAccessToken] = useState<string | null>(null);

  const user: AuthUser | null = accounts[0]
    ? {
        id: accounts[0].localAccountId || accounts[0].homeAccountId,
        email: accounts[0].username,
        name: accounts[0].name || accounts[0].username,
        picture: undefined,
        roles: (accounts[0].idTokenClaims?.roles as string[]) || [],
        roleSource: 'claims',
      }
    : null;

  const login = useCallback(async (options?: LoginOptions) => {
    try {
      const request = options?.loginHint
        ? { ...msalLoginRequest, loginHint: options.loginHint }
        : msalLoginRequest;
      if (isElectron) {
        await instance.loginPopup(request);
      } else {
        await instance.loginRedirect(request);
      }
    } catch (error) {
      console.error('MSAL login error:', error);
      throw error;
    }
  }, [instance]);

  const logout = useCallback(async () => {
    try {
      // Mirror the Auth0 logout: clear the selected tenant before redirecting
      // so the next sign-in starts from a clean slate.
      clearSelectedTenant();

      if (isElectron) {
        await instance.logoutPopup({ postLogoutRedirectUri: window.location.origin });
      } else {
        await instance.logoutRedirect({
          postLogoutRedirectUri: window.location.origin,
        });
      }
    } catch (error) {
      console.error('MSAL logout error:', error);
      throw error;
    }
  }, [instance]);

  const getAccessToken = useCallback(async (): Promise<string | null> => {
    if (!accounts[0]) return null;
    try {
      const response = await instance.acquireTokenSilent({
        ...msalApiTokenRequest,
        account: accounts[0],
      });
      setAccessToken(response.accessToken);
      return response.accessToken;
    } catch (error) {
      console.error('MSAL token acquisition error:', error);
      // Try interactive token acquisition
      try {
        const response = await instance.acquireTokenPopup(msalApiTokenRequest);
        setAccessToken(response.accessToken);
        return response.accessToken;
      } catch (popupError) {
        console.error('MSAL popup token acquisition error:', popupError);
        return null;
      }
    }
  }, [instance, accounts]);

  // Acquire token on mount if authenticated
  useEffect(() => {
    if (isAuthenticated && accounts[0]) {
      getAccessToken();
    }
  }, [isAuthenticated, accounts, getAccessToken]);

  return {
    isAuthenticated,
    isLoading: inProgress !== InteractionStatus.None,
    user,
    accessToken,
    provider: 'azure-ad',
    authError: null,
    login,
    logout,
    getAccessToken,
  };
}

// Auth0 Auth Hook
function useAuth0Auth(): AuthContextType {
  const {
    isAuthenticated,
    isLoading,
    user: auth0User,
    loginWithRedirect,
    loginWithPopup,
    logout: auth0Logout,
    getAccessTokenSilently,
    error: auth0Error,
  } = useAuth0();
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const consentPromptKey = 'aonik:auth0:consent_prompted';
  const loginPromptKey = 'aonik:auth0:login_prompted';

  const isConsentRequiredError = (err: unknown): boolean => {
    if (!err) return false;
    if (typeof err === 'string') {
      const lower = err.toLowerCase();
      return lower.includes('consent required') || lower.includes('consent_required');
    }
    if (typeof err === 'object') {
      const typed = err as { error?: string; error_description?: string; message?: string };
      const errorCode = typed.error ?? '';
      const message = typed.message ?? typed.error_description ?? '';
      const combined = `${errorCode} ${message}`.toLowerCase();
      return combined.includes('consent required') || combined.includes('consent_required');
    }
    return false;
  };

  const isLoginRequiredError = (err: unknown): boolean => {
    if (!err) return false;
    if (typeof err === 'string') {
      const lower = err.toLowerCase();
      return lower.includes('login required') || lower.includes('login_required');
    }
    if (typeof err === 'object') {
      const typed = err as { error?: string; error_description?: string; message?: string };
      const errorCode = typed.error ?? '';
      const message = typed.message ?? typed.error_description ?? '';
      const combined = `${errorCode} ${message}`.toLowerCase();
      return combined.includes('login required') || combined.includes('login_required');
    }
    return false;
  };

  const user: AuthUser | null = auth0User
    ? {
        id: auth0User.sub || '',
        email: auth0User.email || '',
        name: auth0User.name || auth0User.email || '',
        picture: auth0User.picture,
        roles: (auth0User['https://aonik.com/roles'] as string[]) || [],
        roleSource: 'claims',
      }
    : null;

  const login = useCallback(async (options?: LoginOptions) => {
    try {
      const authorizationParams = {
        ...auth0Config.authorizationParams,
        ...(options?.loginHint ? { login_hint: options.loginHint } : {}),
      };
      if (isElectron) {
        await loginWithPopup({ authorizationParams });
      } else {
        await loginWithRedirect({ authorizationParams });
      }
    } catch (error) {
      console.error('Auth0 login error:', error);
      throw error;
    }
  }, [loginWithRedirect, loginWithPopup]);

  const logout = useCallback(async () => {
    try {
      // Clear the selected tenant BEFORE redirecting to Auth0. Otherwise
      // the next user who logs in on this device inherits the previous
      // operator's tenant context — they sign in successfully at Auth0,
      // get bounced back with a token, and then every API call carries
      // X-Tenant-Id pointing at a tenant they have no membership in,
      // producing a confusing wall of 401s and a "session expired" loop
      // that's actually a tenant-mismatch.
      clearSelectedTenant();

      if (isElectron) {
        await auth0Logout({ openUrl: false });
      } else {
        await auth0Logout({
          logoutParams: {
            returnTo: window.location.origin,
          },
        });
      }
    } catch (error) {
      console.error('Auth0 logout error:', error);
      throw error;
    }
  }, [auth0Logout]);

  const getAccessToken = useCallback(async (): Promise<string | null> => {
    // Do NOT gate on isLoading/isAuthenticated here. Those values are captured
    // at the time this callback is created, so a stale closure causes the
    // getter to return null even after Auth0 has finished processing.
    // getAccessTokenSilently handles its own state checks internally and will
    // throw if the user is not authenticated.
    try {
      const token = await getAccessTokenSilently({ authorizationParams: auth0Config.authorizationParams });
      setAccessToken(token);
      return token;
    } catch (error) {
      // Skip the auto-redirect when the user is already on the login page.
      // getAccessToken is called by the api request interceptor on every
      // outbound call, including the public listForLogin fetch the
      // LoginPage fires on mount. Without this guard we'd race the user
      // and bounce to Auth0 before they ever click Sign in.
      const onLoginPage =
        typeof window !== 'undefined' && window.location.pathname.startsWith('/login');

      if (isLoginRequiredError(error)) {
        setAccessToken(null);
        if (onLoginPage) {
          return null;
        }
        try {
          if (!sessionStorage.getItem(loginPromptKey)) {
            sessionStorage.setItem(loginPromptKey, 'true');
            await loginWithRedirect({
              authorizationParams: {
                ...auth0Config.authorizationParams,
                prompt: 'login',
              },
            });
          }
        } catch (loginError) {
          console.error('Auth0 login redirect error:', loginError);
        }
        return null;
      }
      if (isConsentRequiredError(error)) {
        if (onLoginPage) {
          return null;
        }
        try {
          if (!sessionStorage.getItem(consentPromptKey)) {
            sessionStorage.setItem(consentPromptKey, 'true');
            await loginWithRedirect({
              authorizationParams: {
                ...auth0Config.authorizationParams,
                prompt: 'consent',
              },
            });
          }
        } catch (consentError) {
          console.error('Auth0 consent redirect error:', consentError);
        }
        return null;
      }
      console.error('Auth0 token acquisition error:', error);
      return null;
    }
  }, [getAccessTokenSilently, loginWithRedirect]);

  // Acquire token on mount if authenticated
  useEffect(() => {
    if (isAuthenticated) {
      getAccessToken();
    }
  }, [isAuthenticated, getAccessToken]);

  return {
    isAuthenticated,
    isLoading,
    user,
    accessToken,
    provider: 'auth0',
    authError: auth0Error ?? null,
    login,
    logout,
    getAccessToken,
  };
}

// Wrapper for MSAL that provides context
function MsalAuthContextProvider({ children }: { children: ReactNode }) {
  const auth = useMsalAuth();
  return <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>;
}

// Wrapper for Auth0 that provides context
function Auth0AuthContextProvider({ children }: { children: ReactNode }) {
  const auth = useAuth0Auth();
  return <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>;
}

// Wrapper for Mock auth that provides context
function MockAuthContextProvider({ children }: { children: ReactNode }) {
  const auth = useMockAuth();
  return <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>;
}

export { AuthContext, MsalAuthContextProvider, Auth0AuthContextProvider, MockAuthContextProvider };
