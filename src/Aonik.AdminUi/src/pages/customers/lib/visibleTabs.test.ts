import { describe, expect, it } from 'vitest';

import {
  CUSTOMER_TABS,
  FINANCE_SUB_TABS,
  MODULE_COMMERCE,
  MODULE_DOCUMENTS,
  MODULE_FINANCE,
  MODULE_PERSONAL_FINANCE,
  ensureVisibleTab,
  filterVisibleTabs,
  isTabVisible,
  resolveCustomerTabs,
  type CustomerTabKey,
  type FinanceSubTabKey,
} from './visibleTabs';

const ALL_TABS: CustomerTabKey[] = [
  'overview', 'finance', 'orders', 'commerce', 'insights', 'documents', 'activity',
];
const ALL_FINANCE_SUBS: FinanceSubTabKey[] = [
  'accounts', 'transactions', 'budgets', 'commitments', 'graph',
];
const EVERYTHING = [
  'platform', 'ordering', MODULE_FINANCE, MODULE_COMMERCE, MODULE_PERSONAL_FINANCE, MODULE_DOCUMENTS,
];

function without(...off: string[]): string[] {
  return EVERYTHING.filter((id) => !off.includes(id));
}

function tabKeys(manifestEnabled: string[] | undefined | null) {
  const { tabs, financeSubTabs } = resolveCustomerTabs(
    manifestEnabled === undefined || manifestEnabled === null ? manifestEnabled : { enabledModules: manifestEnabled },
  );
  return { tabs: tabs.map((t) => t.value), subs: financeSubTabs.map((t) => t.value) };
}

describe('customer detail tab ownership', () => {
  it('tags every domain tab with the backend module that serves it and leaves the Platform lenses untagged', () => {
    const byKey = Object.fromEntries(CUSTOMER_TABS.map((t) => [t.value, t.module]));
    expect(byKey).toEqual({
      overview: undefined,
      finance: MODULE_FINANCE,
      orders: MODULE_FINANCE,
      commerce: MODULE_COMMERCE,
      insights: undefined,
      documents: MODULE_DOCUMENTS,
      activity: undefined,
    });
  });

  it('tags every Finance sub-tab with personal-finance (they all read /personal-finance/*)', () => {
    expect(FINANCE_SUB_TABS.map((t) => t.value)).toEqual(ALL_FINANCE_SUBS);
    expect(FINANCE_SUB_TABS.every((t) => t.module === MODULE_PERSONAL_FINANCE)).toBe(true);
  });
});

describe('resolveCustomerTabs', () => {
  it('renders every tab and sub-tab when there is no manifest (fail-open)', () => {
    expect(tabKeys(undefined)).toEqual({ tabs: ALL_TABS, subs: ALL_FINANCE_SUBS });
    expect(tabKeys(null)).toEqual({ tabs: ALL_TABS, subs: ALL_FINANCE_SUBS });
  });

  it('renders every tab when every module is enabled', () => {
    expect(tabKeys(EVERYTHING)).toEqual({ tabs: ALL_TABS, subs: ALL_FINANCE_SUBS });
  });

  it('hides Orders and Finance when finance is off, keeping Commerce and the Platform lenses', () => {
    const { tabs } = tabKeys(without(MODULE_FINANCE));
    expect(tabs).not.toContain('orders');
    expect(tabs).not.toContain('finance');
    expect(tabs).toEqual(['overview', 'commerce', 'insights', 'documents', 'activity']);
  });

  it('hides the Personal Finance sub-tabs when personal-finance is off while the Finance tab remains', () => {
    const { tabs, subs } = tabKeys(without(MODULE_PERSONAL_FINANCE));
    expect(tabs).toContain('finance');
    expect(tabs).toContain('orders');
    expect(subs).toEqual([]);
  });

  it('hides Commerce when commerce is off, keeping everything else', () => {
    const { tabs, subs } = tabKeys(without(MODULE_COMMERCE));
    expect(tabs).toEqual(['overview', 'finance', 'orders', 'insights', 'documents', 'activity']);
    expect(subs).toEqual(ALL_FINANCE_SUBS);
  });

  it('hides Documents when documents is off', () => {
    const { tabs } = tabKeys(without(MODULE_DOCUMENTS));
    expect(tabs).toEqual(['overview', 'finance', 'orders', 'commerce', 'insights', 'activity']);
  });

  it('always keeps the Platform-owned overview, insights and activity lenses', () => {
    const { tabs, subs } = tabKeys(['platform']);
    expect(tabs).toEqual(['overview', 'insights', 'activity']);
    expect(subs).toEqual([]);
  });
});

describe('isTabVisible / filterVisibleTabs', () => {
  it('always shows an untagged tab', () => {
    expect(isTabVisible({}, [])).toBe(true);
    expect(isTabVisible({ module: undefined }, ['platform'])).toBe(true);
  });

  it('shows a tagged tab only when its module is enabled, failing open without a manifest', () => {
    expect(isTabVisible({ module: MODULE_FINANCE }, [MODULE_FINANCE])).toBe(true);
    expect(isTabVisible({ module: MODULE_FINANCE }, ['platform'])).toBe(false);
    expect(isTabVisible({ module: MODULE_FINANCE }, undefined)).toBe(true);
    expect(isTabVisible({ module: MODULE_FINANCE }, null)).toBe(true);
  });

  it('preserves order while filtering', () => {
    const tabs = [
      { value: 'a', module: MODULE_FINANCE },
      { value: 'b' },
      { value: 'c', module: MODULE_COMMERCE },
    ];
    expect(filterVisibleTabs(tabs, [MODULE_COMMERCE]).map((t) => t.value)).toEqual(['b', 'c']);
  });
});

describe('ensureVisibleTab', () => {
  const visible = [{ value: 'overview' }, { value: 'insights' }] as const;

  it('keeps the requested tab when it is still visible', () => {
    expect(ensureVisibleTab('insights', visible, 'overview')).toBe('insights');
  });

  it('falls back to the fallback tab when the requested one was hidden by a toggle', () => {
    expect(ensureVisibleTab('orders', visible, 'overview')).toBe('overview');
  });

  it('falls back to the first visible tab when even the fallback is hidden', () => {
    expect(ensureVisibleTab('graph', [{ value: 'budgets' }], 'accounts')).toBe('budgets');
  });

  it('returns the fallback when nothing is visible so callers can render an empty state', () => {
    expect(ensureVisibleTab('graph', [], 'accounts')).toBe('accounts');
  });
});
