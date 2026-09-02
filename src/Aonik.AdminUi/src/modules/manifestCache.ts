import type { RuntimeModuleManifest } from './types';
import { api } from '@/lib/api';
import { getSelectedTenant } from '@/lib/tenantContext';

// ---------------------------------------------------------------------------
// Runtime manifest cache (Spec 097 §8).
//
// Deliberately free of any import from the module registry so that auth,
// the org picker and the sidebar can invalidate the cache without pulling
// every page module into their import graph (see the initialisation-cycle
// note in components/layout/aonik/index.ts).
// ---------------------------------------------------------------------------

const MANIFEST_CACHE_TTL_MS = 30_000;

interface CacheEntry {
  tenantId: string;
  data: RuntimeModuleManifest;
  timestamp: number;
}

interface InFlightEntry {
  tenantId: string;
  promise: Promise<RuntimeModuleManifest | null>;
}

let manifestCache: CacheEntry | null = null;
let manifestInFlight: InFlightEntry | null = null;
let manifestVersion = 0;
const listeners = new Set<() => void>();

/** The tenant the manifest is keyed on; empty string when none is selected. */
export function getManifestTenantKey(): string {
  return getSelectedTenant()?.tenantId ?? '';
}

/** Monotonic counter bumped by every invalidation. Hooks re-fetch on change. */
export function getManifestVersion(): number {
  return manifestVersion;
}

/** Subscribe to invalidations. Returns the unsubscribe function. */
export function subscribeManifest(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/**
 * Drop the cached manifest (and any in-flight request) so the next read
 * hits the API. Call on tenant switch, logout, and after a successful
 * module toggle.
 */
export function invalidateModuleManifest(): void {
  manifestCache = null;
  manifestInFlight = null;
  manifestVersion += 1;
  for (const listener of Array.from(listeners)) {
    try {
      listener();
    } catch {
      // A misbehaving subscriber must not break invalidation for the rest.
    }
  }
}

/**
 * Fetch the manifest for the currently selected tenant through the shared
 * API client (bearer token + X-Tenant-Id). Single-flight and cached per
 * tenant; a cache entry for another tenant is never served.
 *
 * Resolves `null` on any failure — transport errors, 401/403 from the
 * manifest itself, or an unresolvable tenant — so callers stay fail-open.
 */
export function fetchManifestOnce(): Promise<RuntimeModuleManifest | null> {
  const tenantId = getManifestTenantKey();

  if (
    manifestCache
    && manifestCache.tenantId === tenantId
    && Date.now() - manifestCache.timestamp < MANIFEST_CACHE_TTL_MS
  ) {
    return Promise.resolve(manifestCache.data);
  }

  if (manifestInFlight && manifestInFlight.tenantId === tenantId) {
    return manifestInFlight.promise;
  }

  const promise = api
    .get<RuntimeModuleManifest>('/admin/manifest')
    .then((data) => {
      if (!data || !Array.isArray(data.enabledModules)) {
        return null;
      }
      const normalised: RuntimeModuleManifest = {
        ...data,
        modules: Array.isArray(data.modules) ? data.modules : [],
        featureFlags: data.featureFlags ?? {},
      };
      // Only cache when the selected tenant has not changed underneath us.
      if (getManifestTenantKey() === tenantId) {
        manifestCache = { tenantId, data: normalised, timestamp: Date.now() };
      }
      return normalised;
    })
    .catch(() => null)
    .finally(() => {
      if (manifestInFlight && manifestInFlight.tenantId === tenantId && manifestInFlight.promise === promise) {
        manifestInFlight = null;
      }
    });

  manifestInFlight = { tenantId, promise };
  return promise;
}
