import { describe, expect, it } from 'vitest';
import type { ComponentType } from 'react';

import type { NavigationSection } from '@/types';
import {
  filterNavByModules,
  filterRoutesByBackendModules,
  isBackendModuleEnabled,
  matchesRoutePath,
  pathRequiresBackendModule,
  resolveDisabledModuleForPath,
  resolveEnabledUiModules,
} from './enablement';
import type { AdminModule, RuntimeModuleManifest } from './types';

const Noop = (() => null) as unknown as ComponentType;

function uiModule(id: string, requires: string[] | undefined, routes: string[] = []): AdminModule {
  return {
    id,
    name: id.charAt(0).toUpperCase() + id.slice(1),
    requires,
    navigation: [],
    routes: routes.map((path) => ({ path, element: Noop, isDynamic: path.includes(':') })),
    panels: [],
    panelComponents: {},
    breadcrumbs: [],
  };
}

const registry: AdminModule[] = [
  uiModule('core', [], ['/ai/models']),
  uiModule('platform', undefined, ['/settings', '/tenants/:id']),
  uiModule('finance', ['finance'], ['/orders/activity', '/billing/invoices/:id', '/catalog/billers/:billerId/services/:serviceId']),
  uiModule('commerce', ['commerce'], ['/commerce', '/commerce/products/:productId']),
  uiModule('agent-command-center', ['agents'], ['/approvals']),
];

function manifest(enabled: string[]): RuntimeModuleManifest {
  return {
    enabledModules: enabled,
    modules: [
      { id: 'platform', name: 'Platform', description: 'Identity and tenancy', isCore: true, isEnabled: true, dependsOn: [] },
      { id: 'finance', name: 'Finance', description: 'Ledger, payments and billing', isCore: false, isEnabled: enabled.includes('finance'), dependsOn: ['ordering'] },
      { id: 'commerce', name: 'Commerce', description: 'Storefront engine', isCore: false, isEnabled: enabled.includes('commerce'), dependsOn: ['finance', 'ordering'] },
      { id: 'agents', name: 'Agents', description: 'Domain agents', isCore: true, isEnabled: true, dependsOn: ['ai'] },
    ],
    featureFlags: {},
  };
}

describe('resolveEnabledUiModules', () => {
  it('returns undefined when there is no manifest (fail-open)', () => {
    expect(resolveEnabledUiModules(registry, undefined)).toBeUndefined();
  });

  it('treats an absent or empty requires list as always enabled', () => {
    const result = resolveEnabledUiModules(registry, []);
    expect(result).toContain('core');
    expect(result).toContain('platform');
    expect(result).not.toContain('finance');
    expect(result).not.toContain('commerce');
    expect(result).not.toContain('agent-command-center');
  });

  it('enables a UI module only when every required backend id is enabled', () => {
    const both = uiModule('both', ['finance', 'commerce']);
    const modules = [...registry, both];

    expect(resolveEnabledUiModules(modules, ['finance'])).not.toContain('both');
    expect(resolveEnabledUiModules(modules, ['finance', 'commerce'])).toContain('both');
  });

  it('maps backend ids to UI module ids', () => {
    expect(resolveEnabledUiModules(registry, ['finance', 'agents'])).toEqual([
      'core',
      'platform',
      'finance',
      'agent-command-center',
    ]);
  });
});

