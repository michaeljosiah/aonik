import axios, { type AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios';
import { apiConfig } from '@/auth';
import { clearSelectedTenant, getSelectedTenant } from '@/lib/tenantContext';
import { getApiBaseUrlOnce } from '@/lib/electron';

// Create axios instance with base configuration
const apiClient: AxiosInstance = axios.create({
  baseURL: apiConfig.baseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 30000,
});

// In Electron production builds the renderer is served from file://, so the
// relative /api base URL won't resolve. The main process exposes the real
// backend URL over IPC; without awaiting it, the first request fires with
// baseURL `/api` and resolves to `file:///api/...`. Resolve once and apply.
const electronBaseUrlReady = getApiBaseUrlOnce().then((url) => {
  if (url) apiClient.defaults.baseURL = url;
});

// Token getter function - will be set by AuthProvider
let getAccessTokenFn: (() => Promise<string | null>) | null = null;

// Function to set the token getter (called by AuthProvider)
export function setAccessTokenGetter(getter: () => Promise<string | null>) {
  getAccessTokenFn = getter;
}

// Module gate (Spec 097): decides whether a 403 `module.disabled` for `moduleId`
// should navigate to the explanation page. The app shell registers a predicate
// backed by the module registry (which imports every page, so it cannot be
// imported here). Without one, no 403 ever turns into a navigation — the caller
// gets the rejection with a userMessage and renders its own error.
export type ModuleDisabledRedirectPredicate = (moduleId: string, pathname: string) => boolean;

let moduleDisabledRedirectPredicate: ModuleDisabledRedirectPredicate | null = null;

export function setModuleDisabledRedirectPredicate(predicate: ModuleDisabledRedirectPredicate | null) {
  moduleDisabledRedirectPredicate = predicate;
}

export const MODULE_DISABLED_USER_MESSAGE = 'This feature is not enabled for this organisation.';

/** True when the app is running under HashRouter (the packaged desktop build). */
function isHashRouting(): boolean {
  return typeof window !== 'undefined' && window.location.hash.startsWith('#/');
}

/**
 * The route the user is actually on, whichever router is in use: the hash in the desktop build,
 * the pathname on the web. Query and fragment are stripped so callers get a bare path to match.
 */
export function currentRoutePath(): string {
  if (typeof window === 'undefined') return '/';
  if (isHashRouting()) {
    const raw = window.location.hash.slice(1);
    const end = raw.search(/[?#]/);
    return end === -1 ? raw : raw.slice(0, end);
  }
  return window.location.pathname;
}

/** Navigate to an app route without a router dependency, honouring the active routing mode. */
function navigateToRoute(path: string): void {
  if (isHashRouting()) {
    window.location.hash = `#${path}`;
    return;
  }
  window.location.href = path;
}

// Request interceptor to add auth token
apiClient.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    // Block the request until the Electron-provided baseURL has been applied
    // (no-op in the browser — the promise resolves with `null` synchronously).
    await electronBaseUrlReady;

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

    // While the user is on the login page they are by definition
    // unauthenticated — don't fire Auth0 silent-auth for outgoing calls
    // there. Without this guard, `getAccessTokenSilently` would burn
    // several seconds timing out before the actual request fires.
    const onLoginRoute =
      typeof window !== 'undefined' &&
      (window.location.pathname.startsWith('/login') ||
        window.location.hash.startsWith('#/login'));

    // Bootstrap routes are intentionally unauthenticated.
    // Host routes mix public and protected endpoints, so attach the bearer token by
    // default unless a caller already set Authorization explicitly.
    const hasExplicitAuthorization = !!config.headers.Authorization;
    const skipAuth = isBootstrapRoute || onLoginRoute;
    if (!skipAuth && !hasExplicitAuthorization && getAccessTokenFn) {
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
        // Mock auth provider — used by the Claude preview server (.env.mock-preview.local).
        // The mock token is rejected by the real API; suppressing the redirect lets us
        // render protected pages for visual review without a live login flow.
        if (import.meta.env.VITE_AUTH_PROVIDER === 'mock') {
          return;
        }
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
      if (originalRequest && !originalRequest._retry && getAccessTokenFn) {
        originalRequest._retry = true;

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

      // If no token getter is registered yet (app still initializing), don't
      // redirect — the request simply failed before auth was ready.  Callers
      // can handle the rejection.  Only redirect when a getter exists and still
      // could not produce a valid token.
      if (getAccessTokenFn) {
        redirectToLogin('session-expired');
      }
      return Promise.reject({ ...error, userMessage: message });
    }

    // Handle other errors
    if (error.response?.status === 403) {
      console.error('Access forbidden:', error.response.data);

      // Module gate (Spec 097): the tenant does not have this module enabled.
      // Navigate to the explanatory page ONLY when the page the user is on is
      // owned by that module (same full-page navigation the 401 path uses;
      // api.ts has no router dependency). A shared page — the home dashboard,
      // a customer record — that merely called a gated endpoint keeps the
      // rejection and renders its own message; bouncing it would loop.
      const body = error.response.data as { code?: unknown; moduleId?: unknown } | null | undefined;
      if (body && body.code === 'module.disabled' && typeof body.moduleId === 'string' && body.moduleId.length > 0) {
        try {
          // The desktop build runs under HashRouter, where the pathname is the packaged HTML file
          // and the real route lives in the hash. Reading the pathname there would fail the
          // ownership check on every page, and assigning href would navigate away from the renderer
          // rather than change the route — so both the read and the write follow the routing mode.
          const routePath = currentRoutePath();
          const pageBelongsToModule = moduleDisabledRedirectPredicate?.(body.moduleId, routePath) ?? false;
          if (pageBelongsToModule && !routePath.startsWith('/module-disabled')) {
            navigateToRoute(`/module-disabled/${encodeURIComponent(body.moduleId)}`);
          }
        } catch {
          // Navigation is best-effort; the rejection below still carries the message.
        }
        return Promise.reject({ ...error, userMessage: MODULE_DISABLED_USER_MESSAGE, moduleId: body.moduleId });
      }
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
