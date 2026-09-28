import { describe, expect, it } from 'vitest';
import { parseAdminProfile, resolveAdminProfile, selectAdminProfile, validateAdminProfile } from './adminProfiles';
import type { AdminProfile } from './adminProfiles';
import type { AdminModule, ModuleRouteConfig, RuntimeModuleManifest } from './types';

const page = () => null;
const routes: ModuleRouteConfig[] = [
  { path: '/orders', element: page, screen: { id: 'orders', label: 'Orders', permissions: { allOf: ['Orders.Read'] } } },
  { path: '/orders/:id', element: page, isDynamic: true, screen: { id: 'order-detail', label: 'Order', permissions: { allOf: ['Orders.Read'] } } },
  { path: '/invoices', element: page, requires: ['finance'], screen: { id: 'invoices', label: 'Invoices', permissions: { allOf: ['Invoices.Read'], anyOf: ['Invoices.Write', 'Invoices.Approve'] } } },
  { path: '/settings', element: page, screen: { id: 'settings', label: 'Settings', permissions: { authenticatedAdmin: true } } },
  { path: '/tenants', element: page, screen: { id: 'tenants', label: 'Tenants', audience: 'host', permissions: { allOf: ['Tenants.Read'] } } },
  { path: '/unclassified', element: page },
];
const modules: AdminModule[] = [{ id: 'test', name: 'Test', requires: ['commerce'], routes, navigation: [], panels: [], panelComponents: {}, breadcrumbs: [] }];
const profile: AdminProfile = {
  id: 'food-commerce', landingPage: 'orders',
  screens: { orders: { label: 'Food Orders' }, 'order-detail': {}, invoices: {}, settings: {}, tenants: {} },
  navigation: [{ id: 'operations', label: 'Operations', items: [
    { screen: 'orders' },
    { id: 'accounting', label: 'Accounting', items: [{ screen: 'invoices' }] },
    { screen: 'settings' }, { screen: 'tenants' },
  ] }],
};
const manifest: RuntimeModuleManifest = { businessType: 'food-commerce', permissions: ['Orders.Read'], enabledModules: ['commerce', 'finance'], modules: [], featureFlags: {} };

describe('admin profile resolution', () => {
  it('intersects screens and permissions without adding unclassified or unrelated routes', () => {
    const result = resolveAdminProfile(profile, modules, manifest, 'tenant')!;
    expect(result.routes.map((route) => route.path)).toEqual(['/orders', '/orders/:id', '/settings']);
    expect(result.navigation[0].items.map((item) => item.label)).toEqual(['Food Orders', 'Settings']);
    expect(result.labels.get('orders')).toBe('Food Orders');
    expect(result.labels.get('order-detail')).toBe('Order');
    expect(result.landingPath).toBe('/orders');
  });

  it('keeps a detail route available without adding it to navigation', () => {
    const result = resolveAdminProfile(profile, modules, manifest, 'tenant')!;
    expect(result.routes).toContain(routes[1]);
    expect(result.navigation[0].items.some((item) => item.id === 'order-detail')).toBe(false);
  });

  it('requires both all-of and any-of permission expressions', () => {
    const readOnly = { ...manifest, permissions: ['Invoices.Read'] };
    expect(resolveAdminProfile(profile, modules, readOnly, 'tenant')!.routes).not.toContain(routes[2]);
    const approved = { ...manifest, permissions: ['Invoices.Read', 'Invoices.Approve'] };
    expect(resolveAdminProfile(profile, modules, approved, 'tenant')!.routes).toContain(routes[2]);
    expect(resolveAdminProfile(profile, modules, { ...approved, permissions: ['Invoices.Approve'] }, 'tenant')!.routes).not.toContain(routes[2]);
  });

  it('requires both owning-module and route-specific dependencies', () => {
    const allowed = { ...manifest, permissions: ['Invoices.Read', 'Invoices.Write'] };
    expect(resolveAdminProfile(profile, modules, { ...allowed, enabledModules: ['commerce'] }, 'tenant')!.routes).not.toContain(routes[2]);
    const result = resolveAdminProfile(profile, modules, { ...allowed, enabledModules: ['finance'] }, 'tenant')!;
    expect(result.routes).toEqual([]);
    expect(result.navigation).toEqual([]);
    expect(result.landingPath).toBeNull();
    expect(allowed.enabledModules).toEqual(['commerce', 'finance']);
  });

  it('cannot expose host screens to tenant users with the same permission', () => {
    const allowed = { ...manifest, permissions: ['Tenants.Read'] };
    expect(resolveAdminProfile(profile, modules, allowed, 'tenant')!.routes).not.toContain(routes[4]);
    expect(resolveAdminProfile(profile, modules, allowed, 'host')!.routes).toContain(routes[4]);
  });

  it('uses the first available navigation entry when the preferred landing is denied', () => {
    expect(resolveAdminProfile(profile, modules, { ...manifest, permissions: [] }, 'tenant')!.landingPath).toBe('/settings');
    const allowed = { ...manifest, permissions: ['Invoices.Read', 'Invoices.Write'] };
    expect(resolveAdminProfile(profile, modules, allowed, 'tenant')!.landingPath).toBe('/invoices');
  });

  it('does not turn an unavailable manifest or old server contract into all screens', () => {
    expect(resolveAdminProfile(profile, modules, null, 'tenant')).toBeNull();
    expect(resolveAdminProfile(profile, modules, { ...manifest, businessType: undefined }, 'tenant')).toBeNull();
    expect(resolveAdminProfile(profile, modules, { ...manifest, permissions: undefined }, 'tenant')).toBeNull();
    expect(resolveAdminProfile(profile, modules, { ...manifest, businessType: '' }, 'tenant')).toBeNull();
  });

  it('retains legacy deny overrides as further restrictions', () => {
    const result = resolveAdminProfile(profile, modules, { ...manifest, disabledRoutes: ['/orders'], disabledNavItems: ['settings'] }, 'tenant')!;
    expect(result.routes).not.toContain(routes[0]);
    expect(result.navigation).toEqual([]);
    expect(result.landingPath).toBeNull();
  });
});