describe('filterNavByModules', () => {
  const nav: NavigationSection[] = [
    {
      id: 'transact',
      label: 'Transact',
      items: [
        {
          id: 'orders',
          label: 'Orders',
          icon: 'receipt',
          moduleId: 'finance',
          children: [{ id: 'orders-activity', label: 'All orders', icon: 'list', href: '/orders/activity' }],
        },
        { id: 'customers', label: 'Customers', icon: 'users2', href: '/customers' },
      ],
    },
    {
      id: 'products',
      label: 'Products',
      items: [
        {
          id: 'billing',
          label: 'Billing',
          icon: 'book',
          children: [
            { id: 'billing-invoices', label: 'Invoices', icon: 'invoice', href: '/billing/invoices', moduleId: 'finance' },
            {
              id: 'ledger',
              label: 'Ledger',
              icon: 'book2',
              children: [
                { id: 'ledger-overview', label: 'Ledgers', icon: 'book', href: '/ledger', moduleId: 'finance' },
              ],
            },
          ],
        },
        {
          id: 'commerce',
          label: 'Commerce',
          icon: 'cart',
          moduleId: 'commerce',
          children: [{ id: 'cm-overview', label: 'Overview', icon: 'dashboard', href: '/commerce' }],
        },
        {
          id: 'grouped',
          label: 'Grouped',
          icon: 'stack',
          childGroups: [
            { label: 'Money', items: [{ id: 'g-fin', label: 'Fin', icon: 'bank', href: '/fin', moduleId: 'finance' }] },
            { label: 'Shop', items: [{ id: 'g-com', label: 'Shop', icon: 'cart', href: '/shop', moduleId: 'commerce' }] },
          ],
        },
      ],
    },
  ];

  const ids = (sections: NavigationSection[]) =>
    sections.map((s) => ({ id: s.id, items: s.items.map((i) => i.id) }));

  it('renders everything when the enabled set is null (no manifest)', () => {
    const result = filterNavByModules(nav, null);
    expect(ids(result)).toEqual([
      { id: 'transact', items: ['orders', 'customers'] },
      { id: 'products', items: ['billing', 'commerce', 'grouped'] },
    ]);
  });

  it('hides items whose moduleId is not enabled and leaves untagged items alone', () => {
    const result = filterNavByModules(nav, new Set(['commerce']));
    expect(ids(result)).toEqual([
      { id: 'transact', items: ['customers'] },
      { id: 'products', items: ['commerce', 'grouped'] },
    ]);
  });

  it('collapses parents with no visible children and no href, recursively', () => {
    // Nothing enabled: billing's invoices go, ledger (nested parent) collapses,
    // then billing itself collapses; commerce goes; grouped loses both groups.
    const result = filterNavByModules(nav, new Set());
    expect(ids(result)).toEqual([{ id: 'transact', items: ['customers'] }]);
  });

  it('keeps a parent with its own href even when its children are gone', () => {
    const sections: NavigationSection[] = [
      {
        id: 's',
        items: [
          {
            id: 'ledger',
            label: 'Ledger',
            icon: 'book2',
            href: '/ledger',
            children: [{ id: 'chart', label: 'Chart', icon: 'landmark', href: '/ledger/accounts', moduleId: 'finance' }],
          },
        ],
      },
    ];
    const result = filterNavByModules(sections, new Set());
    expect(result).toHaveLength(1);
    expect(result[0].items[0].id).toBe('ledger');
    expect(result[0].items[0].children).toEqual([]);
  });

  it('prunes child groups individually', () => {
    const result = filterNavByModules(nav, new Set(['finance']));
    const grouped = result[1].items.find((i) => i.id === 'grouped');
    expect(grouped?.childGroups?.map((g) => g.label)).toEqual(['Money']);
  });

  it('applies the extra visibility predicate to every level', () => {
    const result = filterNavByModules(nav, null, {
      isItemVisible: (item) => item.id !== 'orders-activity' && item.id !== 'customers',
    });
    // orders lost its only child and has no href, so it collapses too.
    expect(result.find((s) => s.id === 'transact')).toBeUndefined();
  });

  it('does not mutate the input tree', () => {
    const before = JSON.stringify(nav);
    filterNavByModules(nav, new Set());
    expect(JSON.stringify(nav)).toBe(before);
  });
});

