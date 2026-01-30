import type { NavigationSection, User, ActivityItem, QuickLink, AppCard, AgentCard, Databox } from '@/types';

export const currentUser: User = {
  id: '1',
  name: 'Oliver Chen',
  role: 'Platform Administrator',
  avatar: undefined,
};

export const navigationSections: NavigationSection[] = [
  {
    id: 'core',
    items: [
      {
        id: 'search',
        label: 'Search',
        icon: 'Search',
        href: '/search',
      },
      {
        id: 'dashboard',
        label: 'Dashboard',
        icon: 'LayoutDashboard',
        href: '/',
      },
      {
        id: 'billing',
        label: 'Billing',
        icon: 'FileText',
        viewAllHref: '/billing',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Documents',
            items: [
              { id: 'invoices', label: 'Invoices', icon: 'Receipt', href: '/billing/invoices' },
              { id: 'customers', label: 'Customers', icon: 'Building2', href: '/billing/customers' },
            ],
          },
          {
            label: 'Management',
            items: [
              { id: 'dunning', label: 'Dunning Plans', icon: 'AlertTriangle', href: '/billing/dunning' },
            ],
          },
        ],
      },
      {
        id: 'payments',
        label: 'Payments',
        icon: 'CreditCard',
        viewAllHref: '/payments',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Activity',
            items: [
              { id: 'transactions', label: 'Transactions', icon: 'ArrowRightLeft', href: '/payments/transactions' },
              { id: 'payouts', label: 'Payouts', icon: 'Banknote', href: '/payments/payouts' },
            ],
          },
          {
            label: 'Disputes',
            items: [
              { id: 'refunds', label: 'Refunds', icon: 'RotateCcw', href: '/payments/refunds' },
              { id: 'chargebacks', label: 'Chargebacks', icon: 'ShieldAlert', href: '/payments/chargebacks' },
            ],
          },
        ],
      },
      {
        id: 'orders',
        label: 'Orders',
        icon: 'ClipboardList',
        viewAllHref: '/orders',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Bill Payments',
            items: [
              { id: 'order-bill-payments', label: 'Create Bill Payment', icon: 'Receipt', href: '/orders/bill-payments/new' },
            ],
          },
        ],
      },
      {
        id: 'ledger',
        label: 'Ledger',
        icon: 'BookOpen',
        viewAllHref: '/ledger',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Core',
            items: [
              { id: 'accounts', label: 'Accounts', icon: 'Landmark', href: '/ledger/accounts' },
              { id: 'journal-entries', label: 'Journal Entries', icon: 'ClipboardList', href: '/ledger/journal-entries' },
            ],
          },
          {
            label: 'Operations',
            items: [
              { id: 'reconciliation', label: 'Reconciliation', icon: 'GitCompare', href: '/ledger/reconciliation' },
            ],
          },
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
        id: 'ai-agents',
        label: 'AI & Agents',
        icon: 'Sparkles',
        viewAllHref: '/ai',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'AI Platform',
            items: [
              { id: 'ai-models', label: 'AI Models', icon: 'Brain', href: '/ai/models' },
              { id: 'ai-chat', label: 'AI Assistant', icon: 'MessageSquare', href: '/ai/chat' },
            ],
          },
          {
            label: 'Agent Framework',
            items: [
              { id: 'agents', label: 'Agents', icon: 'Bot', href: '/ai/agents' },
              { id: 'orchestrator', label: 'Orchestrator', icon: 'Workflow', href: '/ai/orchestrator' },
            ],
          },
        ],
      },
      {
        id: 'users-access',
        label: 'Users & Access',
        icon: 'Users',
        viewAllHref: '/access',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Identity',
            items: [
              { id: 'users', label: 'Users', icon: 'UserCog', href: '/access/users' },
            ],
          },
          {
            label: 'Authorization',
            items: [
              { id: 'roles', label: 'Roles', icon: 'Shield', href: '/access/roles' },
              { id: 'permissions', label: 'Permissions', icon: 'Key', href: '/access/permissions' },
            ],
          },
        ],
      },
      {
        id: 'catalog',
        label: 'Catalog',
        icon: 'Store',
        viewAllHref: '/catalog',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Browse',
            items: [
              { id: 'catalog-overview', label: 'Overview', icon: 'Store', href: '/catalog' },
              { id: 'catalog-countries', label: 'Countries', icon: 'Globe', href: '/catalog/countries' },
            ],
          },
          {
            label: 'Entities',
            items: [
              { id: 'catalog-categories', label: 'Categories', icon: 'Grid3x3', href: '/catalog/categories' },
              { id: 'catalog-billers', label: 'Billers', icon: 'Building2', href: '/catalog/billers' },
            ],
          },
        ],
      },
      {
        id: 'tenants',
        label: 'Tenants',
        icon: 'Building',
        href: '/tenants',
      },
      {
        id: 'settings',
        label: 'Settings',
        icon: 'Settings',
        viewAllHref: '/settings',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Configuration',
            items: [
              { id: 'general-settings', label: 'General', icon: 'Cog', href: '/settings/general' },
              { id: 'autonumbering', label: 'Autonumbering', icon: 'Hash', href: '/settings/autonumbering' },
              { id: 'fx-rates', label: 'FX Rates', icon: 'ArrowRightLeft', href: '/settings/fx-rates' },
              { id: 'webhooks', label: 'Webhooks', icon: 'Webhook', href: '/settings/webhooks' },
            ],
          },
          {
            label: 'Security & Audit',
            items: [
              { id: 'api-keys', label: 'API Keys', icon: 'KeyRound', href: '/settings/api-keys' },
              { id: 'audit-logs', label: 'Audit Logs', icon: 'ScrollText', href: '/settings/audit-logs' },
            ],
          },
        ],
      },
      {
        id: 'cms',
        label: 'Content',
        icon: 'Layers',
        viewAllHref: '/cms',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Content Blocks',
            items: [
              { id: 'content-blocks', label: 'Content Blocks', icon: 'Layers', href: '/cms/content-blocks' },
              { id: 'media-library', label: 'Media Library', icon: 'Image', href: '/cms/media' },
            ],
          },
        ],
      },
    ],
  },
];

