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
// Generation counter. Every invalidation bumps it; a request captures the
// generation it started under and may only write the cache while that
// generation is still current. Without this, a request already in flight
// when a toggle invalidates the cache could finish LAST and re-populate the
// cache with pre-toggle data for another TTL.
let manifestVersion = 0;
const listeners = new Set<() => void>();

/** The tenant the manifest is keyed on; empty string when none is selected. */
export function getManifestTenantKey(): string {
  return getSelectedTenant()?.tenantId ?? '';
}

/**
 * Monotonic generation bumped by every invalidation. Hooks re-fetch on change
 * and in-flight requests older than the current generation never populate
 * the cache.
 */
export function getManifestVersion(): number {
  return manifestVersion;
}

/**
 * The TTL only decides whether the NEXT caller re-fetches. Nothing re-reads on its own, so a module
 * change made by another administrator would never reach an already-mounted layout: the sidebar,
 * routes and agent list would stay as they were for the life of the session while the backend had
 * already started refusing them. So an expiring entry invalidates itself, which wakes every mounted
 * subscriber exactly as a local toggle does. One timer at a time, and none while the tab is hidden
 * (the visibility handler refreshes on the way back instead of accumulating wake-ups).
 */
let expiryTimer: ReturnType<typeof setTimeout> | null = null;

function scheduleExpiryRefresh(): void {
  if (typeof window === 'undefined') return;
  if (expiryTimer !== null) clearTimeout(expiryTimer);
  expiryTimer = setTimeout(() => {
    expiryTimer = null;
    if (typeof document !== 'undefined' && document.visibilityState === 'hidden') return;
    invalidateModuleManifest();
  }, MANIFEST_CACHE_TTL_MS);
}

if (typeof document !== 'undefined') {
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState !== 'visible') return;
    // Back from a hidden tab, where the refresh was deliberately skipped: if the entry has aged out
    // in the meantime, pick up whatever changed while nobody was looking.
    if (manifestCache && Date.now() - manifestCache.timestamp >= MANIFEST_CACHE_TTL_MS) {
      invalidateModuleManifest();
    }
  });
}

/**
 * The manifest currently cached for the selected tenant, or null when there is none yet or it has
 * aged out. For consumers outside React that cannot await a fetch — the workspace registry, which
 * builds its panel and template lists synchronously. Null means fail-open, exactly as elsewhere.
 */
export function getCachedManifest(): RuntimeModuleManifest | null {
  return freshCacheFor(getManifestTenantKey());
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

function freshCacheFor(tenantId: string): RuntimeModuleManifest | null {
  if (
    manifestCache
    && manifestCache.tenantId === tenantId
    && Date.now() - manifestCache.timestamp < MANIFEST_CACHE_TTL_MS
  ) {
    return manifestCache.data;
  }
  return null;
}

/**
 * Fetch the manifest for the currently selected tenant through the shared
 * API client (bearer token + X-Tenant-Id). Single-flight and cached per
 * tenant; a cache entry for another tenant is never served.
 *
 * Resolves `null` on any failure — transport errors, 401/403 from the
 * manifest itself, or an unresolvable tenant — so callers stay fail-open.
 *
 * Generation-guarded: a response that lands after an invalidation is never
 * written to the cache, and its awaiters receive the replacement request's
 * result, so the newest data always wins regardless of completion order.
 */
export function fetchManifestOnce(): Promise<RuntimeModuleManifest | null> {
  const tenantId = getManifestTenantKey();

  const cached = freshCacheFor(tenantId);
  if (cached) {
    return Promise.resolve(cached);
  }

  if (manifestInFlight && manifestInFlight.tenantId === tenantId) {
    return manifestInFlight.promise;
  }

  // The generation this request belongs to. An invalidation while it is in
  // flight makes its response stale: it must not be cached, and callers that
  // awaited it are handed the replacement request's result instead.
  const generation = manifestVersion;

  const promise: Promise<RuntimeModuleManifest | null> = api
    .get<RuntimeModuleManifest>('/admin/manifest')
    .then((data): RuntimeModuleManifest | null | Promise<RuntimeModuleManifest | null> => {
      if (!data || !Array.isArray(data.enabledModules)) {
        return null;
      }
      const normalised: RuntimeModuleManifest = {
        ...data,
        modules: Array.isArray(data.modules) ? data.modules : [],
        featureFlags: data.featureFlags ?? {},
      };

      const currentTenantId = getManifestTenantKey();
      const isCurrent = manifestVersion === generation && currentTenantId === tenantId;
      if (isCurrent) {
        manifestCache = { tenantId, data: normalised, timestamp: Date.now() };
        scheduleExpiryRefresh();
        return normalised;
      }

      // Stale: an invalidation (module toggle, tenant switch, logout) raced
      // this response. Never write the cache. Prefer whatever is newest for
      // the tenant now selected — the replacement request if one is running,
      // else a fresh cache entry — and only fall back to this payload
      // (uncached, fail-open) when there is nothing newer to hand back.
      if (manifestInFlight && manifestInFlight.tenantId === currentTenantId && manifestInFlight.promise !== promise) {
        return manifestInFlight.promise;
      }
      return freshCacheFor(currentTenantId) ?? normalised;
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
