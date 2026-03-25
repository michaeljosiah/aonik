import { type ReactNode, useState, useEffect } from 'react';
import { PublicClientApplication } from '@azure/msal-browser';
import { MsalProvider } from '@azure/msal-react';
import { Auth0Provider } from '@auth0/auth0-react';
import { 
  getAuthProvider, 
  msalConfig, 
  auth0Config, 
  validateAuthConfig,
  getProviderDisplayName,
  getRawAuthProvider 
} from './authConfig';
import { MsalAuthContextProvider, Auth0AuthContextProvider, MockAuthContextProvider } from './useAuth';
import { AuthError, AuthErrors, type AuthErrorInfo } from '@/components/AuthError';

interface AuthProviderProps {
  children: ReactNode;
}

// Loading component
function AuthLoading() {
  return (
    <div 
      style={{ 
        display: 'flex', 
        alignItems: 'center', 
        justifyContent: 'center', 
        minHeight: '100vh', 
        backgroundColor: 'var(--color-gray-100, #F8F9FA)' 
      }}
    >
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '16px' }}>
        <div 
          style={{ 
            width: '40px', 
            height: '40px', 
            border: '4px solid var(--color-brand-primary, #055a60)', 
            borderTopColor: 'transparent', 
            borderRadius: '50%', 
            animation: 'spin 1s linear infinite' 
          }} 
        />
        <p style={{ fontSize: '14px', color: 'var(--color-text-secondary, #6B7280)' }}>Initializing authentication...</p>
      </div>
      <style>{`
        @keyframes spin {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
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
    return <AuthLoading />;
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

  if (provider === 'auth0') {
    return <Auth0AuthProvider>{children}</Auth0AuthProvider>;
  }

  return <AzureAdAuthProvider>{children}</AzureAdAuthProvider>;
}