export const activityFeed: ActivityItem[] = [
  {
    id: '1',
    title: 'Invoice batch processed',
    description: '47 invoices generated',
    timestamp: '30m ago',
    icon: 'FileText',
  },
  {
    id: '2',
    title: 'Payment reconciled',
    description: 'Wire transfer - $12,450.00',
    timestamp: '1h ago',
    icon: 'CheckCircle',
  },
  {
    id: '3',
    title: 'Anomaly detected',
    description: 'Unusual transaction pattern flagged',
    timestamp: '2h ago',
    icon: 'AlertCircle',
  },
  {
    id: '4',
    title: 'Cash flow forecast updated',
    description: 'Q2 projections ready',
    timestamp: '3h ago',
    icon: 'Calendar',
  },
];

export const quickLinks: QuickLink[] = [
  { id: '1', label: 'Create Invoice', icon: 'FilePlus', href: '/billing/invoices/new' },
  { id: '2', label: 'View Transactions', icon: 'ArrowRightLeft', href: '/payments/transactions' },
  { id: '3', label: 'Manage Users', icon: 'UserCog', href: '/access/users' },
  { id: '4', label: 'Audit Logs', icon: 'ScrollText', href: '/settings/audit-logs' },
  { id: '5', label: 'AI Assistant', icon: 'Sparkles', href: '/ai/chat' },
];

export const myApps: AppCard[] = [
  {
    id: '1',
    name: 'Invoice Manager',
    description: 'Create, manage, and track invoices. Automated dunning workflows and payment allocation with AI-assisted invoice insights.',
    icon: undefined,
    status: 'active',
    owners: [{ id: '1', name: 'David Lynn', role: 'Finance' }],
    dateModified: '23 Sept 2025',
    modifiedBy: 'David Lynn',
    tags: ['Billing', 'Finance'],
  },
  {
    id: '2',
    name: 'Reconciliation Hub',
    description: 'AI-powered transaction matching and reconciliation. Automatically detect discrepancies and suggest corrections.',
    icon: 'insights',
    iconBgColor: '#0D7377',
    status: 'active',
    owners: [
      { id: '1', name: 'Maria Gomez', role: 'Operations' },
      { id: '2', name: 'User 2' },
      { id: '3', name: 'User 3' },
      { id: '4', name: 'User 4' },
    ],
    dateModified: '23 Sept 2025',
    modifiedBy: 'Maria Gomez',
    tags: ['Reconciliation', 'AI'],
  },
  {
    id: '3',
    name: 'Cash Flow Forecaster',
    description: 'Predict future cash positions using AI models trained on your transaction history. Scenario planning included.',
    icon: undefined,
    status: 'pending',
    owners: [{ id: '1', name: 'Kiran Desai', role: 'Analytics' }],
    dateModified: '23 Sept 2025',
    modifiedBy: 'Kiran Desai',
    tags: ['Forecasting', 'AI'],
  },
  {
    id: '4',
    name: 'Fraud Detection',
    description: 'Real-time anomaly detection for transactions. AI-driven risk scoring with explainable alerts and audit trails.',
    icon: 'semanticx',
    iconBgColor: '#0D7377',
    status: 'request',
    owners: [{ id: '1', name: 'Samuel Okoro', role: 'Security' }],
    dateModified: '23 Sept 2025',
    modifiedBy: 'Samuel Okoro',
    tags: ['Security', 'Compliance'],
  },
];