describe('matchesRoutePath', () => {
  it('matches exact paths and ignores trailing slashes, query and hash', () => {
    expect(matchesRoutePath('/orders/activity', '/orders/activity')).toBe(true);
    expect(matchesRoutePath('/orders/activity', '/orders/activity/')).toBe(true);
    expect(matchesRoutePath('/orders/activity', '/orders/activity?page=2#top')).toBe(true);
    expect(matchesRoutePath('/orders/activity', '/orders')).toBe(false);
    expect(matchesRoutePath('/orders', '/orders/activity')).toBe(false);
  });

  it('matches dynamic segments', () => {
    expect(matchesRoutePath('/billing/invoices/:id', '/billing/invoices/abc-123')).toBe(true);
    expect(matchesRoutePath('/billing/invoices/:id', '/billing/invoices')).toBe(false);
    expect(matchesRoutePath('/billing/invoices/:id', '/billing/invoices/abc/extra')).toBe(false);
    expect(matchesRoutePath('/catalog/billers/:billerId/services/:serviceId', '/catalog/billers/1/services/2')).toBe(true);
  });

  it('supports a trailing wildcard', () => {
    expect(matchesRoutePath('/commerce/*', '/commerce/products/9')).toBe(true);
    expect(matchesRoutePath('/commerce/*', '/orders')).toBe(false);
  });
});

describe('resolveDisabledModuleForPath', () => {
  it('returns null without a manifest', () => {
    expect(resolveDisabledModuleForPath(registry, null, '/orders/activity')).toBeNull();
    expect(resolveDisabledModuleForPath(registry, undefined, '/orders/activity')).toBeNull();
  });

  it('returns null when the owning module is enabled', () => {
    expect(resolveDisabledModuleForPath(registry, manifest(['finance', 'agents']), '/orders/activity')).toBeNull();
  });

  it('returns null when no registered route matches', () => {
    expect(resolveDisabledModuleForPath(registry, manifest([]), '/definitely/not/a/route')).toBeNull();
  });

  it('resolves the disabled module for an exact route', () => {
    expect(resolveDisabledModuleForPath(registry, manifest(['agents']), '/orders/activity')).toEqual({
      uiModuleId: 'finance',
      backendModuleId: 'finance',
      name: 'Finance',
    });
  });

  it('resolves the disabled module for a dynamic route', () => {
    expect(resolveDisabledModuleForPath(registry, manifest(['finance']), '/commerce/products/sku-42')).toEqual({
      uiModuleId: 'commerce',
      backendModuleId: 'commerce',
      name: 'Commerce',
    });
  });

  it('falls back to the UI module name when the manifest does not describe the backend module', () => {
    const m = manifest([]);
    m.modules = [];
    expect(resolveDisabledModuleForPath(registry, m, '/approvals')).toEqual({
      uiModuleId: 'agent-command-center',
      backendModuleId: 'agents',
      name: 'Agent-command-center',
    });
  });

  it('never flags routes of modules with no requires', () => {
    expect(resolveDisabledModuleForPath(registry, manifest([]), '/settings')).toBeNull();
    expect(resolveDisabledModuleForPath(registry, manifest([]), '/tenants/abc')).toBeNull();
    expect(resolveDisabledModuleForPath(registry, manifest([]), '/ai/models')).toBeNull();
  });
});

describe('pathRequiresBackendModule', () => {
  it('is true when the current page is owned by a UI module that requires the refused module', () => {
    expect(pathRequiresBackendModule(registry, 'finance', '/orders/activity')).toBe(true);
    expect(pathRequiresBackendModule(registry, 'finance', '/billing/invoices/42?tab=lines')).toBe(true);
    expect(pathRequiresBackendModule(registry, 'commerce', '/commerce/products/p1')).toBe(true);
  });

  it('is false for a shared page that merely called a gated endpoint (no redirect, no loop)', () => {
    expect(pathRequiresBackendModule(registry, 'finance', '/')).toBe(false);
    expect(pathRequiresBackendModule(registry, 'finance', '/settings')).toBe(false);
    expect(pathRequiresBackendModule(registry, 'finance', '/tenants/abc')).toBe(false);
  });

  it('is false when the page belongs to a different module than the one refused', () => {
    expect(pathRequiresBackendModule(registry, 'finance', '/commerce')).toBe(false);
    expect(pathRequiresBackendModule(registry, 'commerce', '/orders/activity')).toBe(false);
  });

  it('never matches modules with no requires', () => {
    expect(pathRequiresBackendModule(registry, 'platform', '/settings')).toBe(false);
  });
});

