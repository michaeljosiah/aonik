export type AnalyticsTrend = {
  direction: 'up' | 'down' | 'neutral';
  value: string;
  label: string;
};

export type AnalyticsCard = {
  id: string;
  title: string;
  description: string;
  value: string;
  valueLabel?: string;
  trend?: AnalyticsTrend;
  footerLabel?: string;
  footerHref?: string;
  sparkline?: number[];
  accent?: string;
};

export type AnalyticsQuickAction = {
  id: string;
  label: string;
  href: string;
};

export const analyticsOverviewCards: AnalyticsCard[] = [
  {
    id: 'weekly-insights',
    title: 'Weekly insights',
    description: 'AI digest across billing, payments, and ledger activity.',
    value: '3 highlights',
    valueLabel: '2 anomalies, 1 opportunity',
    footerLabel: 'View insights',
    footerHref: '/ai/chat',
    accent: '#2C7BE5',
  },
  {
    id: 'cash-runway',
    title: 'Cash runway',
    description: 'Based on net burn and liquid balances.',
    value: '7.4 months',
    valueLabel: 'Burn rate steady',
    trend: { direction: 'up', value: '4.2%', label: 'vs last month' },
    footerLabel: 'View runway',
    footerHref: '/ledger/accounts',
    accent: '#0EA5E9',
  },
  {
    id: 'transaction-flow',
    title: 'Transaction flow',
    description: 'Net cash movement across payments and orders.',
    value: 'GBP 48.3K',
    valueLabel: 'Inflow +61.2K, Outflow -12.9K',
    trend: { direction: 'up', value: '12.1%', label: 'vs last 30 days' },
    footerLabel: 'View transactions',
    footerHref: '/payments/transactions',
    accent: '#14B8A6',
  },
  {
    id: 'active-customers',
    title: 'Active customers',
    description: 'Parties with activity in the selected period.',
    value: '1,284',
    valueLabel: '94 new this period',
    trend: { direction: 'up', value: '7.9%', label: 'growth rate' },
    footerLabel: 'View customers',
    footerHref: '/billing/customers',
    accent: '#F97316',
  },
];

export const analyticsPerformanceCards: AnalyticsCard[] = [
  {
    id: 'revenue-summary',
    title: 'Revenue summary',
    description: 'Net revenue across all billing products.',
    value: 'GBP 312.4K',
    valueLabel: 'Gross margin 34.8%',
    trend: { direction: 'up', value: '5.4%', label: 'vs prior period' },
    footerLabel: 'View revenue',
    footerHref: '/billing/invoices',
    accent: '#22C55E',
  },
  {
    id: 'forecast',
    title: 'Forecast',
    description: 'AI projection for the next 30 days.',
    value: 'Next month +86.2K',
    valueLabel: 'Scenario: base case',
    sparkline: [4, 6, 6, 7, 8, 6, 5, 7, 9, 8, 6],
    footerLabel: 'View forecast',
    footerHref: '/analytics',
    accent: '#6366F1',
  },
  {
    id: 'order-pipeline',
    title: 'Order pipeline',
    description: 'Orders by status across all products.',
    value: '1,248 open',
    valueLabel: '912 completed, 48 failed',
    trend: { direction: 'down', value: '3.1%', label: 'pending backlog' },
    footerLabel: 'View orders',
    footerHref: '/orders',
    accent: '#A855F7',
  },
  {
    id: 'ai-activity',
    title: 'AI agent activity',
    description: 'Proposals and approvals in the last 7 days.',
    value: '214 proposals',
    valueLabel: '187 approved, 9 escalated',
    trend: { direction: 'up', value: '18.7%', label: 'automation coverage' },
    footerLabel: 'View agents',
    footerHref: '/ai/agents',
    accent: '#0F766E',
  },
];

export const analyticsQuickActions: AnalyticsQuickAction[] = [
  { id: 'burn-rate', label: 'Burn rate analysis', href: '/ledger/accounts' },
  { id: 'latest-transactions', label: 'Latest transactions', href: '/payments/transactions' },
  { id: 'expense-breakdown', label: 'Expense breakdown', href: '/ledger/journal-entries' },
  { id: 'compliance-overview', label: 'Compliance overview', href: '/settings/audit-logs' },
  { id: 'partner-performance', label: 'Partner performance', href: '/catalog/billers' },
  { id: 'growth-trends', label: 'Growth trends', href: '/analytics' },
];
