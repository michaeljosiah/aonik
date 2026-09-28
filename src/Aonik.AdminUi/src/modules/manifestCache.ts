import type { RuntimeModuleManifest } from './types';
import { api } from '@/lib/api';
import { getSelectedTenant } from '@/lib/tenantContext';

const TTL_MS = 30_000;
let identity = '';
let generation = 0;
let version = 0;
let status: 'idle' | 'loading' | 'ready' | 'error' = 'idle';
let cache: { key: string; data: RuntimeModuleManifest; timestamp: number } | null = null;
let inFlight: { key: string; promise: Promise<RuntimeModuleManifest | null> } | null = null;
let refreshTimer: ReturnType<typeof setTimeout> | undefined;
let expiryTimer: ReturnType<typeof setTimeout> | undefined;
const listeners = new Set<() => void>();

export function getManifestTenantKey(): string { return getSelectedTenant()?.tenantId ?? ''; }
export function getManifestContextKey(): string { return JSON.stringify([identity, getManifestTenantKey()]); }
export function getManifestVersion(): number { return version; }
export function getManifestStatus() { return status; }

function publish() {
  version += 1;
  listeners.forEach((listener) => listener());
}

export function subscribeManifest(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

export function setManifestIdentity(nextIdentity: string): void {
  if (identity === nextIdentity) return;
  identity = nextIdentity;
  invalidateModuleManifest();
}

export function getCachedManifest(): RuntimeModuleManifest | null {
  return cache && cache.key === getManifestContextKey() && Date.now() - cache.timestamp < TTL_MS
    ? cache.data : null;
}

export function invalidateModuleManifest(): void {
  clearTimeout(refreshTimer);
  clearTimeout(expiryTimer);
  cache = null;
  inFlight = null;
  status = 'idle';
  generation += 1;
  publish();
}

function scheduleRefresh() {
  clearTimeout(refreshTimer);
  clearTimeout(expiryTimer);
  // Refresh before expiry, preserving mounted forms while the current decision is still valid.
  refreshTimer = setTimeout(() => {
    if (typeof document !== 'undefined' && document.visibilityState === 'hidden') return;
    status = 'idle';
    publish();
  }, 25_000);
  expiryTimer = setTimeout(() => {
    cache = null;
    publish();
  }, TTL_MS);
}

if (typeof document !== 'undefined') {
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible' && !getCachedManifest()) invalidateModuleManifest();
  });
}

export function isAdminManifest(data: unknown): data is RuntimeModuleManifest {
  if (!data || typeof data !== 'object') return false;
  const value = data as RuntimeModuleManifest;
  return typeof value.businessType === 'string' && !!value.businessType.trim() &&
    [value.enabledModules, value.permissions, value.allowedPolicies].every((keys) =>
      Array.isArray(keys) && keys.every((key) => typeof key === 'string'));
}

export function fetchManifestOnce(): Promise<RuntimeModuleManifest | null> {
  const key = getManifestContextKey();
  if (inFlight?.key === key) return inFlight.promise;
  const fresh = getCachedManifest();
  if (fresh && status === 'ready') return Promise.resolve(fresh);
  const requestGeneration = generation;
  status = 'loading';
  const isCurrent = () => requestGeneration === generation && key === getManifestContextKey();
  const promise = api.get<RuntimeModuleManifest>('/admin/manifest')
    .then((data) => {
      if (!isCurrent()) return null;
      if (!isAdminManifest(data)) throw new Error('Admin manifest is incomplete');
      cache = { key, data, timestamp: Date.now() };
      status = 'ready';
      scheduleRefresh();
      return data;
    })
    .catch(() => {
      if (isCurrent()) {
        cache = null;
        status = 'error';
        clearTimeout(refreshTimer);
        clearTimeout(expiryTimer);
      }
      return null;
    })
    .finally(() => {
      if (inFlight?.promise === promise) inFlight = null;
      if (isCurrent()) publish();
    });
  inFlight = { key, promise };
  return promise;
}

if (typeof window !== 'undefined') {
  window.addEventListener('aonik:tenant-changed', invalidateModuleManifest);
  window.addEventListener('storage', (event) => {
    if (event.key === 'selected_tenant' || event.key === 'selected_tenant_id') invalidateModuleManifest();
  });
}
