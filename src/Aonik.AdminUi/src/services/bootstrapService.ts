import { apiConfig } from '@/auth';
import type { BootstrapStatusResponse, BootstrapTenantResult } from '@/types';

interface BootstrapInitializeRequest {
  setupSecret: string;
  ownerEmail: string;
  ownerDisplayName?: string | null;
}

const statusCache = new Map<string, { data: BootstrapStatusResponse; timestamp: number }>();
const CACHE_TTL_MS = 30000; // 30 seconds
const statusInFlight = new Map<string, Promise<BootstrapStatusResponse>>();

const statusMessages: Record<number, string> = {
  400: 'The request was invalid. Check your inputs and try again.',
  401: 'Authentication failed for this request.',
  403: 'Bootstrap access was denied. Verify the install code and bootstrap configuration.',
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

const getStatusCacheKey = (): string => 'bootstrap-status';

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

// Bootstrap endpoints use fetch instead of the shared Axios client so the
// first-run install flow stays isolated from normal auth/tenant headers.
const requestBootstrap = async <T>(
  path: string,
  options?: {
    method?: 'GET' | 'POST';
    forceRefresh?: boolean;
    body?: object;
  }
): Promise<T> => {
  const headers = new Headers({
    Accept: 'application/json',
  });

  if (options?.body) {
    headers.set('Content-Type', 'application/json');
  }

  let response: Response;

  try {
    response = await fetch(buildUrl(path), {
      method: options?.method ?? 'GET',
      headers,
      cache: options?.forceRefresh ? 'no-store' : 'default',
      body: options?.body ? JSON.stringify(options.body) : undefined,
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
  bootstrap: async (request: BootstrapInitializeRequest): Promise<BootstrapTenantResult> => {
    // Clear cache after bootstrap
    statusCache.clear();
    statusInFlight.clear();
    return requestBootstrap<BootstrapTenantResult>('/bootstrap', {
      method: 'POST',
      body: request,
    });
  },
  status: async (forceRefresh = false): Promise<BootstrapStatusResponse> => {
    const cacheKey = getStatusCacheKey();
    const cached = statusCache.get(cacheKey);

    // Return cached data if valid
    if (!forceRefresh && cached && Date.now() - cached.timestamp < CACHE_TTL_MS) {
      return cached.data;
    }

    const inFlight = statusInFlight.get(cacheKey);
    if (inFlight) {
      return inFlight;
    }

    // Fetch fresh data
    const request = requestBootstrap<BootstrapStatusResponse>('/bootstrap/status', {
      forceRefresh,
    })
      .then((data) => {
        statusCache.set(cacheKey, { data, timestamp: Date.now() });
        return data;
      })
      .finally(() => {
        statusInFlight.delete(cacheKey);
      });

    statusInFlight.set(cacheKey, request);

    return request;
  },
  clearCache: (): void => {
    statusCache.clear();
    statusInFlight.clear();
  },
};
