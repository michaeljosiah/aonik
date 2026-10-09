import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { AdminModule, RuntimeModuleManifest } from './types';
import { pathRequiresBackendModule, resolveDisabledModuleForPath } from './enablement';
import { parseAdminProfile, resolveAdminProfile, validateAdminProfile } from './adminProfiles';
import { readFileSync } from 'node:fs';
import { adminProfiles, resolveCurrentAdminProfile, isProfilePathVisible } from './profileCatalog';
import { resolveProfilePanels } from './profilePanels';

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
  return { businessType: 'base', permissions: [], allowedPolicies: ['AdminUserPolicy', 'AdminPolicy', 'AdminWritePolicy', 'AdminUserWritePolicy'], enabledModules: enabled, modules: [], featureFlags: {} };
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

  it.each(['base', 'food-commerce'])('exposes discount management through the %s profile and Commerce module', (businessType) => {
    const manifest = { ...manifestWithout(), businessType };
    const profile = resolveCurrentAdminProfile(registry, manifest);
    expect(isProfilePathVisible(registry, profile, '/commerce/discounts')).toBe(true);
    expect(JSON.stringify(profile?.navigation)).toContain('/commerce/discounts');
    expect(pathRequiresBackendModule(registry, 'commerce', '/commerce/discounts')).toBe(true);
    expect(resolveDisabledModuleForPath(registry, manifestWithout('commerce'), '/commerce/discounts')).toMatchObject({ backendModuleId: 'commerce' });
  });

  it('validates the food-commerce JSON against actual classified screens', () => {
    const profile = parseAdminProfile(JSON.parse(readFileSync(
      new URL('../config/admin-profiles/food-commerce.json', import.meta.url), 'utf8',
    ).replace(/^\uFEFF/, '')));
    expect(() => validateAdminProfile(profile, registry)).not.toThrow();
    const manifest = { ...manifestWithout(), businessType: 'food-commerce', permissions: [] };
    const resolved = resolveAdminProfile(profile, registry, manifest, 'tenant')!;
    expect(resolved.landingPath).toBe('/commerce/orders');
    expect(resolved.labels.get('commerce.products')).toBe('Menu');
    expect(resolved.routes.some((route) => route.path === '/orders/activity')).toBe(false);
    expect(resolved.routes.some((route) => route.path === '/customers')).toBe(true);
    expect(resolved.routes.some((route) => route.path === '/commerce/products/:productId')).toBe(true);
    expect(resolveAdminProfile(profile, registry, {
      ...manifest, enabledModules: manifest.enabledModules.filter((id) => id !== 'commerce'),
    }, 'tenant')!.routes.some((route) => route.path.startsWith('/commerce'))).toBe(false);
  });


  it('classifies every registered route and validates all bundled profiles', () => {
    expect(registry.flatMap((module) => module.routes).every((route) => !!route.screen)).toBe(true);
    expect(new Set(adminProfiles.map((profile) => profile.id)).size).toBe(adminProfiles.length);
    for (const profile of adminProfiles) expect(() => validateAdminProfile(profile, registry)).not.toThrow();
  });

  it('keeps Arke finance dependencies on while hiding their screens, panels and deep links', () => {
    const manifest = { ...manifestWithout(), businessType: 'arke-kids', permissions: ['Invoice.Read', 'Ledger.Read'] };
    const result = resolveCurrentAdminProfile(registry, manifest)!;
    expect(result.landingPath).toBe('/customers');
    expect(isProfilePathVisible(registry, result, '/ledger')).toBe(false);
    expect(isProfilePathVisible(registry, result, '/billing/invoices/new')).toBe(false);
    expect(isProfilePathVisible(registry, result, '/commerce')).toBe(false);
    expect(manifest.enabledModules).toContain('finance');
    expect(resolveProfilePanels(registry, result, manifest).some((panel) => panel.id === 'invoice-manager')).toBe(false);
  });

  it('does not let an allowed detail pattern swallow a forbidden create route', () => {
    const manifest = { ...manifestWithout(), businessType: 'food-commerce', permissions: ['Invoice.Read'] };
    const result = resolveCurrentAdminProfile(registry, manifest)!;
    expect(isProfilePathVisible(registry, result, '/billing/invoices/123')).toBe(true);
    expect(isProfilePathVisible(registry, result, '/billing/invoices/new')).toBe(false);
    expect(isProfilePathVisible(registry, result, '/billing/invoices/new?copy=1')).toBe(false);
  });

  it('uses the host inventory only when the server authorises PlatformAdmin', () => {
    const manifest = { ...manifestWithout(), businessType: 'arke-kids', permissions: ['Tenants.Read'] };
    expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, manifest), '/tenants')).toBe(false);
    const host = { ...manifest, allowedPolicies: [...manifest.allowedPolicies!, 'PlatformAdmin'] };
    expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, host), '/tenants')).toBe(true);
  });

  it('removes privileged administration for ReadOnly even if it carries a service permission', () => {
    const manifest = { ...manifestWithout(), permissions: ['Users.Read'], allowedPolicies: ['AdminUserPolicy'] };
    expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, manifest), '/access/users')).toBe(false);
  });
  it('keeps order details readable while denying order creation to read-only callers', () => {
    const manifest = { ...manifestWithout(), allowedPolicies: ['AdminUserPolicy'] };
    const result = resolveCurrentAdminProfile(registry, manifest);
    expect(isProfilePathVisible(registry, result, '/orders/activity')).toBe(true);
    expect(isProfilePathVisible(registry, result, '/orders/bill-payments/existing')).toBe(true);
    expect(isProfilePathVisible(registry, result, '/orders/bill-payments/new')).toBe(false);
  });

  it('retains communication and notification settings in host navigation only', () => {
    const manifest = manifestWithout();
    const tenant = resolveCurrentAdminProfile(registry, manifest)!;
    expect(JSON.stringify(tenant.navigation)).not.toContain('/settings/communication');
    const host = resolveCurrentAdminProfile(registry, { ...manifest, allowedPolicies: [...manifest.allowedPolicies!, 'PlatformAdmin'] })!;
    expect(JSON.stringify(host.navigation)).toContain('/settings/communication');
    expect(JSON.stringify(host.navigation)).toContain('/settings/notification-templates');
  });

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

  it('requires staff policy and customer-read permission for contact pages in both profiles', () => {
    for (const businessType of ['base', 'food-commerce']) {
      const manifest = { ...manifestWithout('finance'), businessType, permissions: ['Customers.Read'], allowedPolicies: ['AdminUserPolicy', 'AdminReadPolicy'] };
      const allowed = resolveCurrentAdminProfile(registry, manifest);
      expect(isProfilePathVisible(registry, allowed, '/contact-enquiries')).toBe(true);
      expect(isProfilePathVisible(registry, allowed, '/contact-enquiries/example')).toBe(true);
      expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, { ...manifest, permissions: [] }), '/contact-enquiries')).toBe(false);
      expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, { ...manifest, allowedPolicies: ['AdminUserPolicy'] }), '/contact-enquiries/example')).toBe(false);
    }
  });

  it('never marks the home route or Customers as finance-owned for the 403 redirect rule', () => {
    expect(pathRequiresBackendModule(registry, 'finance', '/')).toBe(false);
    expect(pathRequiresBackendModule(registry, 'finance', '/customers')).toBe(false);
    expect(pathRequiresBackendModule(registry, 'finance', '/orders/activity')).toBe(true);
  });
});
