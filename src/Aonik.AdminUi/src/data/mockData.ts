import type { NavigationSection, User, ActivityItem, QuickLink, AppCard, AgentCard, Databox } from '@/types';

export const currentUser: User = {
  id: '1',
  name: 'Oliver Chen',
  role: 'Platform Administrator',
  avatar: undefined,
};

export const navigationSections: NavigationSection[] = [
  {
    id: 'cross-functional',
    label: 'Home',
    items: [
      {
        id: 'dashboard',
        label: 'Dashboard',
        icon: 'LayoutDashboard',
        href: '/',
      },
    ],
  },
  {
    id: 'platform-core',
    label: 'Finance',
    items: [
      {
        id: 'identity-access',
        label: 'Access',
        icon: 'Users',
        viewAllHref: '/access/users',
        viewAllLabel: 'View all',
        audience: 'host',
        childGroups: [
          {
            label: 'Team',
            items: [
              { id: 'users', label: 'Users', icon: 'UserCog', href: '/access/users', audience: 'host' },
            ],
          },
          {
            label: 'Permissions',
            items: [
              { id: 'roles', label: 'Roles', icon: 'Shield', href: '/access/roles', audience: 'host' },
              { id: 'permissions', label: 'Permissions', icon: 'Key', href: '/access/permissions', audience: 'host' },
            ],
          },
        ],
      },
      {
        id: 'party-profiles',
        label: 'Customers',
        icon: 'Building2',
        href: '/customers',
      },
      {
        id: 'orders',
        label: 'Orders',
        icon: 'ClipboardList',
        viewAllHref: '/orders',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Actions',
            items: [
              { id: 'order-bill-payments', label: 'New Bill Payment', icon: 'Receipt', href: '/orders/bill-payments/new' },
            ],
          },
        ],
      },
      {
        id: 'partner-network-routing',
        label: 'Network',
        icon: 'Network',
        viewAllHref: '/catalog',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Coverage',
            items: [
              { id: 'catalog-countries', label: 'Countries', icon: 'Globe', href: '/catalog/countries' },
            ],
          },
          {
            label: 'Partners',
            items: [
              { id: 'catalog-partners', label: 'Partners', icon: 'Network', href: '/catalog/partners' },
              { id: 'catalog-billers', label: 'Billers', icon: 'Building2', href: '/catalog/billers' },
              { id: 'catalog-categories', label: 'Categories', icon: 'Grid3x3', href: '/catalog/categories' },
            ],
          },
        ],
      },
      {
        id: 'pricing-policy',
        label: 'Pricing',
        icon: 'ArrowRightLeft',
        viewAllHref: '/settings/fx-rates',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Rates & Rules',
            items: [
              { id: 'fx-rates', label: 'FX Rates', icon: 'ArrowRightLeft', href: '/settings/fx-rates' },
              { id: 'autonumbering', label: 'Autonumbering', icon: 'Hash', href: '/settings/autonumbering' },
            ],
          },
        ],
      },
      {
        id: 'compliance-risk',
        label: 'Compliance',
        icon: 'ClipboardCheck',
        viewAllHref: '/compliance',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Screening',
            items: [
              { id: 'compliance-documents', label: 'Documents', icon: 'FileText', href: '/compliance/documents' },
            ],
          },
        ],
      },
    ],
  },
  {
    id: 'platform-admin',
    label: 'Admin',
    audience: 'host',
    items: [
      {
        id: 'tenants',
        label: 'Tenants',
        icon: 'Building',
        href: '/tenants',
      },
      {
        id: 'system-tools',
        label: 'System Tools',
        icon: 'Wrench',
        href: '/settings/system-tools',
      },
      {
        id: 'catalog',
        label: 'Catalog',
        icon: 'Store',
        viewAllHref: '/catalog',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Overview',
            items: [
              { id: 'catalog-overview', label: 'Home', icon: 'Store', href: '/catalog' },
            ],
          },
        ],
      },
      {
        id: 'cms',
        label: 'Content',
        icon: 'Layers',
        viewAllHref: '/cms/content-blocks',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Library',
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
  { id: '1', label: 'New Bill Payment', icon: 'Receipt', href: '/orders/bill-payments/new' },
  { id: '2', label: 'Users', icon: 'UserCog', href: '/access/users' },
  { id: '3', label: 'Customers', icon: 'Building2', href: '/customers' },
  { id: '4', label: 'Documents', icon: 'FileText', href: '/compliance/documents' },
  { id: '5', label: 'Tenants', icon: 'Building', href: '/tenants' },
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
