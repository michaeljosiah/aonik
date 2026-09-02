import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { AdminModule, RuntimeModuleManifest } from './types';
import { pathRequiresBackendModule, resolveDisabledModuleForPath } from './enablement';

// ---------------------------------------------------------------------------
// Route ownership against the REAL registry (Spec 097 §10.1, acceptance 6).
// Customers and Compliance are Platform-owned: with Finance off they must not
// resolve to the finance module's "not enabled" page, and the home route must
// never be treated as finance-owned by the API client's redirect rule.
//
// The registry imports every page, and a few of them (auth config, theme)
// read browser globals at module scope, so the test runs in node with a
// minimal window/document stub and loads the registry lazily.
// ---------------------------------------------------------------------------

function manifestWithout(...disabled: string[]): RuntimeModuleManifest {
  const enabled = ['platform', 'ordering', 'ai', 'agents', 'finance', 'commerce', 'subscriptions',
    'groups', 'workspaces', 'personal-finance', 'voice', 'documents']
    .filter((id) => !disabled.includes(id));
  return { enabledModules: enabled, modules: [], featureFlags: {} };
}

const noop = () => undefined;
const storage = {
  getItem: () => null,
  setItem: noop,
  removeItem: noop,
  clear: noop,
  key: () => null,
  length: 0,
};

describe('registry route ownership', () => {
  let registry: AdminModule[] = [];

  beforeAll(async () => {
    vi.stubGlobal('window', {
      location: { origin: 'http://localhost', href: 'http://localhost/', pathname: '/', search: '', hash: '' },
      addEventListener: noop,
      removeEventListener: noop,
      matchMedia: () => ({ matches: false, addEventListener: noop, removeEventListener: noop, addListener: noop, removeListener: noop }),
      localStorage: storage,
      sessionStorage: storage,
      navigator: { userAgent: 'vitest' },
      setTimeout,
      clearTimeout,
    });
    const element = () => ({
      style: {},
      classList: { add: noop, remove: noop, toggle: noop, contains: () => false },
      setAttribute: noop,
      getAttribute: () => null,
      appendChild: noop,
      removeChild: noop,
      insertBefore: noop,
      querySelector: () => null,
      querySelectorAll: () => [],
      addEventListener: noop,
      removeEventListener: noop,
      textContent: '',
      innerHTML: '',
      firstChild: null,
      childNodes: [],
    });
    vi.stubGlobal('document', {
      documentElement: element(),
      body: element(),
      head: element(),
      addEventListener: noop,
      removeEventListener: noop,
      createElement: element,
      createTextNode: (text: string) => ({ textContent: text }),
      querySelector: () => null,
      querySelectorAll: () => [],
      getElementById: () => null,
      getElementsByTagName: () => [],
      cookie: '',
    });
    vi.stubGlobal('localStorage', storage);
    vi.stubGlobal('sessionStorage', storage);
    vi.stubGlobal('navigator', { userAgent: 'vitest' });

    const { getModules } = await import('./registry');
    registry = getModules();
  }, 60_000);

  it('keeps Customers and Compliance reachable when Finance is off', () => {
    const manifest = manifestWithout('finance', 'commerce', 'subscriptions', 'workspaces');
    expect(resolveDisabledModuleForPath(registry, manifest, '/customers')).toBeNull();
    expect(resolveDisabledModuleForPath(registry, manifest, '/customers/abc')).toBeNull();
    expect(resolveDisabledModuleForPath(registry, manifest, '/compliance')).toBeNull();
    expect(resolveDisabledModuleForPath(registry, manifest, '/compliance/documents')).toBeNull();
    expect(resolveDisabledModuleForPath(registry, manifest, '/compliance/documents/new')).toBeNull();
    expect(resolveDisabledModuleForPath(registry, manifest, '/compliance/documents/d1')).toBeNull();
  });

  it('still resolves finance-owned routes to the finance module when Finance is off', () => {
    const manifest = manifestWithout('finance', 'commerce', 'subscriptions', 'workspaces');
    expect(resolveDisabledModuleForPath(registry, manifest, '/orders/activity')?.backendModuleId).toBe('finance');
    expect(resolveDisabledModuleForPath(registry, manifest, '/ledger')?.backendModuleId).toBe('finance');
  });

  it('registers Customers and Compliance on the platform module and nowhere else', () => {
    const owners = (path: string) => registry
      .filter((mod) => mod.routes.some((route) => route.path === path))
      .map((mod) => mod.id);
    expect(owners('/customers')).toEqual(['platform']);
    expect(owners('/customers/:partyId')).toEqual(['platform']);
    expect(owners('/compliance/documents')).toEqual(['platform']);
  });

  it('never marks the home route or Customers as finance-owned for the 403 redirect rule', () => {
    expect(pathRequiresBackendModule(registry, 'finance', '/')).toBe(false);
    expect(pathRequiresBackendModule(registry, 'finance', '/customers')).toBe(false);
    expect(pathRequiresBackendModule(registry, 'finance', '/orders/activity')).toBe(true);
  });
});
