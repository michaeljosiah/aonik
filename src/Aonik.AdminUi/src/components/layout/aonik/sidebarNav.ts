import type { NavItem, NavigationSection } from '@/types';

// Authoritative sidebar navigation tree for the Admin UI. Defines the
// top-level sections (HOME / TRANSACT / PRODUCTS / PLATFORM / HOST), their
// items, and child links. AonikSidebar.tsx imports `SIDEBAR_NAV` directly
// from here.
//
// Note: the per-module `navigation` arrays in `modules/*/index.ts` are
// aggregated by `getAggregatedNavigation()` but are not currently wired
// into the rendered sidebar — this file is the live source. Audience
// filtering ('host' | 'tenant' | 'all') is applied at render time.
export const SIDEBAR_NAV: NavigationSection[] = [
  {
    id: 'home',
    label: 'Home',
    items: [
      {
        id: 'dashboard',
        label: 'My Space',
        icon: 'home',
        href: '/',
      },
    ],
  },
  {
    id: 'transact',
    label: 'Transact',
    items: [
      {
        id: 'orders',
        label: 'Orders',
        icon: 'receipt',
        children: [
          { id: 'orders-activity', label: 'All orders', icon: 'list', href: '/orders/activity' },
          { id: 'orders-bill-payments-new', label: 'New order', icon: 'plus', href: '/orders/bill-payments/new' },
        ],
      },
      {
        id: 'customers',
        label: 'Customers',
        icon: 'users2',
        href: '/customers',
      },
      {
        id: 'approvals',
        label: 'Approvals',
        icon: 'clipcheck',
        href: '/approvals',
      },
    ],
  },
  {
    id: 'products',
    label: 'Products',
    items: [
      {
        id: 'bill-payments',
        label: 'Bill Payments',
        icon: 'invoice',
        children: [
          { id: 'catalog-billers', label: 'Billers', icon: 'building', href: '/catalog/billers' },
          { id: 'catalog-categories', label: 'Categories', icon: 'tag', href: '/catalog/categories' },
        ],
      },
      {
        id: 'remittances',
        label: 'Remittances',
        icon: 'globe2',
        children: [
          { id: 'catalog-countries', label: 'Corridors', icon: 'route', href: '/catalog/countries' },
          { id: 'catalog-partners', label: 'Partners', icon: 'network', href: '/catalog/partners' },
          { id: 'settings-fx-rates', label: 'FX & Rates', icon: 'arrows', href: '/settings/fx-rates' },
        ],
      },
      {
        id: 'billing',
        label: 'Billing',
        icon: 'book',
        children: [
          { id: 'billing-invoices', label: 'Invoices', icon: 'invoice', href: '/billing/invoices' },
          { id: 'bank-accounts', label: 'Customer accounts', icon: 'users2', href: '/accounts' },
          {
            id: 'ledger',
            label: 'Ledger',
            icon: 'book2',
            href: '/ledger',
            children: [
              { id: 'ledger-overview', label: 'Ledgers', icon: 'book', href: '/ledger' },
              { id: 'ledger-accounts', label: 'Chart of accounts', icon: 'landmark', href: '/ledger/accounts' },
              { id: 'ledger-journal-entries', label: 'Journal entries', icon: 'invoice', href: '/ledger/journal-entries' },
            ],
          },
        ],
      },
      // Personal Finance nav entry intentionally omitted — no dedicated
      // PF admin surfaces exist today (the template's Wallets / Savings /
      // Transfers all routed to /accounts or /orders/activity). Re-add
      // when real PF admin pages land.
    ],
  },
  {
    id: 'platform',
    label: 'Platform',
    items: [
      {
        id: 'team',
        label: 'Team',
        icon: 'users2',
        children: [
          { id: 'access-users', label: 'Users', icon: 'users', href: '/access/users' },
          { id: 'access-roles', label: 'Roles', icon: 'shield', href: '/access/roles' },
          { id: 'access-permissions', label: 'Permissions', icon: 'verified', href: '/access/permissions' },
        ],
      },
      {
        id: 'content',
        label: 'Content',
        icon: 'book',
        children: [
          { id: 'content-blocks', label: 'Content Blocks', icon: 'book2', href: '/cms/content-blocks' },
          { id: 'content-wizard', label: 'Content Wizard', icon: 'sparkles', href: '/cms/content-wizard' },
          { id: 'media-library', label: 'Media Library', icon: 'inbox', href: '/cms/media' },
        ],
      },
      {
        id: 'compliance',
        label: 'Compliance',
        icon: 'clipcheck',
        href: '/compliance',
      },
      {
        id: 'ai-agents-parent',
        label: 'AI & Agents',
        icon: 'sparkles',
        children: [
          { id: 'ai-playground-item', label: 'Playground', icon: 'bot', href: '/ai/playground' },
          { id: 'ai-agents-item', label: 'Agents', icon: 'sparkles', href: '/ai/agents' },
          // Workflows nav entry intentionally hidden — read-only display
          // until a create-workflow flow exists. Route still registered
          // in modules/agent-command-center so deep links resolve.
          { id: 'ai-tasks-item', label: 'Tasks', icon: 'clipcheck', href: '/ai/tasks' },
          { id: 'ai-policies', label: 'Policies', icon: 'shield', href: '/ai/policies' },
          { id: 'ai-usage', label: 'Usage', icon: 'chart', href: '/ai/usage' },
        ],
      },
    ],
  },
  {
    id: 'host',
    label: 'Host',
    audience: 'host',
    items: [
      {
        id: 'tenants',
        label: 'Tenants',
        icon: 'building',
        href: '/tenants',
        audience: 'host',
      },
      {
        id: 'observability',
        label: 'Observability',
        icon: 'activity',
        audience: 'host',
        children: [
          { id: 'observability-overview', label: 'Overview', icon: 'chart', href: '/admin/observability', audience: 'host' },
          { id: 'observability-traces', label: 'Traces', icon: 'gitbranch', href: '/admin/observability/traces', audience: 'host' },
          { id: 'observability-logs', label: 'Logs', icon: 'terminal', href: '/admin/observability/logs', audience: 'host' },
          { id: 'observability-audit', label: 'Audit Log', icon: 'verified', href: '/admin/observability/audit', audience: 'host' },
        ],
      },
      {
        id: 'settings-global',
        label: 'Settings',
        icon: 'settings',
        audience: 'host',
        children: [
          { id: 'settings-platform', label: 'Platform', icon: 'settings', href: '/settings/global' },
          { id: 'settings-communication', label: 'Communication', icon: 'bell', href: '/settings/communication' },
          { id: 'settings-notification-templates', label: 'Notification Templates', icon: 'list', href: '/settings/notification-templates' },
          { id: 'settings-finance', label: 'Finance', icon: 'bank', href: '/settings/payment-gateways' },
          { id: 'settings-ai', label: 'AI & Agents', icon: 'sparkles', href: '/settings/speech' },
        ],
      },
    ],
  },
];

export function collectNavItemHrefs(item: NavItem): string[] {
  const hrefs = new Set<string>();

  const visit = (current: NavItem) => {
    if (current.href) hrefs.add(current.href);
    current.children?.forEach(visit);
    current.childGroups?.forEach((group) => group.items.forEach(visit));
  };

  visit(item);
  return Array.from(hrefs);
}

export function navItemMatchesPath(item: NavItem, pathname: string): boolean {
  return collectNavItemHrefs(item).includes(pathname);
}
