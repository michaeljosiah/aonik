import { AnalyticsPanel } from './apps/AnalyticsPanel';
import { CashFlowForecasterPanel } from './apps/CashFlowForecasterPanel';
import { FraudDetectionPanel } from './apps/FraudDetectionPanel';
import { InvoiceManagerPanel } from './apps/InvoiceManagerPanel';
import { PlaceholderPanel } from './apps/PlaceholderPanel';
import { ReconciliationHubPanel } from './apps/ReconciliationHubPanel';
import { AccessPermissionsPage, AccessRolesPage, AccessUsersPage, AutonumberingPage, BillPaymentOrderFormPage, CatalogBillersPage, CatalogCategoriesPage, CatalogCountriesPage, CatalogLandingPage, ContentBlocksListPage, CustomersListPage, FxRatesPage, MediaLibraryPage, OrdersLandingPage, TenantsListPage } from '@/pages';
import { createElement } from 'react';
import type { ComponentType } from 'react';
import type { WorkspacePanelRenderProps } from './types';

const wrapPage = (Component: ComponentType<Record<string, never>>) => {
  return function WrappedPage(_: WorkspacePanelRenderProps) {
    return createElement(Component);
  };
};

export const workspacePanelComponents: Record<string, ComponentType<WorkspacePanelRenderProps>> = {
  analytics: AnalyticsPanel,
  placeholder: PlaceholderPanel,
  'customers-list': wrapPage(CustomersListPage),
  'orders-landing': wrapPage(OrdersLandingPage),
  'bill-payment-form': wrapPage(BillPaymentOrderFormPage),
  'access-users': wrapPage(AccessUsersPage),
  'access-roles': wrapPage(AccessRolesPage),
  'access-permissions': wrapPage(AccessPermissionsPage),
  'catalog-landing': wrapPage(CatalogLandingPage),
  'catalog-countries': wrapPage(CatalogCountriesPage),
  'catalog-categories': wrapPage(CatalogCategoriesPage),
  'catalog-billers': wrapPage(CatalogBillersPage),
  tenants: wrapPage(TenantsListPage),
  autonumbering: wrapPage(AutonumberingPage),
  'fx-rates': wrapPage(FxRatesPage),
  'content-blocks': wrapPage(ContentBlocksListPage),
  'media-library': wrapPage(MediaLibraryPage),
  'invoice-manager': InvoiceManagerPanel,
  'reconciliation-hub': ReconciliationHubPanel,
  'cash-flow-forecaster': CashFlowForecasterPanel,
  'fraud-detection': FraudDetectionPanel,
};
