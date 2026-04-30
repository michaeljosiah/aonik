/// <reference types="vite/client" />

interface ImportMetaEnv {
  // Auth Provider
  readonly VITE_AUTH_PROVIDER: 'azure-ad' | 'auth0' | 'mock';

  // Azure AD
  readonly VITE_AZURE_AD_CLIENT_ID: string;
  readonly VITE_AZURE_AD_TENANT_ID: string;
  readonly VITE_AZURE_AD_REDIRECT_URI: string;
  readonly VITE_AZURE_AD_API_SCOPE: string;

  // Auth0
  readonly VITE_AUTH0_DOMAIN: string;
  readonly VITE_AUTH0_CLIENT_ID: string;
  readonly VITE_AUTH0_REDIRECT_URI: string;
  readonly VITE_AUTH0_AUDIENCE: string;

  // API
  readonly VITE_API_BASE_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

// Build-time constant injected by Vite's `define` (see vite.config.ts).
declare const __APP_VERSION__: string;
