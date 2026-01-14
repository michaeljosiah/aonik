import { type Configuration, LogLevel } from '@azure/msal-browser';

// Auth provider type - determined by environment variable
export type AuthProvider = 'azure-ad' | 'auth0';

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

// Get the active auth provider from environment
export const getAuthProvider = (): AuthProvider => {
  const provider = import.meta.env.VITE_AUTH_PROVIDER as string;
  if (provider === 'auth0') return 'auth0';
  return 'azure-ad'; // Default to Azure AD
};

// Validate the authentication configuration
export const validateAuthConfig = (): ConfigValidationResult => {
  const rawProvider = getRawAuthProvider();
  const provider = getAuthProvider();
  const missingFields: string[] = [];

  // Check if provider is explicitly set
  if (!rawProvider) {
    missingFields.push('VITE_AUTH_PROVIDER (set to "azure-ad" or "auth0")');
  } else if (rawProvider !== 'azure-ad' && rawProvider !== 'auth0') {
    return {
      isValid: false,
      provider: null,
      missingFields: [],
      error: `Invalid auth provider: "${rawProvider}". Must be "azure-ad" or "auth0".`,
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
  }

  return {
    isValid: missingFields.length === 0,
    provider,
    missingFields,
  };
};

// Get provider display name
export const getProviderDisplayName = (provider: AuthProvider): string => {
  return provider === 'azure-ad' ? 'Microsoft Entra ID' : 'Auth0';
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
export const auth0Config = {
  domain: import.meta.env.VITE_AUTH0_DOMAIN || '',
  clientId: import.meta.env.VITE_AUTH0_CLIENT_ID || '',
  authorizationParams: {
    redirect_uri: import.meta.env.VITE_AUTH0_REDIRECT_URI || window.location.origin,
    audience: import.meta.env.VITE_AUTH0_AUDIENCE || '',
    scope: 'openid profile email',
  },
  cacheLocation: 'localstorage' as const,
  useRefreshTokens: true,
};

// API Configuration
export const apiConfig = {
  baseUrl: import.meta.env.VITE_API_BASE_URL || 'https://localhost:5001',
};
