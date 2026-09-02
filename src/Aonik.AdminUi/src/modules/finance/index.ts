import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig, WorkspaceTemplate } from '@/workspace/types';
import {
  AccountsListPage,
  AccountConnectionDetailPage,
  AccountTransactionsPage,
} from '@/pages/accounts';
import {
  OrdersListPage,
  BillPaymentOrderFormPage,
} from '@/pages/orders';
import {
  LedgerOverviewPage,
  LedgerAccountsPage,
  LedgerJournalEntriesPage,
} from '@/pages/ledger';
import {
  CatalogLandingPage,
  CatalogCountriesPage,
  CatalogCategoriesPage,
  CatalogBillersPage,
  CatalogBillerDetailPage,
  CatalogBillerServicesPage,
  CatalogBillerServiceDetailPage,
  CatalogPartnersPage,
  CatalogPartnerDetailPage,
} from '@/pages/catalog';
import {
  InvoicesListPage,
  InvoiceFormPage,
} from '@/pages/billing';
import { AutonumberingPage } from '@/pages/settings';
import { FxRatesPage } from '@/pages/FxRatesPage';
import { InvoiceManagerPanel } from '@/workspace/apps/InvoiceManagerPanel';
import { ReconciliationHubPanel } from '@/workspace/apps/ReconciliationHubPanel';
import { redirectTo, wrapPage } from '../utils';

// ---------------------------------------------------------------------------
// Navigation
// ---------------------------------------------------------------------------
// One unlabeled operational section. Ordering is intentionally curated to
// match the current admin shell IA rather than the original starterkit
// group labels. Customers (the party registry) and Compliance are
// Platform-owned (Spec 097 §10.1) and live in the platform module, so they
// stay when Finance is off; the finance-specific customer tabs are gated
// inside the customer page instead.
const navigation: NavigationSection[] = [
  {
    id: 'operations',
    items: [
      {
        id: 'orders',
        label: 'Orders',
        icon: 'ClipboardList',
        href: '/orders/activity',
      },
      {
        id: 'accounting',
        label: 'Accounting',
        icon: 'BookOpen',
        viewAllHref: '/ledger',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Accounting',
            items: [
              { id: 'ledger-overview', label: 'Books', icon: 'BookOpen', href: '/ledger' },
              { id: 'billing-invoices', label: 'Invoices', icon: 'FileText', href: '/billing/invoices' },
              { id: 'bank-accounts', label: 'Bank accounts', icon: 'Landmark', href: '/accounts' },
              { id: 'ledger-accounts', label: 'Chart of accounts', icon: 'Landmark', href: '/ledger/accounts' },
              { id: 'ledger-journal-entries', label: 'Journal entries', icon: 'ClipboardList', href: '/ledger/journal-entries' },
            ],
          },
        ],
      },
      {
        id: 'service-catalog',
        label: 'Catalog',
        icon: 'Store',
        viewAllHref: '/catalog',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Coverage',
            items: [
              { id: 'catalog-countries', label: 'Countries', icon: 'Globe', href: '/catalog/countries' },
              { id: 'fx-rates', label: 'Exchange Rates', icon: 'ArrowRightLeft', href: '/settings/fx-rates' },
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
    ],
  },
];

// ---------------------------------------------------------------------------
// Routes
// ---------------------------------------------------------------------------
const routes = [
  { path: '/accounts', element: AccountsListPage },
  { path: '/accounts/:accountId/transactions', element: AccountTransactionsPage, isDynamic: true },
  { path: '/accounts/connections/:connectionId', element: AccountConnectionDetailPage, isDynamic: true },
  // /orders is a vestigial landing page (two tiles). Collapse it to the
  // activity list — the "Create" path is reachable from there.
  { path: '/orders', element: redirectTo('/orders/activity') },
  { path: '/orders/activity', element: OrdersListPage },
  { path: '/orders/bill-payments/new', element: BillPaymentOrderFormPage },
  { path: '/orders/bill-payments/:orderId', element: BillPaymentOrderFormPage, isDynamic: true },
  { path: '/billing/invoices', element: InvoicesListPage },
  { path: '/billing/invoices/new', element: InvoiceFormPage },
  { path: '/billing/invoices/:id', element: InvoiceFormPage, isDynamic: true },
  { path: '/ledger', element: LedgerOverviewPage },
  { path: '/ledger/accounts', element: LedgerAccountsPage },
  { path: '/ledger/journal-entries', element: LedgerJournalEntriesPage },
  { path: '/catalog', element: CatalogLandingPage },
  { path: '/catalog/countries', element: CatalogCountriesPage },
  { path: '/catalog/categories', element: CatalogCategoriesPage },
  { path: '/catalog/billers', element: CatalogBillersPage },
  { path: '/catalog/billers/:billerId', element: CatalogBillerDetailPage, isDynamic: true },
  { path: '/catalog/billers/:billerId/services', element: CatalogBillerServicesPage, isDynamic: true },
  { path: '/catalog/billers/:billerId/services/:serviceId', element: CatalogBillerServiceDetailPage, isDynamic: true },
  { path: '/catalog/partners', element: CatalogPartnersPage },
  { path: '/catalog/partners/:partnerId', element: CatalogPartnerDetailPage, isDynamic: true },
  { path: '/settings/autonumbering', element: AutonumberingPage },
  { path: '/settings/fx-rates', element: FxRatesPage },
];