describe('admin profile validation and selection', () => {
  it('rejects unknown, unclassified and duplicate screen IDs', () => {
    expect(() => validateAdminProfile({ ...profile, screens: { ...profile.screens, missing: {} } }, modules)).toThrow('Unknown or unclassified');
    expect(() => validateAdminProfile(profile, [{ ...modules[0], routes: [...routes, routes[0]] }])).toThrow('Duplicate screen');
  });

  it('rejects navigation outside the allowlist and parameterised landing destinations', () => {
    expect(() => validateAdminProfile({ ...profile, screens: { orders: {} } }, modules)).toThrow('navigation destination');
    expect(() => validateAdminProfile({ ...profile, landingPage: 'order-detail' }, modules)).toThrow('navigation destination');
  });

  it('rejects executable/unknown fields, duplicate navigation IDs and missing permission rules', () => {
    expect(() => parseAdminProfile({ ...profile, component: 'InjectedPage' })).toThrow('Invalid profile fields');
    expect(() => parseAdminProfile({ ...profile, screens: { orders: { permissions: ['Admin'] } } })).toThrow('Invalid screen');
    expect(() => parseAdminProfile({ ...profile, navigation: [...profile.navigation, ...profile.navigation] })).toThrow('Duplicate navigation');
    const invalid = { ...routes[0], screen: { ...routes[0].screen!, permissions: { allOf: [] } } };
    expect(() => validateAdminProfile(profile, [{ ...modules[0], routes: [invalid] }])).toThrow('Missing permission');
  });

  it('uses general only for valid unmapped business types and refuses missing bundles', () => {
    const general = { ...profile, id: 'general' };
    const profiles = [general, profile];
    const mapping = { base: 'general', 'food-commerce': 'food-commerce' };
    expect(selectAdminProfile('food-commerce', profiles, mapping)).toBe(profile);
    expect(selectAdminProfile('future-business', profiles, mapping)).toBe(general);
    expect(selectAdminProfile(undefined, profiles, mapping)).toBeNull();
    expect(selectAdminProfile('', profiles, mapping)).toBeNull();
    expect(() => selectAdminProfile('food-commerce', [general], mapping)).toThrow('Missing admin profile');
  });
});
