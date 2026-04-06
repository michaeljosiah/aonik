import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig } from '@/workspace/types';
import {
  CustomersListPage,
  CustomerDetailPage,
} from '@/pages/customers';
import {
  AccountsListPage,
  AccountConnectionDetailPage,
  AccountTransactionsPage,
} from '@/pages/accounts';
import {
  OrdersLandingPage,
  OrdersListPage,
  BillPaymentOrderFormPage,
} from '@/pages/orders';
import {
  LedgerOverviewPage,
  LedgerAccountsPage,
  LedgerJournalEntriesPage,
} from '@/pages/ledger';
import {
  ComplianceLandingPage,
  DocumentsListPage,
  DocumentDetailPage,
  DocumentCreatePage,
} from '@/pages/compliance';
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
import { CashFlowForecasterPanel } from '@/workspace/apps/CashFlowForecasterPanel';
import { FraudDetectionPanel } from '@/workspace/apps/FraudDetectionPanel';
import { wrapPage } from '../utils';

// ---------------------------------------------------------------------------
// Navigation
// ---------------------------------------------------------------------------
const navigation: NavigationSection[] = [
  {
    id: 'finance',
    items: [
      {
        id: 'party-profiles',
        label: 'Customers',
        icon: 'Building2',
        href: '/customers',
      },
      {
        id: 'accounts',
        label: 'Accounts',
        icon: 'Landmark',
        href: '/accounts',
      },
      {
        id: 'orders',
        label: 'Orders',
        icon: 'ClipboardList',
        href: '/orders/activity',
      },
      {
        id: 'billing-invoices',
        label: 'Invoices',
        icon: 'FileText',
        href: '/billing/invoices',
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
              { id: 'ledger-accounts', label: 'Accounts', icon: 'Landmark', href: '/ledger/accounts' },
              { id: 'ledger-journal-entries', label: 'Transactions', icon: 'ClipboardList', href: '/ledger/journal-entries' },
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
      {
        id: 'compliance-documents',
        label: 'Documents',
        icon: 'FileText',
        href: '/compliance/documents',
      },
    ],
  },
];

// ---------------------------------------------------------------------------
// Routes
// ---------------------------------------------------------------------------
const routes = [
  { path: '/customers', element: CustomersListPage },
  { path: '/customers/:partyId', element: CustomerDetailPage, isDynamic: true },
  { path: '/accounts', element: AccountsListPage },
  { path: '/accounts/:accountId/transactions', element: AccountTransactionsPage, isDynamic: true },
  { path: '/accounts/connections/:connectionId', element: AccountConnectionDetailPage, isDynamic: true },
  { path: '/orders', element: OrdersLandingPage },
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
  { path: '/compliance', element: ComplianceLandingPage },
  { path: '/compliance/documents', element: DocumentsListPage },
  { path: '/compliance/documents/new', element: DocumentCreatePage },
  { path: '/compliance/documents/:documentId', element: DocumentDetailPage, isDynamic: true },
  { path: '/settings/autonumbering', element: AutonumberingPage },
  { path: '/settings/fx-rates', element: FxRatesPage },
];

// ---------------------------------------------------------------------------
// Workspace panels
// ---------------------------------------------------------------------------
const panels: WorkspacePanelConfig[] = [
  { id: 'customers', title: 'Customers', type: 'internal', componentKey: 'customers-list', route: '/customers' },
  { id: 'accounts', title: 'Accounts', type: 'internal', componentKey: 'accounts-list', route: '/accounts' },
  { id: 'orders', title: 'Orders', type: 'internal', componentKey: 'orders-landing', route: '/orders' },
  { id: 'orders-bill-payments-new', title: 'Create Bill Payment', type: 'internal', componentKey: 'bill-payment-form', route: '/orders/bill-payments/new' },
  { id: 'orders-activity', title: 'Order Activity', type: 'internal', componentKey: 'orders-list', route: '/orders/activity' },
  { id: 'billing', title: 'Billing', type: 'internal', componentKey: 'placeholder', route: '/billing' },
  { id: 'billing-invoices', title: 'Invoices', type: 'internal', componentKey: 'invoices-list', route: '/billing/invoices' },
  { id: 'billing-invoices-new', title: 'Create Invoice', type: 'internal', componentKey: 'invoice-form', route: '/billing/invoices/new' },
  { id: 'billing-dunning', title: 'Dunning Plans', type: 'internal', componentKey: 'placeholder', route: '/billing/dunning' },
  { id: 'payments', title: 'Payments', type: 'internal', componentKey: 'placeholder', route: '/payments' },
  { id: 'payments-transactions', title: 'Transactions', type: 'internal', componentKey: 'placeholder', route: '/payments/transactions' },
  { id: 'payments-payouts', title: 'Payouts', type: 'internal', componentKey: 'placeholder', route: '/payments/payouts' },
  { id: 'payments-refunds', title: 'Refunds', type: 'internal', componentKey: 'placeholder', route: '/payments/refunds' },
  { id: 'payments-chargebacks', title: 'Chargebacks', type: 'internal', componentKey: 'placeholder', route: '/payments/chargebacks' },
  { id: 'ledger', title: 'Ledger', type: 'internal', componentKey: 'placeholder', route: '/ledger' },
  { id: 'ledger-accounts', title: 'Accounts', type: 'internal', componentKey: 'placeholder', route: '/ledger/accounts' },
  { id: 'ledger-journal-entries', title: 'Journal Entries', type: 'internal', componentKey: 'placeholder', route: '/ledger/journal-entries' },
  { id: 'ledger-reconciliation', title: 'Reconciliation', type: 'internal', componentKey: 'placeholder', route: '/ledger/reconciliation' },
  { id: 'catalog', title: 'Catalog', type: 'internal', componentKey: 'catalog-landing', route: '/catalog' },
  { id: 'catalog-countries', title: 'Countries', type: 'internal', componentKey: 'catalog-countries', route: '/catalog/countries' },
  { id: 'catalog-categories', title: 'Categories', type: 'internal', componentKey: 'catalog-categories', route: '/catalog/categories' },
  { id: 'catalog-billers', title: 'Billers', type: 'internal', componentKey: 'catalog-billers', route: '/catalog/billers' },
  { id: 'catalog-partners', title: 'Partners', type: 'internal', componentKey: 'catalog-partners', route: '/catalog/partners' },
  { id: 'settings-autonumbering', title: 'Autonumbering', type: 'internal', componentKey: 'autonumbering', route: '/settings/autonumbering' },
  { id: 'settings-fx-rates', title: 'FX Rates', type: 'internal', componentKey: 'fx-rates', route: '/settings/fx-rates' },
  // App-card panels (opened from MySpace dashboard)
  { id: 'invoice-manager', title: 'Invoice Manager', description: 'Create, manage, and track invoices with AI-assisted insights.', type: 'internal', componentKey: 'invoice-manager', appCardId: '1', defaultWidth: 520 },
  { id: 'reconciliation-hub', title: 'Reconciliation Hub', description: 'AI-powered matching and discrepancy detection.', type: 'internal', componentKey: 'reconciliation-hub', appCardId: '2', defaultWidth: 520 },
  { id: 'cash-flow-forecaster', title: 'Cash Flow Forecaster', description: 'Predict cash positions and run scenario planning.', type: 'internal', componentKey: 'cash-flow-forecaster', appCardId: '3', defaultWidth: 520 },
  { id: 'fraud-detection', title: 'Fraud Detection', description: 'Real-time anomaly detection and explainable alerts.', type: 'internal', componentKey: 'fraud-detection', appCardId: '4', defaultWidth: 520 },
];

const panelComponents = {
  'customers-list': wrapPage(CustomersListPage),
  'accounts-list': wrapPage(AccountsListPage),
  'orders-landing': wrapPage(OrdersLandingPage),
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
  'cash-flow-forecaster': CashFlowForecasterPanel,
  'fraud-detection': FraudDetectionPanel,
};

// ---------------------------------------------------------------------------
// Breadcrumbs
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/customers', trail: ['Customers'] },
  { pathPrefix: '/accounts', trail: ['Accounts'] },
  { pathPrefix: '/orders/bill-payments', trail: ['Orders', 'Bill Payments'] },
  { pathPrefix: '/orders', trail: ['Orders'] },
  { pathPrefix: '/ledger', trail: ['Accounting'] },
  { pathPrefix: '/catalog', trail: ['Catalog'] },
  { pathPrefix: '/compliance', trail: ['Documents'] },
  { pathPrefix: '/billing/invoices', trail: ['Billing', 'Invoices'] },
  { pathPrefix: '/billing', trail: ['Billing'] },
  { pathPrefix: '/payments', trail: ['Payments'] },
];

// ---------------------------------------------------------------------------
// Module export
// ---------------------------------------------------------------------------
export const financeModule: AdminModule = {
  id: 'finance',
  name: 'Finance',
  navigation,
  routes,
  panels,
  panelComponents,
  defaultWorkspacePanels: ['invoice-manager', 'reconciliation-hub'],
  breadcrumbs,
};
