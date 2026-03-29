import type { NavigationSection, User, ActivityItem, QuickLink, AppCard, AgentCard, Databox } from '@/types';
import type { FinancialSnapshotData } from '@/components/dashboard/FinancialSnapshotCard';

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
        label: 'My Space',
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
        viewAllHref: '/orders/activity',
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
        id: 'ledger',
        label: 'Ledger',
        icon: 'BookOpen',
        viewAllHref: '/ledger',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Core ledger',
            items: [
              { id: 'ledger-overview', label: 'Ledgers', icon: 'BookOpen', href: '/ledger' },
              { id: 'ledger-accounts', label: 'Accounts', icon: 'Landmark', href: '/ledger/accounts' },
              { id: 'ledger-journal-entries', label: 'Journal Entries', icon: 'ClipboardList', href: '/ledger/journal-entries' },
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
    title: 'New data product published',
    description: '',
    timestamp: '30m ago',
    icon: 'FileText',
  },
  {
    id: '2',
    title: 'Access request approved',
    description: 'Data Quality Workspace',
    timestamp: '5m ago',
    icon: 'CheckCircle',
  },
  {
    id: '3',
    title: 'Platform maintenance scheduled',
    description: '',
    timestamp: '1h ago',
    icon: 'Calendar',
  },
  {
    id: '4',
    title: 'Agent requires review',
    description: 'Portfolio Analysis Agent',
    timestamp: '1h ago',
    icon: 'AlertCircle',
  },
];

export const quickLinks: QuickLink[] = [
  { id: '1', label: 'Smart Docs', icon: 'Receipt', href: '/orders/bill-payments/new' },
  { id: '2', label: 'Manage Agents', icon: 'UserCog', href: '/ai/agents' },
  { id: '3', label: 'Work Browser', icon: 'Building2', href: '/workspace' },
  { id: '4', label: 'App Store', icon: 'Grid3x3', href: '/catalog' },
  { id: '5', label: 'Chat to Personal Assistant', icon: 'Sparkles', href: '/ai/chat' },
];

export const myApps: AppCard[] = [
  {
    id: '1',
    name: 'Application Name',
    description: '(3-line truncation) Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur sit amet eros euismod, varius nulla id, accumsan purus...',
    icon: undefined,
    status: 'active',
    owners: [{ id: '1', name: 'David Lynn', role: 'Analytics' }],
    dateModified: '23 Sept 2025',
    modifiedBy: 'David Lynn',
    tags: ['Finance', 'Analyst', 'Ops', 'Billing', 'Insights'],
  },
  {
    id: '2',
    name: 'Smart Insights',
    description: '(3-line truncation) Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur sit amet eros euismod, varius nulla id, accumsan purus...',
    icon: 'insights',
    iconBgColor: '#6fb8b6',
    status: 'active',
    owners: [
      { id: '1', name: 'Maria Gomez', role: 'Operations' },
      { id: '2', name: 'User 2' },
      { id: '3', name: 'User 3' },
      { id: '4', name: 'User 4' },
      { id: '5', name: 'User 5' },
      { id: '6', name: 'User 6' },
      { id: '7', name: 'User 7' },
    ],
    dateModified: '23 Sept 2025',
    modifiedBy: 'Maria Gomez',
    tags: ['Finance', 'Analyst', 'Ops', 'Billing', 'Insights'],
  },
  {
    id: '3',
    name: 'VisionEdge',
    description: '(3-line truncation) Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur sit amet eros euismod, varius nulla id, accumsan purus...',
    icon: undefined,
    status: 'pending',
    owners: [{ id: '1', name: 'David Lynn', role: 'Analytics' }],
    dateModified: '23 Sept 2025',
    modifiedBy: 'David Lynn',
    tags: ['Finance', 'Analyst', 'Ops', 'Billing', 'Insights'],
  },
  {
    id: '4',
    name: 'Semanticx',
    description: '(3-line truncation) Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur sit amet eros euismod, varius nulla id, accumsan purus...',
    icon: 'semanticx',
    iconBgColor: '#6fb8b6',
    status: 'request',
    owners: [{ id: '1', name: 'David Lynn', role: 'Analytics' }],
    dateModified: '23 Sept 2025',
    modifiedBy: 'David Lynn',
    tags: ['Finance', 'Analyst', 'Ops', 'Billing', 'Insights'],
  },
];

export const myAgents: AgentCard[] = [
  {
    id: '1',
    name: 'Agent Name',
    description: '(3-line truncation) Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur sit amet eros euismod, varius nulla id, accumsan purus...',
    visibility: 'team',
    source: 'Co-Pilot',
    skills: ['Coding', 'Research', 'Writing', 'Strategy', 'Analysis', 'Planning'],
    plugins: ['ledger', 'payments', 'export'],
  },
  {
    id: '2',
    name: 'Agent Name',
    description: '(3-line truncation) Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur sit amet eros euismod, varius nulla id, accumsan purus...',
    visibility: 'enterprise',
    source: 'Co-Pilot',
    skills: ['Coding', 'Research', 'Writing', 'Strategy', 'Analysis', 'Planning'],
    plugins: ['billing', 'analytics', 'notifications'],
  },
];

