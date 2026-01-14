import { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { Sidebar, Header } from '@/components/layout';
import { MySpacePage, LoginPage } from '@/pages';
import { AuthProvider, useAuth } from '@/auth';
import { ProtectedRoute } from '@/components/ProtectedRoute';
import { setAccessTokenGetter } from '@/lib/api';

// Component to set up API authentication
function ApiAuthSetup() {
  const { getAccessToken } = useAuth();

  useEffect(() => {
    setAccessTokenGetter(getAccessToken);
  }, [getAccessToken]);

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
        <main className="flex-1 overflow-auto">
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
            <Route path="/access/users" element={<PlaceholderPage title="Users" />} />
            <Route path="/access/roles" element={<PlaceholderPage title="Roles" />} />
            <Route path="/access/permissions" element={<PlaceholderPage title="Permissions" />} />
            {/* Tenants */}
            <Route path="/tenants" element={<PlaceholderPage title="Tenants" />} />
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
  return (
    <>
      <ApiAuthSetup />
      <Routes>
        <Route path="/login" element={<LoginPage />} />
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
      <AuthProvider>
        <AuthenticatedApp />
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
