import { apiConfig } from '@/auth';
import type { BootstrapStatusResponse, BootstrapTenantResult } from '@/types';

// Simple in-memory cache for bootstrap status
let statusCache: { data: BootstrapStatusResponse; timestamp: number } | null = null;
const CACHE_TTL_MS = 30000; // 30 seconds
let statusInFlight: Promise<BootstrapStatusResponse> | null = null;

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

const buildUrl = (path: string): string => {
  const baseUrl = apiConfig.baseUrl.endsWith('/')
    ? apiConfig.baseUrl.slice(0, -1)
    : apiConfig.baseUrl;

  return `${baseUrl}${path}`;
};

const tryGetString = (value: unknown): string | null => {
  if (typeof value !== 'string') return null;
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
};

const resolveErrorMessage = (status: number, payload: unknown): string => {
  const serverMessage =
    tryGetString((payload as { message?: unknown } | null)?.message) ??
    tryGetString((payload as { error?: unknown } | null)?.error) ??
    tryGetString(payload);

  return serverMessage ?? statusMessages[status] ?? 'Something went wrong. Please try again.';
};

const parseResponseBody = async (response: Response): Promise<unknown> => {
  const contentType = response.headers.get('content-type') ?? '';

  if (contentType.includes('application/json')) {
    return response.json();
  }

  return response.text();
};

// Bootstrap endpoints use fetch instead of the shared Axios client because
// cross-origin XHR requests with Authorization are being aborted in ACA while
// the equivalent fetch requests succeed.
const requestBootstrap = async <T>(
  path: string,
  options?: {
    method?: 'GET' | 'POST';
    accessToken?: string | null;
    forceRefresh?: boolean;
  }
): Promise<T> => {
  const headers = new Headers({
    Accept: 'application/json',
  });

  if (options?.accessToken) {
    headers.set('Authorization', `Bearer ${options.accessToken}`);
  }

  let response: Response;

  try {
    response = await fetch(buildUrl(path), {
      method: options?.method ?? 'GET',
      headers,
      cache: options?.forceRefresh ? 'no-store' : 'default',
    });
  } catch {
    throw { userMessage: 'Unable to reach the service. Check your connection and try again.' };
  }

  const payload = await parseResponseBody(response);

  if (!response.ok) {
    throw { userMessage: resolveErrorMessage(response.status, payload) };
  }

  return payload as T;
};

export const bootstrapService = {
  bootstrap: async (accessToken?: string | null): Promise<BootstrapTenantResult> => {
    // Clear cache after bootstrap
    statusCache = null;
    statusInFlight = null;
    return requestBootstrap<BootstrapTenantResult>('/bootstrap', {
      method: 'POST',
      accessToken,
    });
  },
  status: async (forceRefresh = false, accessToken?: string | null): Promise<BootstrapStatusResponse> => {
    // Return cached data if valid
    if (!forceRefresh && statusCache && Date.now() - statusCache.timestamp < CACHE_TTL_MS) {
      return statusCache.data;
    }

    if (statusInFlight) {
      return statusInFlight;
    }

    // Fetch fresh data
    statusInFlight = requestBootstrap<BootstrapStatusResponse>('/bootstrap/status', {
      accessToken,
      forceRefresh,
    })
      .then((data) => {
        statusCache = { data, timestamp: Date.now() };
        return data;
      })
      .finally(() => {
        statusInFlight = null;
      });

    return statusInFlight;
  },
  clearCache: (): void => {
    statusCache = null;
    statusInFlight = null;
  },
};
