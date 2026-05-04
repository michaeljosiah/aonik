import type { NavItem, NavigationSection } from '@/types';

// Starterkit-first sidebar tree. Route targets are mapped to the closest live
// admin pages until every dedicated starterkit surface exists in AONIK.
export const STARTERKIT_SIDEBAR_NAV: NavigationSection[] = [
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
        badge: '4',
        children: [
          { id: 'orders-activity', label: 'All orders', icon: 'list', href: '/orders/activity' },
          { id: 'orders-bill-payments-new', label: 'New order', icon: 'plus', href: '/orders/bill-payments/new' },
          { id: 'order-items', label: 'Item monitor', icon: 'activity', badge: '2', href: '/orders' },
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
        badge: '7',
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
          { id: 'bill-history', label: 'Recent activity', icon: 'list', href: '/orders/activity' },
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
          { id: 'remit-history', label: 'Recent activity', icon: 'list', href: '/orders/activity' },
        ],
      },
      {
        id: 'billing',
        label: 'Billing',
        icon: 'book',
        children: [
          { id: 'billing-invoices', label: 'Invoices', icon: 'invoice', href: '/billing/invoices' },
          { id: 'bank-accounts', label: 'Customer accounts', icon: 'users2', href: '/accounts' },
          { id: 'collections', label: 'Collections', icon: 'arrows', badge: '3', href: '/billing/invoices' },
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
      {
        id: 'personal-finance',
        label: 'Personal Finance',
        icon: 'bank',
        children: [
          { id: 'wallets', label: 'Wallets', icon: 'bank', href: '/accounts' },
          { id: 'savings', label: 'Savings', icon: 'chart', href: '/accounts' },
          { id: 'transfers', label: 'Transfers', icon: 'payout', href: '/orders/activity' },
        ],
      },
    ],
  },
  {
    id: 'platform',
    label: 'Platform',
    items: [
      {
        id: 'compliance',
        label: 'Compliance',
        icon: 'clipcheck',
        badge: '2',
        href: '/compliance',
      },
      {
        id: 'ai-agents-parent',
        label: 'AI & Agents',
        icon: 'sparkles',
        children: [
          { id: 'ai-playground-item', label: 'Playground', icon: 'bot', href: '/ai/playground' },
          { id: 'ai-agents-item', label: 'Agents', icon: 'sparkles', href: '/ai/agents' },
          { id: 'ai-workflows-item', label: 'Workflows', icon: 'workflow', href: '/ai/workflows' },
          { id: 'ai-tasks-item', label: 'Tasks', icon: 'clipcheck', badge: '3', href: '/ai/tasks' },
          { id: 'ai-policies', label: 'Policies', icon: 'shield', href: '/ai/policies' },
          { id: 'ai-usage', label: 'Usage', icon: 'chart', href: '/ai/usage' },
        ],
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
        href: '/settings',
        audience: 'host',
      },
      {
        id: 'tenants',
        label: 'Tenants',
        icon: 'building',
        href: '/tenants',
        audience: 'host',
      },
      // System Tools intentionally removed from main sidebar — already
      // exposed as a card on the /settings landing page. Direct link
      // /settings/system-tools and route wiring still resolve.
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
