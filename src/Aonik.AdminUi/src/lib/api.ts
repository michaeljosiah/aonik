import axios, { type AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios';
import { apiConfig } from '@/auth';
import { clearSelectedTenant, getSelectedTenant } from '@/lib/tenantContext';
import { isElectron, electronAPI } from '@/lib/electron';

// Create axios instance with base configuration
const apiClient: AxiosInstance = axios.create({
  baseURL: apiConfig.baseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 30000,
});

// In Electron production builds the renderer is served from file://, so the
// relative /api base URL won't resolve. Override with the backend URL provided
// by the main process via IPC.
if (isElectron) {
  electronAPI.getApiBaseUrl().then((url: string) => {
    if (url) apiClient.defaults.baseURL = url;
  });
}

// Token getter function - will be set by AuthProvider
let getAccessTokenFn: (() => Promise<string | null>) | null = null;

// Function to set the token getter (called by AuthProvider)
export function setAccessTokenGetter(getter: () => Promise<string | null>) {
  getAccessTokenFn = getter;
}

// Request interceptor to add auth token
apiClient.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    // Attach tenant context header for tenant-scoped routes.
    // (/host/* and /bootstrap/* bypass tenant middleware; avoid sending there to reduce confusion.)
    const url = config.url ?? '';
    const isHostRoute = url.startsWith('/host');
    const isBootstrapRoute = url.startsWith('/bootstrap');

    if (!isHostRoute && !isBootstrapRoute) {
      const selectedTenant = getSelectedTenant();
      if (selectedTenant?.tenantId) {
        config.headers['X-Tenant-Id'] = config.headers['X-Tenant-Id'] ?? selectedTenant.tenantId;
      }
    }

    // Bootstrap routes are intentionally unauthenticated.
    // Host routes mix public and protected endpoints, so attach the bearer token by
    // default unless a caller already set Authorization explicitly.
    const hasExplicitAuthorization = !!config.headers.Authorization;
    if (!isBootstrapRoute && !hasExplicitAuthorization && getAccessTokenFn) {
      try {
        const token = await getAccessTokenFn();
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
      } catch (error) {
        console.error('Error getting access token:', error);
      }
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

const statusMessages: Record<number, string> = {
  400: 'The request was invalid. Check your inputs and try again.',
  401: 'You are not signed in or your session expired. Please sign in and try again.',
  403: 'Access denied. You do not have permission to perform this action. Contact your administrator if you believe this is an error.',
  404: 'We could not find what you requested.',
  409: 'This request could not be completed because of a conflict.',
  422: 'Some of the provided data is not valid. Please review and try again.',
  429: 'Too many requests. Please wait a moment and try again.',
  500: 'Something went wrong on our side. Please try again shortly.',
  502: 'The service is unavailable right now. Please try again shortly.',
  503: 'The service is unavailable right now. Please try again shortly.',
  504: 'The request timed out. Please try again.',
};

const tryGetString = (value: unknown): string | null => {
  if (typeof value !== 'string') return null;
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
};

const tryGetNestedErrorMessage = (data: unknown): string | null => {
  if (!data || typeof data !== 'object') {
    return null;
  }

  const errors = (data as { errors?: unknown }).errors;
  if (!errors || typeof errors !== 'object') {
    return null;
  }

  const generalErrors = (errors as { generalErrors?: unknown }).generalErrors;
  if (Array.isArray(generalErrors)) {
    const firstGeneralError = generalErrors.map(tryGetString).find(Boolean);
    if (firstGeneralError) {
      return firstGeneralError;
    }
  }

  for (const value of Object.values(errors as Record<string, unknown>)) {
    if (!Array.isArray(value)) {
      continue;
    }

    const firstMessage = value.map(tryGetString).find(Boolean);
    if (firstMessage) {
      return firstMessage;
    }
  }

  return null;
};

const resolveErrorMessage = (error: AxiosError): string => {
  const status = error.response?.status;
  if (!status) {
    return 'Unable to reach the service. Check your connection and try again.';
  }

  const data = error.response?.data as unknown;

  // Prefer server-provided message/error fields when available.
  const serverMessage =
    tryGetNestedErrorMessage(data) ??
    tryGetString((data as { message?: unknown } | null)?.message) ??
    tryGetString((data as { error?: unknown } | null)?.error) ??
    tryGetString(data);

  if (serverMessage) {
    if (status === 401 && serverMessage.toLowerCase().includes('tenant context missing')) {
      return 'Tenant context is missing. This request requires a tenant context (tenant claim/header/subdomain). Try signing out/in; if it persists, contact an admin.';
    }

    return serverMessage;
  }

  return statusMessages[status] ?? 'Something went wrong. Please try again.';
};

// Response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined;

    const redirectToLogin = (reason?: 'session-expired' | 'tenant-missing') => {
      try {
        if (window.location.pathname.startsWith('/login')) {
          return;
        }
        const currentUrl = window.location.pathname + window.location.search + window.location.hash;
        const params = new URLSearchParams();
        if (reason) params.set('reason', reason);
        params.set('returnTo', currentUrl);
        window.location.href = `/login?${params.toString()}`;
      } catch {
        if (!window.location.pathname.startsWith('/login')) {
          window.location.href = '/login';
        }
      }
    };

    // Handle 401 Unauthorized
    if (error.response?.status === 401) {
      const requestUrl = typeof originalRequest?.url === 'string' ? originalRequest.url : '';
      if (requestUrl.startsWith('/host') || requestUrl.startsWith('/bootstrap')) {
        return Promise.reject(error);
      }
      const message = resolveErrorMessage(error);
      const isTenantMissing = message.toLowerCase().includes('tenant context is missing')
        || message.toLowerCase().includes('tenant context missing');

      // If tenant context is missing, we can't fix this with a token retry.
      // Clear the stored tenant selection so the login flow can re-prompt.
      if (isTenantMissing) {
        clearSelectedTenant();
        redirectToLogin('tenant-missing');
        return Promise.reject({ ...error, userMessage: message });
      }

      // Token might be missing/expired. Retry once with a fresh token.
      if (originalRequest && !originalRequest._retry) {
        originalRequest._retry = true;

        if (getAccessTokenFn) {
          try {
            const token = await getAccessTokenFn();
            if (token) {
              originalRequest.headers.Authorization = `Bearer ${token}`;
              return apiClient(originalRequest);
            }
          } catch (refreshError) {
            console.error('Error getting access token:', refreshError);
          }
        }
      }

      // If we still don't have a valid token/session, force re-auth.
      redirectToLogin('session-expired');
      return Promise.reject({ ...error, userMessage: message });
    }

    // Handle other errors
    if (error.response?.status === 403) {
      console.error('Access forbidden:', error.response.data);
    }

    if (error.response?.status === 404) {
      console.error('Resource not found:', error.response.data);
    }

    if (error.response?.status && error.response.status >= 500) {
      console.error('Server error:', error.response.data);
    }

    const message = resolveErrorMessage(error);
    return Promise.reject({ ...error, userMessage: message });
  }
);

// API helper functions
export const api = {
  // GET request
  get: <T>(url: string, config?: object) => 
    apiClient.get<T>(url, config).then((res) => res.data),

  // POST request
  post: <T>(url: string, data?: object, config?: object) =>
    apiClient.post<T>(url, data, config).then((res) => res.data),

  // PUT request
  put: <T>(url: string, data?: object, config?: object) =>
    apiClient.put<T>(url, data, config).then((res) => res.data),

  // PATCH request
  patch: <T>(url: string, data?: object, config?: object) =>
    apiClient.patch<T>(url, data, config).then((res) => res.data),

  // DELETE request
  delete: <T>(url: string, config?: object) =>
    apiClient.delete<T>(url, config).then((res) => res.data),
};

export default apiClient;
