import { createContext, useContext, useEffect, useState, useCallback, type ReactNode } from 'react';
import { useMsal, useIsAuthenticated as useMsalIsAuthenticated } from '@azure/msal-react';
import { useAuth0 } from '@auth0/auth0-react';
import { InteractionStatus } from '@azure/msal-browser';
import { msalLoginRequest, msalApiTokenRequest, type AuthProvider } from './authConfig';

// Unified user type
export interface AuthUser {
  id: string;
  email: string;
  name: string;
  picture?: string;
  roles?: string[];
}

// Unified auth context type
export interface AuthContextType {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthUser | null;
  accessToken: string | null;
  provider: AuthProvider;
  login: () => Promise<void>;
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
      }
    : null;

  const login = useCallback(async () => {
    try {
      await instance.loginRedirect(msalLoginRequest);
    } catch (error) {
      console.error('MSAL login error:', error);
      throw error;
    }
  }, [instance]);

  const logout = useCallback(async () => {
    try {
      await instance.logoutRedirect({
        postLogoutRedirectUri: window.location.origin,
      });
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
    logout: auth0Logout,
    getAccessTokenSilently,
  } = useAuth0();
  const [accessToken, setAccessToken] = useState<string | null>(null);

  const user: AuthUser | null = auth0User
    ? {
        id: auth0User.sub || '',
        email: auth0User.email || '',
        name: auth0User.name || auth0User.email || '',
        picture: auth0User.picture,
        roles: (auth0User['https://aonik.com/roles'] as string[]) || [],
      }
    : null;

  const login = useCallback(async () => {
    try {
      await loginWithRedirect();
    } catch (error) {
      console.error('Auth0 login error:', error);
      throw error;
    }
  }, [loginWithRedirect]);

  const logout = useCallback(async () => {
    try {
      await auth0Logout({
        logoutParams: {
          returnTo: window.location.origin,
        },
      });
    } catch (error) {
      console.error('Auth0 logout error:', error);
      throw error;
    }
  }, [auth0Logout]);

  const getAccessToken = useCallback(async (): Promise<string | null> => {
    try {
      const token = await getAccessTokenSilently();
      setAccessToken(token);
      return token;
    } catch (error) {
      console.error('Auth0 token acquisition error:', error);
      return null;
    }
  }, [getAccessTokenSilently]);

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

export { AuthContext, MsalAuthContextProvider, Auth0AuthContextProvider };
