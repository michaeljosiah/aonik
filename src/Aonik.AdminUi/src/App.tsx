import { useEffect, useRef, useState } from 'react';
import { BrowserRouter, Routes, Route, useLocation } from 'react-router-dom';
import { Toaster } from 'sonner';
import { Sidebar, Header } from '@/components/layout';
import type { AiAgentSelectorItem } from '@/components/ai/AiAgentSelector';
import { AiAgentSelector } from '@/components/ai/AiAgentSelector';
import {
  MySpacePage,
  AnalyticsPage,
  LoginPage,
  SetupWizardPage,
  SetupJourneyPage,
  SetupGuidePage,
  SetupGuidesLandingPage,
  TenantSetupWizardPage,
  AiChatMock,
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
  UserDetailPage,
  BillPaymentOrderFormPage,
  OrdersLandingPage,
  ContentBlocksListPage,
  ContentBlockEditPage,
  MediaLibraryPage,
  AutonumberingPage,
  SystemToolsPage,
  FxRatesPage,
  CustomersListPage,
  CustomerDetailPage,
  WorkspacePage,
} from '@/pages';
import { AuthProvider, useAuth } from '@/auth';
import { ThemeProvider } from '@/contexts';
import { ProtectedRoute } from '@/components/ProtectedRoute';
import { setAccessTokenGetter } from '@/lib/api';
import { bootstrapService } from '@/services/bootstrapService';
import { tenantService } from '@/services/tenantService';
import { identityService } from '@/services/identityService';
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
  const location = useLocation();
  const isAiChat = location.pathname.startsWith('/ai/chat');
  const isWorkspace = location.pathname.startsWith('/workspace');
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const previousSidebarCollapsed = useRef<boolean | null>(null);
  const preFullscreenSidebarState = useRef<boolean | null>(null);

  const agents = useRef<AiAgentSelectorItem[]>([
    {
      id: 'a-personal',
      title: 'Agent name',
      description: 'Short description',
      group: 'personal',
      icon: 'fox',
    },
    {
      id: 'a-centrali',
      title: 'Centrali Ai',
      description: 'Short description',
      group: 'agents',
      icon: 'centrali',
    },
    {
      id: 'a-2',
      title: 'Agent name',
      description: 'Short description',
      group: 'agents',
      icon: 'fox',
    },
    {
      id: 'a-3',
      title: 'Agent name',
      description: 'Short description',
      group: 'agents',
      icon: 'fox',
    },
    {
      id: 'a-4',
      title: 'Agent name',
      description: 'Short description',
      group: 'agents',
      icon: 'fox',
    },
  ]);

  const [selectedAgentId, setSelectedAgentId] = useState('a-centrali');

  // Auto-collapse main nav on AI chat page.
  useEffect(() => {
    if (isAiChat) {
      if (previousSidebarCollapsed.current === null) {
        previousSidebarCollapsed.current = sidebarCollapsed;
      }
      setSidebarCollapsed(true);
      return;
    }

    if (previousSidebarCollapsed.current !== null) {
      setSidebarCollapsed(previousSidebarCollapsed.current);
      previousSidebarCollapsed.current = null;
    }
  }, [isAiChat, sidebarCollapsed]);

  // Handle fullscreen state changes - auto-collapse sidebar for maximum screen real estate
  const handleFullscreenChange = (isFullscreen: boolean) => {
    if (isFullscreen) {
      // Save current sidebar state before collapsing
      preFullscreenSidebarState.current = sidebarCollapsed;
      setSidebarCollapsed(true);
    } else {
      // Restore previous sidebar state when exiting fullscreen
      if (preFullscreenSidebarState.current !== null) {
        setSidebarCollapsed(preFullscreenSidebarState.current);
        preFullscreenSidebarState.current = null;
      }
    }
  };

  // Determine breadcrumb based on current route (simplified)
  const getBreadcrumb = () => {
    const path = window.location.pathname;
    if (path === '/') return ['Dashboard'];
    if (path.startsWith('/analytics')) return ['Analytics'];
    if (path.startsWith('/customers')) return ['Customers'];
    if (path.startsWith('/billing')) return ['Billing'];
    if (path.startsWith('/orders/bill-payments')) return ['Orders', 'Bill Payments'];
    if (path.startsWith('/payments')) return ['Payments'];
    if (path.startsWith('/orders')) return ['Orders'];
    if (path.startsWith('/ledger')) return ['Ledger'];
    if (path.startsWith('/ai')) return ['AI & Agents'];
    if (path.startsWith('/workspace')) return ['Workspace'];
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
        <Header
          breadcrumb={getBreadcrumb()}
          isWorkspace={isWorkspace}
          leftSlot={
            isAiChat ? (
              <AiAgentSelector
                agents={agents.current}
                selectedAgentId={selectedAgentId}
                onSelectAgent={setSelectedAgentId}
              />
            ) : undefined
          }
          onWorkspaceReset={
            isWorkspace
              ? () => window.dispatchEvent(new CustomEvent('aonik:workspace:reset'))
              : undefined
          }
          onFullscreenChange={handleFullscreenChange}
        />
        <main className={isAiChat || isWorkspace ? 'flex-1 overflow-hidden' : 'flex-1 overflow-auto bg-[var(--color-surface-inset)]'}>
          <Routes>
            <Route path="/" element={<DashboardHome />} />
            <Route path="/search" element={<PlaceholderPage title="Search" />} />
            <Route path="/analytics" element={<AnalyticsPage />} />
            <Route path="/workspace" element={<WorkspacePage />} />
            {/* Customers */}
            <Route path="/customers" element={<CustomersListPage />} />
            <Route path="/customers/:partyId" element={<CustomerDetailPage />} />
            {/* Billing */}
            <Route path="/billing/invoices" element={<PlaceholderPage title="Invoices" />} />
            <Route path="/billing/invoices/new" element={<PlaceholderPage title="Create Invoice" />} />
            <Route path="/billing/dunning" element={<PlaceholderPage title="Dunning Plans" />} />
            {/* Payments */}
            <Route path="/payments/transactions" element={<PlaceholderPage title="Transactions" />} />
            <Route path="/payments/refunds" element={<PlaceholderPage title="Refunds" />} />
            <Route path="/payments/chargebacks" element={<PlaceholderPage title="Chargebacks" />} />
            <Route path="/payments/payouts" element={<PlaceholderPage title="Payouts" />} />
            {/* Orders */}
            <Route path="/orders" element={<OrdersLandingPage />} />
            <Route path="/orders/bill-payments/new" element={<BillPaymentOrderFormPage />} />
            <Route path="/orders/bill-payments/:orderId" element={<BillPaymentOrderFormPage />} />
            <Route path="/orders/activity" element={<PlaceholderPage title="Order Activity" />} />
            {/* Ledger */}
            <Route path="/ledger/accounts" element={<PlaceholderPage title="Accounts" />} />
            <Route path="/ledger/journal-entries" element={<PlaceholderPage title="Journal Entries" />} />
            <Route path="/ledger/reconciliation" element={<PlaceholderPage title="Reconciliation" />} />
            {/* AI & Agents */}
            <Route path="/ai/agents" element={<PlaceholderPage title="Agents" />} />
            <Route path="/ai/models" element={<PlaceholderPage title="AI Models" />} />
            <Route path="/ai/orchestrator" element={<PlaceholderPage title="Orchestrator" />} />
            <Route path="/ai/chat" element={<AiChatMock agentId={selectedAgentId} />} />
            {/* Users & Access */}
            <Route path="/access/users" element={<AccessUsersPage />} />
            <Route path="/access/users/:userId" element={<UserDetailPage />} />
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
            <Route path="/settings/autonumbering" element={<AutonumberingPage />} />
            <Route path="/settings/fx-rates" element={<FxRatesPage />} />
            <Route path="/settings/system-tools" element={<SystemToolsPage />} />
            
            {/* CMS */}
            <Route path="/cms/content-blocks" element={<ContentBlocksListPage />} />
            <Route path="/cms/content-blocks/new" element={<ContentBlockEditPage />} />
            <Route path="/cms/content-blocks/:id" element={<ContentBlockEditPage />} />
            <Route path="/cms/media" element={<MediaLibraryPage />} />
            
            <Route path="/setup/journey" element={<SetupJourneyPage />} />
            <Route path="/setup/tenant" element={<TenantSetupWizardPage />} />
            <Route path="/setup-guides" element={<SetupGuidesLandingPage />} />
            <Route path="/setup-guides/:slug" element={<SetupGuidePage />} />
            {/* Fallback */}
            <Route path="*" element={<PlaceholderPage title="Page Not Found" />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}

function DashboardHome() {
  const [setupState, setSetupState] = useState<'loading' | 'ready' | 'journey'>(
    'loading'
  );

  useEffect(() => {
    const checkTenantSetup = async () => {
      try {
        // Check if user has skipped or completed the old onboarding flow
        const skip = localStorage.getItem('aonik:onboarding:skip');
        const complete = localStorage.getItem('aonik:onboarding:complete');
        
        // Tenant wizard setup is now handled at route level in AuthenticatedApp
        // Only check if they want to see the journey
        if (!skip && !complete) {
          setSetupState('journey');
          return;
        }
        
        setSetupState('ready');
      } catch (err) {
        // If we can't check, default to showing the journey (old behavior)
        const skip = localStorage.getItem('aonik:onboarding:skip');
        const complete = localStorage.getItem('aonik:onboarding:complete');
        setSetupState(!skip && !complete ? 'journey' : 'ready');
      }
    };

    checkTenantSetup();
  }, []);

  if (setupState === 'loading') {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="w-8 h-8 border-4 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  if (setupState === 'journey') {
    return (
      <SetupJourneyPage
        onSkip={() => setSetupState('ready')}
        onComplete={() => setSetupState('ready')}
      />
    );
  }

  return <MySpacePage />;
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
  const [tenantNeedsSetup, setTenantNeedsSetup] = useState<boolean | null>(null);
  const { isLoading: authLoading, isAuthenticated, accessToken, getAccessToken } = useAuth();

  useEffect(() => {
    const checkSetup = async () => {
      // Wait for auth to complete before checking
      if (authLoading) return;

      // Ensure API layer has the latest token getter before any calls
      setAccessTokenGetter(getAccessToken);

      try {
        const status = await bootstrapService.status();
        setNeedsSetup(status.tenantCount === 0);
        
        // Only check tenant setup if user is authenticated
        if (status.tenantCount > 0 && isAuthenticated) {
          try {
            const selectedTenant = getSelectedTenant();
            if (!selectedTenant?.tenantId) {
              setTenantNeedsSetup(false);
              return;
            }
            if (!accessToken) {
              const token = await getAccessToken();
              if (!token) {
                return;
              }
            }
            const currentUser = await identityService.getCurrentUser();
            const tenant = await tenantService.get(currentUser.tenantId);
            setTenantNeedsSetup(!tenant.isSetupComplete);
          } catch {
            setTenantNeedsSetup(false);
          }
        } else {
          setTenantNeedsSetup(false);
        }
      } catch {
        setNeedsSetup(false);
        setTenantNeedsSetup(false);
      }
    };

    checkSetup();
  }, [authLoading, isAuthenticated, accessToken, getAccessToken]);

  if (needsSetup === null || tenantNeedsSetup === null) {
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
          <Route path="/setup-guides" element={<AppLayout />} />
          <Route path="/setup-guides/:slug" element={<AppLayout />} />
          <Route path="/*" element={<SetupWizardPage />} />
        </Routes>
      </>
    );
  }

  return (
    <>
      <ApiAuthSetup />
      <TenantContextSetup />
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/setup" element={<SetupWizardPage />} />
        <Route 
          path="/setup/tenant" 
          element={
            <ProtectedRoute>
              <TenantSetupWizardPage onComplete={() => window.location.href = '/'} />
            </ProtectedRoute>
          } 
        />
        {tenantNeedsSetup ? (
          <Route 
            path="/*" 
            element={
              <ProtectedRoute>
                <TenantSetupWizardPage onComplete={() => window.location.href = '/'} />
              </ProtectedRoute>
            } 
          />
        ) : (
          <Route
            path="/*"
            element={
              <ProtectedRoute>
                <AppLayout />
              </ProtectedRoute>
            }
          />
        )}
      </Routes>
    </>
  );
}

function App() {
  return (
    <BrowserRouter>
      <ThemeProvider>
        <AuthProvider>
          <div className="flex-1 min-w-0">
            <AuthenticatedApp />
            <Toaster richColors position="top-right" />
          </div>
        </AuthProvider>
      </ThemeProvider>
    </BrowserRouter>
  );
}

export default App;
