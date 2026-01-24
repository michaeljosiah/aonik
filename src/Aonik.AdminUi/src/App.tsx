import { useEffect, useState } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { Sidebar, Header } from '@/components/layout';
import {
  MySpacePage,
  LoginPage,
  SetupWizardPage,
  TenantsListPage,
  CreateTenantPage,
  TenantDetailPage,
  CatalogLandingPage,
  CatalogCountriesPage,
  CatalogCategoriesPage,
  CatalogBillersPage,
  CatalogBillerDetailPage,
  CatalogBillerServicesPage,
  CatalogBillerServiceDetailPage,
  AccessUsersPage,
  AccessRolesPage,
  AccessPermissionsPage,
} from '@/pages';
import { AuthProvider, useAuth } from '@/auth';
import { ThemeProvider } from '@/contexts';
import { ProtectedRoute } from '@/components/ProtectedRoute';
import { setAccessTokenGetter } from '@/lib/api';
import { SetupRedirect } from '@/components/SetupRedirect';
import { bootstrapService } from '@/services/bootstrapService';
import { tenantService } from '@/services/tenantService';
import { getSelectedTenant, setSelectedTenant } from '@/lib/tenantContext';

// Component to set up API authentication
function ApiAuthSetup() {
  const { getAccessToken } = useAuth();

  useEffect(() => {
    setAccessTokenGetter(getAccessToken);
  }, [getAccessToken]);

  return null;
}

function TenantContextSetup() {
  const { isLoading: authLoading } = useAuth();

  useEffect(() => {
    const ensureTenantSelected = async () => {
      if (authLoading) return;

      // If a tenant is already selected (login dropdown or earlier session), nothing to do.
      if (getSelectedTenant()?.tenantId) return;

      // If running on a tenant subdomain, try to resolve it via the public host endpoint.
      const hostname = window.location.hostname;
      const parts = hostname.split('.');
      const looksTenantScoped = parts.length >= 3 && !hostname.startsWith('www.') && !hostname.includes('localhost');
      if (!looksTenantScoped) return;

      const subdomain = parts[0];
      try {
        const response = await tenantService.listForLogin();
        const match = response.tenants.find(t => (t.subdomain ?? '').toLowerCase() === subdomain.toLowerCase());
        if (match) {
          setSelectedTenant({
            tenantId: match.tenantId,
            name: match.name,
            subdomain: match.subdomain,
            environment: match.environment,
          });
        }
      } catch {
        // If we can't resolve, leave unset and let API return a clear error.
      }
    };

    ensureTenantSelected();
  }, [authLoading]);

  return null;
}