// ---------------------------------------------------------------------------
// Workspace panels
// ---------------------------------------------------------------------------
const panels: WorkspacePanelConfig[] = [
  // Page panels — wrapped full-page components
  { id: 'accounts', title: 'Accounts', type: 'internal', category: 'page', componentKey: 'accounts-list', route: '/accounts' },
  { id: 'orders-bill-payments-new', title: 'Create Bill Payment', type: 'internal', category: 'page', componentKey: 'bill-payment-form', route: '/orders/bill-payments/new' },
  { id: 'orders-activity', title: 'Order Activity', type: 'internal', category: 'page', componentKey: 'orders-list', route: '/orders/activity' },
  { id: 'billing-invoices', title: 'Invoices', type: 'internal', category: 'page', componentKey: 'invoices-list', route: '/billing/invoices' },
  { id: 'billing-invoices-new', title: 'Create Invoice', type: 'internal', category: 'page', componentKey: 'invoice-form', route: '/billing/invoices/new' },
  { id: 'catalog', title: 'Catalog', type: 'internal', category: 'page', componentKey: 'catalog-landing', route: '/catalog' },
  { id: 'catalog-countries', title: 'Countries', type: 'internal', category: 'page', componentKey: 'catalog-countries', route: '/catalog/countries' },
  { id: 'catalog-categories', title: 'Categories', type: 'internal', category: 'page', componentKey: 'catalog-categories', route: '/catalog/categories' },
  { id: 'catalog-billers', title: 'Billers', type: 'internal', category: 'page', componentKey: 'catalog-billers', route: '/catalog/billers' },
  { id: 'catalog-partners', title: 'Partners', type: 'internal', category: 'page', componentKey: 'catalog-partners', route: '/catalog/partners' },
  { id: 'settings-autonumbering', title: 'Autonumbering', type: 'internal', category: 'page', componentKey: 'autonumbering', route: '/settings/autonumbering' },
  { id: 'settings-fx-rates', title: 'FX Rates', type: 'internal', category: 'page', componentKey: 'fx-rates', route: '/settings/fx-rates' },
  // Micro-app panels — workspace-native, cross-panel communication
  { id: 'invoice-manager', title: 'Invoice Manager', description: 'Create, manage, and track invoices with AI-assisted insights.', type: 'internal', category: 'micro-app', componentKey: 'invoice-manager', appCardId: '1', defaultWidth: 520 },
  { id: 'reconciliation-hub', title: 'Reconciliation Hub', description: 'AI-powered matching and discrepancy detection.', type: 'internal', category: 'micro-app', componentKey: 'reconciliation-hub', appCardId: '2', defaultWidth: 520 },
];

const panelComponents = {
  'accounts-list': wrapPage(AccountsListPage),
  'orders-list': wrapPage(OrdersListPage),
  'bill-payment-form': wrapPage(BillPaymentOrderFormPage),
  'catalog-landing': wrapPage(CatalogLandingPage),
  'catalog-countries': wrapPage(CatalogCountriesPage),
  'catalog-categories': wrapPage(CatalogCategoriesPage),
  'catalog-billers': wrapPage(CatalogBillersPage),
  'catalog-partners': wrapPage(CatalogPartnersPage),
  autonumbering: wrapPage(AutonumberingPage),
  'fx-rates': wrapPage(FxRatesPage),
  'invoices-list': wrapPage(InvoicesListPage),
  'invoice-form': wrapPage(InvoiceFormPage),
  'invoice-manager': InvoiceManagerPanel,
  'reconciliation-hub': ReconciliationHubPanel,
};

// ---------------------------------------------------------------------------
// Workspace templates
// ---------------------------------------------------------------------------
const workspaceTemplates: WorkspaceTemplate[] = [
  {
    id: 'billing-ops',
    name: 'Billing Ops',
    description: 'Invoice management with AI-powered reconciliation.',
    icon: 'Receipt',
    panels: ['invoice-manager', 'reconciliation-hub'],
    layout: 'split-horizontal',
  },
];

// ---------------------------------------------------------------------------
// Breadcrumbs
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/accounts', trail: ['Accounts'] },
  { pathPrefix: '/orders/bill-payments', trail: [{ label: 'Orders', href: '/orders' }, 'Bill Payments'] },
  { pathPrefix: '/orders', trail: ['Orders'] },
  { pathPrefix: '/ledger', trail: ['Accounting'] },
  { pathPrefix: '/catalog', trail: ['Catalog'] },
  { pathPrefix: '/billing/invoices', trail: [{ label: 'Billing', href: '/billing' }, 'Invoices'] },
];

// ---------------------------------------------------------------------------
// Module export
// ---------------------------------------------------------------------------
export const financeModule: AdminModule = {
  id: 'finance',
  name: 'Finance',
  requires: ['finance'],
  navigation,
  routes,
  panels,
  panelComponents,
  defaultWorkspacePanels: ['invoice-manager', 'reconciliation-hub'],
  workspaceTemplates,
  breadcrumbs,
};