export const myDataboxes: Databox[] = [
  {
    id: '1',
    name: 'Tech Innovations in Asia',
    description: 'Exploring the latest advancements in technology across Asian markets.',
    color: '#eb5c37',
    lastModified: '06 Sep 2025',
    modifiedBy: 'Raj Patel',
  },
  {
    id: '2',
    name: 'Latin America Economic Outlook',
    description: 'A comprehensive overview of economic predictions for Latin American countries.',
    color: '#3B82F6',
    lastModified: '07 Sep 2025',
    modifiedBy: 'Maria Gomez',
  },
  {
    id: '3',
    name: "Africa's Growth Potential",
    description: 'Assessing the growth opportunities and challenges in various African nations.',
    color: '#4caf50',
    lastModified: '08 Sep 2025',
    modifiedBy: 'Amina Nkrumah',
  },
  {
    id: '4',
    name: 'Sustainable Investments in India',
    description: 'Investigating the rise of sustainable investment practices in the Indian market.',
    color: '#eb5c37',
    lastModified: '09 Sep 2025',
    modifiedBy: 'Kiran Desai',
  },
  {
    id: '5',
    name: 'Impact of COVID-19 on Emerging Economies',
    description: 'Analyzing the long-term effects of the pandemic on economic growth.',
    color: '#ebc334',
    lastModified: '10 Sep 2025',
    modifiedBy: 'Samuel Okoro',
  },
  {
    id: '6',
    name: 'Trade Wars and Their Global Effects',
    description: 'Understanding how trade disputes influence emerging market stability.',
    color: '#eb5c37',
    lastModified: '11 Sep 2025',
    modifiedBy: 'Emma Wu',
  },
  {
    id: '7',
    name: 'Regulatory Changes in Southeast Asia',
    description: 'Reviewing significant regulatory shifts and their implications for businesses.',
    color: '#eb5c37',
    lastModified: '12 Sep 2025',
    modifiedBy: 'Jaya Lim',
  },
  {
    id: '8',
    name: 'Consumer Behavior Trends in Africa',
    description: 'Exploring the evolving preferences of consumers in African markets.',
    color: '#3B82F6',
    lastModified: '13 Sep 2025',
    modifiedBy: 'Thandiwe Moyo',
  },
  {
    id: '9',
    name: 'Future of E-commerce in Latin America',
    description: 'Predicting the growth trajectory of e-commerce platforms in the region.',
    color: '#eb5c37',
    lastModified: '14 Sep 2025',
    modifiedBy: 'Javier Ruiz',
  },
];

export const myFinancialSnapshots: FinancialSnapshotData[] = [
  {
    id: 'burn-rate',
    title: 'Burn rate',
    description: 'Average monthly operating spend.',
    value: '$8,200/mo',
    valueLabel: 'Runway: 9.2 months at current rate',
    trend: { direction: 'down', value: '6.1%', label: 'vs last month' },
    sparkline: [9400, 8800, 9100, 8600, 8400, 8200],
    footerLabel: 'View burn analysis',
    footerHref: '/ledger/accounts',
    accent: '#EF4444',
  },
  {
    id: 'revenue',
    title: 'Revenue',
    description: 'Total revenue recognised this month.',
    value: '$24,500',
    valueLabel: 'This month',
    trend: { direction: 'up', value: '12.3%', label: 'vs last month' },
    sparkline: [18200, 19800, 21000, 20400, 22100, 24500],
    footerLabel: 'View revenue',
    footerHref: '/billing/invoices',
    accent: '#22C55E',
  },
  {
    id: 'outstanding-invoices',
    title: 'Outstanding invoices',
    description: 'Unpaid invoices requiring attention.',
    value: '4 unpaid',
    valueLabel: '$12,500 outstanding \u00b7 2 overdue',
    trend: { direction: 'neutral', value: '0', label: 'change this week' },
    footerLabel: 'View invoices',
    footerHref: '/billing/invoices',
    accent: '#F97316',
  },
  {
    id: 'expenses',
    title: 'Expenses',
    description: 'Total spend this month across all categories.',
    value: '$5,120',
    valueLabel: 'Top category: Payroll ($2,800)',
    trend: { direction: 'up', value: '3.4%', label: 'vs last month' },
    sparkline: [4200, 4600, 4900, 5300, 4800, 5120],
    footerLabel: 'View expenses',
    footerHref: '/ledger/journal-entries',
    accent: '#A855F7',
  },
  {
    id: 'cash-position',
    title: 'Cash position',
    description: 'Liquid balance across all linked accounts.',
    value: '$74,300',
    valueLabel: 'Across 3 accounts \u00b7 +$6,200 net this month',
    trend: { direction: 'up', value: '9.1%', label: 'vs last month' },
    sparkline: [62000, 64500, 66800, 68100, 71200, 74300],
    footerLabel: 'View accounts',
    footerHref: '/ledger/accounts',
    accent: '#0EA5E9',
  },
  {
    id: 'profit-loss',
    title: 'Profit / Loss',
    description: 'Net profit after all expenses this month.',
    value: '$19,380',
    valueLabel: 'Net margin: 42%',
    trend: { direction: 'up', value: '18.7%', label: 'vs last month' },
    sparkline: [12400, 14200, 15800, 14900, 17100, 19380],
    footerLabel: 'View P&L',
    footerHref: '/ledger/accounts',
    accent: '#055a60',
  },
];