export const myAgents: AgentCard[] = [
  {
    id: '1',
    name: 'Reconciliation Agent',
    description: 'Automatically matches transactions across accounts, identifies discrepancies, and suggests corrections with full audit trail.',
    visibility: 'team',
    source: 'Aonik AI',
    skills: ['Matching', 'Analysis', 'Reporting'],
    plugins: ['ledger', 'payments', 'export'],
  },
  {
    id: '2',
    name: 'Invoice Insights Agent',
    description: 'Analyzes invoice patterns, predicts payment timelines, and identifies opportunities to optimize billing workflows.',
    visibility: 'enterprise',
    source: 'Aonik AI',
    skills: ['Forecasting', 'Classification', 'Insights'],
    plugins: ['billing', 'analytics', 'notifications'],
  },
];

export const myDataboxes: Databox[] = [
  {
    id: '1',
    name: 'Q1 Revenue Analysis',
    description: 'Comprehensive breakdown of Q1 revenue streams by region and product line.',
    color: '#E8A838',
    lastModified: '06 Jan 2026',
    modifiedBy: 'Oliver Chen',
  },
  {
    id: '2',
    name: 'Payment Trends - Africa',
    description: 'Analysis of payment method adoption and transaction volumes across African markets.',
    color: '#3B82F6',
    lastModified: '07 Jan 2026',
    modifiedBy: 'Amina Nkrumah',
  },
  {
    id: '3',
    name: 'Remittance Corridor Analysis',
    description: 'Cross-border remittance flows between diaspora communities and home countries.',
    color: '#10B981',
    lastModified: '08 Jan 2026',
    modifiedBy: 'Maria Gomez',
  },
  {
    id: '4',
    name: 'Billing Optimization Report',
    description: 'Recommendations for improving invoice-to-cash cycles based on AI analysis.',
    color: '#8B5CF6',
    lastModified: '09 Jan 2026',
    modifiedBy: 'Kiran Desai',
  },
  {
    id: '5',
    name: 'Fraud Detection Metrics',
    description: 'Performance metrics and false positive rates for anomaly detection systems.',
    color: '#EF4444',
    lastModified: '10 Jan 2026',
    modifiedBy: 'Samuel Okoro',
  },
  {
    id: '6',
    name: 'Cash Flow Projections',
    description: '12-month rolling forecast with scenario analysis for different market conditions.',
    color: '#0D7377',
    lastModified: '11 Jan 2026',
    modifiedBy: 'David Lynn',
  },
  {
    id: '7',
    name: 'Customer Payment Behavior',
    description: 'Segmentation analysis of customer payment patterns and credit risk indicators.',
    color: '#F59E0B',
    lastModified: '12 Jan 2026',
    modifiedBy: 'Jaya Lim',
  },
  {
    id: '8',
    name: 'Reconciliation Efficiency',
    description: 'Tracking automated vs manual reconciliation rates and time savings.',
    color: '#EC4899',
    lastModified: '13 Jan 2026',
    modifiedBy: 'Emma Wu',
  },
  {
    id: '9',
    name: 'Multi-Currency Analytics',
    description: 'FX exposure analysis and hedging recommendations for cross-border transactions.',
    color: '#6366F1',
    lastModified: '14 Jan 2026',
    modifiedBy: 'Javier Ruiz',
  },
];