function AppLayout() {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  // Determine breadcrumb based on current route (simplified)
  const getBreadcrumb = () => {
    const path = window.location.pathname;
    if (path === '/') return ['Dashboard'];
    if (path.startsWith('/billing')) return ['Billing'];
    if (path.startsWith('/payments')) return ['Payments'];
    if (path.startsWith('/ledger')) return ['Ledger'];
    if (path.startsWith('/ai')) return ['AI & Agents'];
    if (path.startsWith('/access')) return ['Users & Access'];
    if (path.startsWith('/catalog')) return ['Catalog'];
    if (path.startsWith('/tenants')) return ['Tenants'];
    if (path.startsWith('/settings')) return ['Settings'];
    return ['Dashboard'];
  };

  return (
    <div className="flex min-h-screen bg-[var(--color-background)]">
      <Sidebar
        collapsed={sidebarCollapsed}
        onToggle={() => setSidebarCollapsed(!sidebarCollapsed)}
      />
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        <Header breadcrumb={getBreadcrumb()} />
        <main className="flex-1 overflow-auto bg-[var(--color-surface-inset)]">
          <Routes>
            <Route path="/" element={<MySpacePage />} />
            <Route path="/search" element={<PlaceholderPage title="Search" />} />
            {/* Billing */}
            <Route path="/billing/invoices" element={<PlaceholderPage title="Invoices" />} />
            <Route path="/billing/invoices/new" element={<PlaceholderPage title="Create Invoice" />} />
            <Route path="/billing/customers" element={<PlaceholderPage title="Customers" />} />
            <Route path="/billing/dunning" element={<PlaceholderPage title="Dunning Plans" />} />
            {/* Payments */}
            <Route path="/payments/transactions" element={<PlaceholderPage title="Transactions" />} />
            <Route path="/payments/refunds" element={<PlaceholderPage title="Refunds" />} />
            <Route path="/payments/chargebacks" element={<PlaceholderPage title="Chargebacks" />} />
            <Route path="/payments/payouts" element={<PlaceholderPage title="Payouts" />} />
            {/* Ledger */}
            <Route path="/ledger/accounts" element={<PlaceholderPage title="Accounts" />} />
            <Route path="/ledger/journal-entries" element={<PlaceholderPage title="Journal Entries" />} />
            <Route path="/ledger/reconciliation" element={<PlaceholderPage title="Reconciliation" />} />
            {/* AI & Agents */}
            <Route path="/ai/agents" element={<PlaceholderPage title="Agents" />} />
            <Route path="/ai/models" element={<PlaceholderPage title="AI Models" />} />
            <Route path="/ai/orchestrator" element={<PlaceholderPage title="Orchestrator" />} />
            <Route path="/ai/chat" element={<PlaceholderPage title="AI Assistant" />} />
            {/* Users & Access */}
            <Route path="/access/users" element={<AccessUsersPage />} />
            <Route path="/access/roles" element={<AccessRolesPage />} />
            <Route path="/access/permissions" element={<AccessPermissionsPage />} />
            {/* Catalog */}
            <Route path="/catalog" element={<CatalogLandingPage />} />
            <Route path="/catalog/countries" element={<CatalogCountriesPage />} />
            <Route path="/catalog/categories" element={<CatalogCategoriesPage />} />
            <Route path="/catalog/billers" element={<CatalogBillersPage />} />
            <Route path="/catalog/billers/:billerId" element={<CatalogBillerDetailPage />} />
            <Route path="/catalog/billers/:billerId/services" element={<CatalogBillerServicesPage />} />
            <Route path="/catalog/billers/:billerId/services/:serviceId" element={<CatalogBillerServiceDetailPage />} />
            {/* Tenants */}
            <Route path="/tenants" element={<TenantsListPage />} />
            <Route path="/tenants/new" element={<CreateTenantPage />} />
            <Route path="/tenants/:id" element={<TenantDetailPage />} />
            {/* Settings */}
            <Route path="/settings/general" element={<PlaceholderPage title="General Settings" />} />
            <Route path="/settings/api-keys" element={<PlaceholderPage title="API Keys" />} />
            <Route path="/settings/webhooks" element={<PlaceholderPage title="Webhooks" />} />
            <Route path="/settings/audit-logs" element={<PlaceholderPage title="Audit Logs" />} />
            {/* Fallback */}
            <Route path="*" element={<PlaceholderPage title="Page Not Found" />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}

function PlaceholderPage({ title }: { title: string }) {
  return (
    <div className="flex items-center justify-center h-full">
      <div className="text-center">
        <h1 className="text-2xl font-bold text-[var(--color-text-primary)] mb-2">{title}</h1>
        <p className="text-[var(--color-text-secondary)]">This page is under construction.</p>
      </div>
    </div>
  );
}

function AuthenticatedApp() {
  const [needsSetup, setNeedsSetup] = useState<boolean | null>(null);

  useEffect(() => {
    const checkSetup = async () => {
      try {
        const status = await bootstrapService.status();
        setNeedsSetup(status.tenantCount === 0);
      } catch {
        setNeedsSetup(false);
      }
    };

    checkSetup();
  }, []);

  if (needsSetup === null) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-[var(--color-background)]">
        <div className="flex flex-col items-center gap-4">
          <div className="w-8 h-8 border-4 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
          <p className="text-sm text-[var(--color-text-secondary)]">Loading...</p>
        </div>
      </div>
    );
  }

  if (needsSetup) {
    return (
      <>
        <ApiAuthSetup />
        <TenantContextSetup />
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/*" element={<SetupWizardPage />} />
        </Routes>
      </>
    );
  }

  return (
    <>
      <ApiAuthSetup />
      <TenantContextSetup />
      <SetupRedirect />
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/setup" element={<SetupWizardPage />} />
        <Route
          path="/*"
          element={
            <ProtectedRoute>
              <AppLayout />
            </ProtectedRoute>
          }
        />
      </Routes>
    </>
  );
}

function App() {
  return (
    <BrowserRouter>
      <ThemeProvider>
        <AuthProvider>
          <AuthenticatedApp />
        </AuthProvider>
      </ThemeProvider>
    </BrowserRouter>
  );
}

export default App;