describe('isBackendModuleEnabled', () => {
  it('is fail-open without a manifest', () => {
    expect(isBackendModuleEnabled(null, 'finance')).toBe(true);
    expect(isBackendModuleEnabled(undefined, 'finance')).toBe(true);
  });

  it('reads enabledModules from the manifest', () => {
    expect(isBackendModuleEnabled(manifest(['finance']), 'finance')).toBe(true);
    expect(isBackendModuleEnabled(manifest(['finance']), 'commerce')).toBe(false);
  });
});


// ---------------------------------------------------------------------------
// Per-route module requirements (Spec 097 §10.2). A UI module is a packaging
// unit, not a data boundary: the always-enabled platform module registers the
// speech and document pages, and the finance module registers the customer
// account pages, each of which draws from a different backend module.
// ---------------------------------------------------------------------------

const crossModuleRegistry: AdminModule[] = [
  {
    ...uiModule('platform', undefined, ['/settings']),
    routes: [
      { path: '/settings', element: Noop },
      { path: '/settings/speech', element: Noop, requires: ['voice'] },
      { path: '/compliance/documents', element: Noop, requires: ['documents'] },
    ],
  },
  {
    ...uiModule('finance', ['finance'], ['/orders/activity']),
    routes: [
      { path: '/orders/activity', element: Noop },
      { path: '/accounts', element: Noop, requires: ['personal-finance'] },
    ],
  },
];

describe('filterRoutesByBackendModules', () => {
  const routes = crossModuleRegistry.flatMap((m) => m.routes);

  it('keeps everything without a manifest', () => {
    expect(filterRoutesByBackendModules(routes, undefined)).toHaveLength(routes.length);
  });

  it('drops only the routes whose own module is off', () => {
    const kept = filterRoutesByBackendModules(routes, ['platform', 'finance', 'documents', 'personal-finance']);
    expect(kept.map((r) => r.path)).toEqual(['/settings', '/compliance/documents', '/orders/activity', '/accounts']);
  });

  it('keeps routes that require nothing', () => {
    const kept = filterRoutesByBackendModules(routes, ['platform']);
    expect(kept.map((r) => r.path)).toEqual(['/settings', '/orders/activity']);
  });
});

describe('per-route requirements', () => {
  const withVoiceOff = manifest(['platform', 'finance', 'documents', 'personal-finance']);

  it('explains a route whose own module is off even though its owner is enabled', () => {
    const match = resolveDisabledModuleForPath(crossModuleRegistry, withVoiceOff, '/settings/speech');
    expect(match?.backendModuleId).toBe('voice');
    expect(match?.uiModuleId).toBe('platform');
  });

  it('leaves a sibling route in the same module alone', () => {
    expect(resolveDisabledModuleForPath(crossModuleRegistry, withVoiceOff, '/settings')).toBeNull();
  });

  it('explains an account page when personal finance is off but finance is on', () => {
    const match = resolveDisabledModuleForPath(
      crossModuleRegistry,
      manifest(['platform', 'finance', 'voice', 'documents']),
      '/accounts',
    );
    expect(match?.backendModuleId).toBe('personal-finance');
  });

  it('lets the interceptor recognise a route by its own requirement', () => {
    expect(pathRequiresBackendModule(crossModuleRegistry, 'voice', '/settings/speech')).toBe(true);
    expect(pathRequiresBackendModule(crossModuleRegistry, 'personal-finance', '/accounts')).toBe(true);
    expect(pathRequiresBackendModule(crossModuleRegistry, 'voice', '/settings')).toBe(false);
  });
});
