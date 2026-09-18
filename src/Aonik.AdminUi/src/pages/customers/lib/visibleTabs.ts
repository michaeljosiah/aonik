// Customer-detail tab visibility (Spec 097 §10.1) — pure, React-free so the
// node-environment vitest suite exercises the exact rule the page renders.
//
// The customer registry is Platform-owned and stays reachable whatever modules
// are on; each DOMAIN tab is tagged with the backend module that serves its
// data and is hidden when the manifest says that module is off. A tab that
// only fails with `module.disabled` after being clicked is worse than no tab.
//
// Absent manifest (`undefined` / `null` enabled list) fails OPEN — every tab
// renders — matching useModules' own degradation.

import type { RuntimeModuleManifest } from '@/modules/types';

export type CustomerTabKey =
  | 'overview'
  | 'finance'
  | 'orders'
  | 'commerce'
  | 'insights'
  | 'documents'
  | 'activity';

export type FinanceSubTabKey = 'accounts' | 'transactions' | 'budgets' | 'commitments' | 'graph';

/** A tab (or sub-tab) whose data is served by an optional backend module. */
export interface ModuleGatedTab<TKey extends string> {
  value: TKey;
  label: string;
  /** Backend module id that serves this tab's data; absent = Platform-owned, always visible. */
  module?: string;
}

/** Backend module ids (Spec 097 §5) the customer view depends on. */
export const MODULE_FINANCE = 'finance';
export const MODULE_PERSONAL_FINANCE = 'personal-finance';
export const MODULE_COMMERCE = 'commerce';
export const MODULE_DOCUMENTS = 'documents';

/**
 * One party, every lens (Spec 081). Ownership:
 *  - Overview, Insights, Activity — Platform (customer registry endpoints).
 *  - Finance — hosts the personal-finance sub-tabs; its `module` is the
 *    umbrella Finance module and each sub-tab carries its own owner.
 *  - Orders — `orderService.listOrders` and the `/orders/:id` detail page
 *    both live in the Finance module.
 *  - Commerce — storefront record, Commerce module.
 *  - Documents — `/documents` is served by the Documents module.
 */
export const CUSTOMER_TABS: ReadonlyArray<ModuleGatedTab<CustomerTabKey>> = [
  { value: 'overview', label: 'Overview' },
  { value: 'finance', label: 'Finance', module: MODULE_FINANCE },
  { value: 'orders', label: 'Orders', module: MODULE_FINANCE },
  { value: 'commerce', label: 'Commerce', module: MODULE_COMMERCE },
  { value: 'insights', label: 'Insights' },
  { value: 'documents', label: 'Documents', module: MODULE_DOCUMENTS },
  { value: 'activity', label: 'Activity' },
];

/** Every Finance sub-tab reads `/personal-finance/*` (accounts, transactions, budgets, commitments, life graph). */
export const FINANCE_SUB_TABS: ReadonlyArray<ModuleGatedTab<FinanceSubTabKey>> = [
  { value: 'accounts', label: 'Accounts', module: MODULE_PERSONAL_FINANCE },
  { value: 'transactions', label: 'Transactions', module: MODULE_PERSONAL_FINANCE },
  { value: 'budgets', label: 'Budgets', module: MODULE_PERSONAL_FINANCE },
  { value: 'commitments', label: 'Commitments', module: MODULE_PERSONAL_FINANCE },
  { value: 'graph', label: 'Financial graph', module: MODULE_PERSONAL_FINANCE },
];

/** The enabled backend module ids, or `undefined`/`null` when there is no manifest. */
export type EnabledModuleIds = ReadonlyArray<string> | null | undefined;

/**
 * Whether one tab renders. The single rule: an untagged tab always renders;
 * a tagged tab renders when its module is enabled; no manifest fails open.
 */
export function isTabVisible(tab: { module?: string }, enabledModules: EnabledModuleIds): boolean {
  if (!tab.module) return true;
  if (enabledModules === undefined || enabledModules === null) return true;
  return enabledModules.includes(tab.module);
}

/** Filter a tab list to the ones that render for these enabled modules. */
export function filterVisibleTabs<T extends { module?: string }>(
  tabs: ReadonlyArray<T>,
  enabledModules: EnabledModuleIds,
): T[] {
  return tabs.filter((tab) => isTabVisible(tab, enabledModules));
}

/** Resolved tab set for one manifest state. */
export interface CustomerTabVisibility {
  tabs: ModuleGatedTab<CustomerTabKey>[];
  financeSubTabs: ModuleGatedTab<FinanceSubTabKey>[];
}

/**
 * Resolve every tab and Finance sub-tab visible under `manifest`. The
 * Finance tab is gated by the Finance module; inside it, each sub-tab is
 * gated by the module that actually serves it, so a tenant with Finance on
 * but Personal Finance off keeps the tab and loses the sub-tabs.
 */
export function resolveCustomerTabs(
  manifest: Pick<RuntimeModuleManifest, 'enabledModules'> | null | undefined,
): CustomerTabVisibility {
  const enabled = manifest?.enabledModules;
  return {
    tabs: filterVisibleTabs(CUSTOMER_TABS, enabled),
    financeSubTabs: filterVisibleTabs(FINANCE_SUB_TABS, enabled),
  };
}

/**
 * Keep the active tab on something that still renders. A module toggled off
 * while its tab is open (the manifest re-fetches after a toggle) must not
 * leave the page on a tab with no button and no content.
 */
export function ensureVisibleTab<TKey extends string>(
  active: TKey,
  visible: ReadonlyArray<{ value: TKey }>,
  fallback: TKey,
): TKey {
  if (visible.some((tab) => tab.value === active)) return active;
  return visible.some((tab) => tab.value === fallback) ? fallback : (visible[0]?.value ?? fallback);
}
