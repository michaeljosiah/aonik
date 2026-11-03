import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { AdminModule, RuntimeModuleManifest } from './types';
import { pathRequiresBackendModule, resolveDisabledModuleForPath } from './enablement';
import { parseAdminProfile, resolveAdminProfile, validateAdminProfile } from './adminProfiles';
import { readFileSync } from 'node:fs';
import { adminProfiles, resolveCurrentAdminProfile, isProfilePathVisible } from './profileCatalog';
import { resolveProfilePanels } from './profilePanels';
import { getMenuIconPath, getScreenMenuIcon } from '@/lib/menuIcons';
import type { NavItem } from '@/types';

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

  it('maps every registered screen to a renderable shared menu icon', () => {
    for (const module of registry) {
      for (const route of module.routes) {
        if (!route.screen) continue;
        const icon = getScreenMenuIcon(route.screen.id, 'general');
        expect(icon, route.screen.id).toBeDefined();
        expect(getMenuIconPath(icon!), route.screen.id).toBeTruthy();
      }
    }
  });

  it.each(['base', 'simi', 'food-commerce', 'arke-kids', 'remittance'])('uses shared icons throughout the resolved %s navigation', (businessType) => {
    const permissions = registry.flatMap(module => module.routes.flatMap(route => {
      const rule = route.screen?.permissions;
      return rule && !('authenticatedAdmin' in rule) ? [...(rule.allOf ?? []), ...(rule.anyOf ?? [])] : [];
    }));
    const profile = resolveCurrentAdminProfile(registry, { ...manifestWithout(), businessType, permissions });
    expect(profile).not.toBeNull();
    const checkItems = (items: NavItem[]) => {
      for (const item of items) {
        expect(item.icon, item.id).toMatch(/^menu-/);
        expect(getMenuIconPath(item.icon), item.id).toBeTruthy();
        if (item.children) checkItems(item.children);
      }
    };
    profile!.navigation.forEach(section => checkItems(section.items));
    const commerce = profile!.navigation.flatMap(section => section.items).flatMap(item => item.children ?? [item]);
    const menu = commerce.find(item => item.id === 'commerce.products');
    if (businessType === 'food-commerce') expect(menu).toMatchObject({ label: 'Menu', icon: 'menu-menu' });
    if (businessType === 'base' || businessType === 'simi') expect(menu?.icon).toBe('menu-products');
  });

  it('keeps general product icons for host administrators viewing a food-commerce tenant', () => {
    const manifest = manifestWithout();
    const profile = resolveCurrentAdminProfile(registry, { ...manifest, businessType: 'food-commerce', allowedPolicies: [...manifest.allowedPolicies!, 'PlatformAdmin'] });
    const products = profile!.navigation.flatMap(section => section.items).flatMap(item => item.children ?? [item]).find(item => item.id === 'commerce.products');
    expect(products).toMatchObject({ label: 'Products', icon: 'menu-products' });
  });

  it('resolves the Remittance profile with operational detail routes and no unrelated product routes', () => {
    const manifest = { ...manifestWithout(), businessType: 'remittance', permissions: ['Catalog.Read', 'Ledger.Read', 'Users.Read', 'Roles.Read', 'Permissions.Read'] };
    const profile = resolveCurrentAdminProfile(registry, manifest)!;
    expect(profile.landingPath).toBe('/orders/activity');
    expect(profile.navigation.map(section => section.label)).toEqual(['Money movement', 'Network & treasury', 'Administration']);
    expect(profile.navigation[0].items[0]).toMatchObject({ label: 'Transfer orders', icon: 'menu-transfer-orders' });
    for (const path of ['/orders', '/customers/customer-id', '/catalog/partners/partner-id', '/compliance/documents', '/compliance/documents/document-id', '/access/users/user-id', '/ai/agents/finance-agent', '/ledger/accounts', '/ledger/journal-entries']) {
      expect(isProfilePathVisible(registry, profile, path), path).toBe(true);
    }
    for (const path of ['/orders/bill-payments/new', '/orders/bill-payments/order-id', '/billing/invoices', '/commerce', '/accounts', '/tenants']) {
      expect(isProfilePathVisible(registry, profile, path), path).toBe(false);
    }
  });

  it('keeps Remittance permissions, policies, module overrides and host audience authoritative', () => {
    const manifest = { ...manifestWithout(), businessType: 'remittance', permissions: [] };
    const restricted = resolveCurrentAdminProfile(registry, manifest)!;
    expect(isProfilePathVisible(registry, restricted, '/catalog/partners')).toBe(false);
    expect(isProfilePathVisible(registry, restricted, '/ledger')).toBe(false);
    expect(isProfilePathVisible(registry, restricted, '/access/users')).toBe(false);
    const withoutFinance = resolveCurrentAdminProfile(registry, { ...manifest, enabledModules: manifest.enabledModules.filter(id => id !== 'finance') })!;
    expect(withoutFinance.navigation.some(section => section.id === 'network')).toBe(false);
    expect(withoutFinance.landingPath).toBe('/customers');
    const withoutDocuments = resolveCurrentAdminProfile(registry, { ...manifest, enabledModules: manifest.enabledModules.filter(id => id !== 'documents') })!;
    expect(isProfilePathVisible(registry, withoutDocuments, '/compliance')).toBe(false);
    expect(isProfilePathVisible(registry, withoutDocuments, '/compliance/documents')).toBe(false);
    const withoutPolicy = resolveCurrentAdminProfile(registry, { ...manifest, permissions: ['Catalog.Read'], allowedPolicies: ['AdminUserPolicy'] })!;
    expect(isProfilePathVisible(registry, withoutPolicy, '/catalog/partners')).toBe(false);
    const host = resolveCurrentAdminProfile(registry, { ...manifest, allowedPolicies: [...manifest.allowedPolicies!, 'PlatformAdmin'] })!;
    expect(host.labels.get('finance.orders')).toBe('All orders');
    expect(isProfilePathVisible(registry, host, '/commerce')).toBe(true);
  });

  it('gates transfer creation and detail independently, including the static new route', () => {
    const base = { ...manifestWithout(), businessType: 'remittance', allowedPolicies: ['AdminUserPolicy', 'AdminReadPolicy'], permissions: ['Payment.Read'] };
    const reader = resolveCurrentAdminProfile(registry, base);
    expect(isProfilePathVisible(registry, reader, '/orders/transfers/order-id')).toBe(true);
    expect(isProfilePathVisible(registry, reader, '/orders/transfers/new')).toBe(false);
    const writer = resolveCurrentAdminProfile(registry, { ...base, permissions: ['Payment.Read', 'Payment.Create', 'Customers.Read'], allowedPolicies: [...base.allowedPolicies, 'AdminWritePolicy'] });
    expect(isProfilePathVisible(registry, writer, '/orders/transfers/new')).toBe(true);
    expect(writer!.navigation[0].items.some(item => item.id === 'finance.transfer-new')).toBe(true);
    expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, { ...base, enabledModules: base.enabledModules.filter(id => id !== 'finance') }), '/orders/transfers/order-id')).toBe(false);
    expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, { ...base, businessType: 'food-commerce' }), '/orders/transfers/order-id')).toBe(false);
  });

  it.each(['base', 'food-commerce'])('exposes discount management through the %s profile and Commerce module', (businessType) => {
    const manifest = { ...manifestWithout(), businessType };
    const profile = resolveCurrentAdminProfile(registry, manifest);
    expect(isProfilePathVisible(registry, profile, '/commerce/discounts')).toBe(true);
    expect(JSON.stringify(profile?.navigation)).toContain('/commerce/discounts');
    expect(pathRequiresBackendModule(registry, 'commerce', '/commerce/discounts')).toBe(true);
    expect(resolveDisabledModuleForPath(registry, manifestWithout('commerce'), '/commerce/discounts')).toMatchObject({ backendModuleId: 'commerce' });
  });

  it('exposes beneficiary management only through the right profile, module, policy and read permissions', () => {
    const base = { ...manifestWithout(), businessType: 'remittance', allowedPolicies: ['AdminUserPolicy', 'AdminReadPolicy'], permissions: ['Payment.Read', 'Customers.Read'] };
    const reader = resolveCurrentAdminProfile(registry, base);
    expect(isProfilePathVisible(registry, reader, '/remittance/beneficiaries')).toBe(true);
    expect(reader!.navigation[0].items.some(item => item.id === 'finance.beneficiaries')).toBe(true);
    for (const changes of [{ permissions: ['Payment.Read'] }, { allowedPolicies: ['AdminUserPolicy'] }, { enabledModules: base.enabledModules.filter(id => id !== 'finance') }, { businessType: 'food-commerce' }]) {
      expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, { ...base, ...changes }), '/remittance/beneficiaries')).toBe(false);
    }
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

  it('makes packing reachable only through the staff policy in both commerce profiles', () => {
    for (const businessType of ['base', 'food-commerce']) {
      const manifest = { ...manifestWithout('finance'), businessType, allowedPolicies: ['AdminUserPolicy', 'AdminReadPolicy'] };
      expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, manifest), '/commerce/orders/example/packing')).toBe(true);
      expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, { ...manifest, allowedPolicies: ['AdminUserPolicy'] }), '/commerce/orders/example/packing')).toBe(false);
      expect(isProfilePathVisible(registry, resolveCurrentAdminProfile(registry, { ...manifest, enabledModules: manifest.enabledModules.filter(id => id !== 'commerce') }), '/commerce/orders/example/packing')).toBe(false);
    }
  });

  it('never marks the home route or Customers as finance-owned for the 403 redirect rule', () => {
    expect(pathRequiresBackendModule(registry, 'finance', '/')).toBe(false);
    expect(pathRequiresBackendModule(registry, 'finance', '/customers')).toBe(false);
    expect(pathRequiresBackendModule(registry, 'finance', '/orders/activity')).toBe(true);
  });
});
