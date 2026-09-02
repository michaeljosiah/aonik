import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { RuntimeModuleManifest } from './types';

// vi.mock is hoisted above the imports, so the mock state must be too.
const mocks = vi.hoisted(() => ({
  get: vi.fn<(url: string) => Promise<unknown>>(),
  tenant: { current: 'tenant-a' as string | null },
}));

vi.mock('@/lib/api', () => ({
  api: { get: mocks.get },
}));

vi.mock('@/lib/tenantContext', () => ({
  getSelectedTenant: () =>
    mocks.tenant.current ? { tenantId: mocks.tenant.current, name: mocks.tenant.current } : null,
}));

import {
  fetchManifestOnce,
  getManifestTenantKey,
  getManifestVersion,
  invalidateModuleManifest,
  subscribeManifest,
} from './manifestCache';

interface Deferred<T> {
  promise: Promise<T>;
  resolve: (value: T) => void;
  reject: (error: unknown) => void;
}

/** A promise the test resolves by hand, so completion ORDER is under control. */
function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function manifest(enabled: string[]): RuntimeModuleManifest {
  return { enabledModules: enabled, modules: [], featureFlags: {} };
}

/** Queue one manual response for the next `api.get` call. */
function nextResponse(): Deferred<RuntimeModuleManifest> {
  const d = deferred<RuntimeModuleManifest>();
  mocks.get.mockImplementationOnce(() => d.promise);
  return d;
}

/** Let every settled promise callback run before asserting on cache state. */
async function flush(): Promise<void> {
  for (let i = 0; i < 5; i += 1) {
    await Promise.resolve();
  }
}

const BEFORE_TOGGLE = manifest(['platform', 'finance']);
const AFTER_TOGGLE = manifest(['platform']);

describe('manifestCache', () => {
  beforeEach(() => {
    mocks.get.mockReset();
    mocks.tenant.current = 'tenant-a';
    // Reset shared module state between tests.
    invalidateModuleManifest();
  });

  it('caches a response that completes with no intervening invalidation', async () => {
    const first = nextResponse();

    const pending = fetchManifestOnce();
    first.resolve(BEFORE_TOGGLE);

    await expect(pending).resolves.toEqual(BEFORE_TOGGLE);
    // Second read is served from the cache — no second request.
    await expect(fetchManifestOnce()).resolves.toEqual(BEFORE_TOGGLE);
    expect(mocks.get).toHaveBeenCalledTimes(1);
  });

  it('is single-flight: concurrent reads share one request', async () => {
    const first = nextResponse();

    const a = fetchManifestOnce();
    const b = fetchManifestOnce();
    expect(a).toBe(b);

    first.resolve(BEFORE_TOGGLE);
    await expect(a).resolves.toEqual(BEFORE_TOGGLE);
    expect(mocks.get).toHaveBeenCalledTimes(1);
  });

  it('does not let an older response that completes after invalidation overwrite the newer cache', async () => {
    const older = nextResponse();
    const olderRead = fetchManifestOnce();

    // A module toggle lands while the first request is still running.
    invalidateModuleManifest();
    const newer = nextResponse();
    const newerRead = fetchManifestOnce();
    expect(mocks.get).toHaveBeenCalledTimes(2);

    // The replacement completes FIRST ...
    newer.resolve(AFTER_TOGGLE);
    await expect(newerRead).resolves.toEqual(AFTER_TOGGLE);

    // ... and the pre-toggle response lands last.
    older.resolve(BEFORE_TOGGLE);
    await flush();

    // The cache still holds the post-toggle manifest: no third request, and
    // the newer data is what every subsequent read sees.
    await expect(fetchManifestOnce()).resolves.toEqual(AFTER_TOGGLE);
    expect(mocks.get).toHaveBeenCalledTimes(2);
    // Whoever awaited the older request is handed the newest data too.
    await expect(olderRead).resolves.toEqual(AFTER_TOGGLE);
  });

  it("hands an older awaiter the newer request's result when the older one completes first", async () => {
    const older = nextResponse();
    const olderRead = fetchManifestOnce();

    invalidateModuleManifest();
    const newer = nextResponse();
    const newerRead = fetchManifestOnce();

    // Pre-toggle response lands while the replacement is still in flight.
    older.resolve(BEFORE_TOGGLE);
    await flush();

    // Nothing cached from the stale response: a fresh read joins the
    // in-flight replacement rather than being served pre-toggle data.
    const joined = fetchManifestOnce();
    expect(mocks.get).toHaveBeenCalledTimes(2);

    newer.resolve(AFTER_TOGGLE);
    await expect(newerRead).resolves.toEqual(AFTER_TOGGLE);
    await expect(joined).resolves.toEqual(AFTER_TOGGLE);
    await expect(olderRead).resolves.toEqual(AFTER_TOGGLE);
    await expect(fetchManifestOnce()).resolves.toEqual(AFTER_TOGGLE);
    expect(mocks.get).toHaveBeenCalledTimes(2);
  });

  it('serves a stale response fail-open (uncached) when nothing newer exists', async () => {
    const older = nextResponse();
    const olderRead = fetchManifestOnce();

    invalidateModuleManifest();
    older.resolve(BEFORE_TOGGLE);

    // No replacement request was started: the awaiter still gets data ...
    await expect(olderRead).resolves.toEqual(BEFORE_TOGGLE);

    // ... but it was never cached, so the next read hits the API.
    const fresh = nextResponse();
    const freshRead = fetchManifestOnce();
    expect(mocks.get).toHaveBeenCalledTimes(2);
    fresh.resolve(AFTER_TOGGLE);
    await expect(freshRead).resolves.toEqual(AFTER_TOGGLE);
  });

  it('never caches a response for a tenant that is no longer selected', async () => {
    const forTenantA = nextResponse();
    const readA = fetchManifestOnce();
    expect(getManifestTenantKey()).toBe('tenant-a');

    // Tenant switch: the picker updates the selection and invalidates.
    mocks.tenant.current = 'tenant-b';
    invalidateModuleManifest();
    const forTenantB = nextResponse();
    const readB = fetchManifestOnce();

    forTenantB.resolve(AFTER_TOGGLE);
    await expect(readB).resolves.toEqual(AFTER_TOGGLE);

    forTenantA.resolve(BEFORE_TOGGLE);
    await flush();

    await expect(fetchManifestOnce()).resolves.toEqual(AFTER_TOGGLE);
    expect(mocks.get).toHaveBeenCalledTimes(2);
    await expect(readA).resolves.toEqual(AFTER_TOGGLE);
  });

  it('resolves null on transport failure without caching', async () => {
    const failing = nextResponse();
    const read = fetchManifestOnce();
    failing.reject(new Error('boom'));
    await expect(read).resolves.toBeNull();

    const retry = nextResponse();
    const retryRead = fetchManifestOnce();
    expect(mocks.get).toHaveBeenCalledTimes(2);
    retry.resolve(AFTER_TOGGLE);
    await expect(retryRead).resolves.toEqual(AFTER_TOGGLE);
  });

  it('bumps the generation and notifies subscribers on every invalidation', () => {
    const before = getManifestVersion();
    const listener = vi.fn();
    const unsubscribe = subscribeManifest(listener);

    invalidateModuleManifest();
    expect(getManifestVersion()).toBe(before + 1);
    expect(listener).toHaveBeenCalledTimes(1);

    unsubscribe();
    invalidateModuleManifest();
    expect(getManifestVersion()).toBe(before + 2);
    expect(listener).toHaveBeenCalledTimes(1);
  });
});
