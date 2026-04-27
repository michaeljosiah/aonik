import { useEffect, useRef, useState, createElement, useCallback } from 'react';
import { BrowserRouter, Navigate, Routes, Route, useLocation, useNavigate, useParams } from 'react-router-dom';
import { Toaster } from 'sonner';
import { AiChatPanel, LoadingScreen } from '@/components/layout';
import { AonikSidebar } from '@/components/layout/aonik/AonikSidebar';
import { AonikTopBar } from '@/components/layout/aonik/AonikTopBar';
import type { AiAgentSelectorItem } from '@/components/ai/AiAgentSelector';
import { AiAgentSelector } from '@/components/ai/AiAgentSelector';
import {
  MySpacePage,
  LoginPage,
  SetupWizardPage,
  SetupJourneyPage,
  SetupGuidePage,
  SetupGuidesLandingPage,
  TenantSetupWizardPage,
  AiChatMock,
} from '@/pages';
import { WorkspacePage } from '@/workspace/WorkspacePage';
import { useModules } from '@/modules';
import { AuthProvider, useAuth } from '@/auth';
import { ThemeProvider } from '@/contexts';
import { ProtectedRoute } from '@/components/ProtectedRoute';
import { setAccessTokenGetter } from '@/lib/api';
import { bootstrapService } from '@/services/bootstrapService';
import { tenantService } from '@/services/tenantService';
import { identityService } from '@/services/identityService';
import { agentConfigService } from '@/services/aiService';
import { getSelectedTenant, setSelectedTenant } from '@/lib/tenantContext';
import { isTenantScopedHostname } from '@/lib/tenantRouting';

const orchestratorEntry: AiAgentSelectorItem = {
  id: '',
  title: 'AONIK Orchestrator',
  description: 'Routes to domain agents automatically',
  group: 'personal',
  icon: 'centrali',
};

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
      const looksTenantScoped = isTenantScopedHostname(hostname);
      if (!looksTenantScoped) return;

      const subdomain = hostname.split('.')[0];
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
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const isAiChat = location.pathname.startsWith('/ai/chat');
  const isWorkspace = location.pathname.startsWith('/workspace');
  const [selectedAgentId, setSelectedAgentId] = useState('');
  const routeChatAgentId = isAiChat
    ? decodeURIComponent(location.pathname.replace(/^\/ai\/chat\/?/, ''))
    : '';
  const activeChatAgentId = isAiChat ? routeChatAgentId : selectedAgentId;
  const [sidebarCollapsed, setSidebarCollapsed] = useState(true);
  const [showAiChat, setShowAiChat] = useState(false);
  const previousSidebarCollapsed = useRef<boolean | null>(null);
  const preFullscreenSidebarState = useRef<boolean | null>(null);
  const preChatSidebarState = useRef<boolean | null>(null);

  // Module system: aggregated routes and breadcrumbs
  const { routes, getBreadcrumb } = useModules();

  const [agents, setAgents] = useState<AiAgentSelectorItem[]>([orchestratorEntry]);

  const fetchAgents = useCallback(async () => {
    try {
      const configs = await agentConfigService.list();
      const items: AiAgentSelectorItem[] = configs
        .filter((a) => a.isActive)
        .map((a) => ({
          id: a.name,
          title: a.name
            .replace(/-agent$/, '')
            .replace(/-/g, ' ')
            .replace(/\b\w/g, (c) => c.toUpperCase()),
          description: a.description || a.domain,
          group: a.agentType === 1 ? ('personal' as const) : ('agents' as const),
          icon: a.agentType === 1 ? ('centrali' as const) : ('fox' as const),
        }));

      // Orchestrator always first in its group
      setAgents([orchestratorEntry, ...items]);
    } catch {
      // Keep default entry on error
    }
  }, []);

  useEffect(() => {
    if (isAuthenticated) {
      fetchAgents();
    }
  }, [isAuthenticated, fetchAgents]);

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

  // Toggle AI chat panel — auto-collapse sidebar on screens < 1800px
  const handleAiChatToggle = () => {
    setShowAiChat((prev) => {
      const willShow = !prev;
      if (willShow && window.innerWidth < 1800 && !sidebarCollapsed) {
        preChatSidebarState.current = sidebarCollapsed;
        setSidebarCollapsed(true);
      } else if (!willShow && preChatSidebarState.current !== null) {
        setSidebarCollapsed(preChatSidebarState.current);
        preChatSidebarState.current = null;
      }
      return willShow;
    });
  };

  const handleSelectChatAgent = useCallback((nextAgentId: string) => {
    setSelectedAgentId(nextAgentId);

    if (!isAiChat) {
      return;
    }

    navigate(nextAgentId ? `/ai/chat/${nextAgentId}` : '/ai/chat');
  }, [isAiChat, navigate]);

  return (
    <div className="flex min-h-screen bg-[var(--color-background)]">
      <AonikSidebar
        collapsed={sidebarCollapsed}
        onToggle={() => setSidebarCollapsed(!sidebarCollapsed)}
      />
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        <AonikTopBar
          breadcrumb={getBreadcrumb(window.location.pathname)}
          isWorkspace={isWorkspace}
          onAskAonik={handleAiChatToggle}
          leftSlot={
            isAiChat ? (
              <AiAgentSelector
                agents={agents}
                selectedAgentId={activeChatAgentId}
                onSelectAgent={handleSelectChatAgent}
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
        <div className="flex-1 flex min-h-0 overflow-hidden">
          <main
            className={isAiChat || isWorkspace ? 'flex-1 overflow-hidden min-w-0 transition-[width] duration-400 ease-in-out' : 'flex-1 overflow-auto bg-[var(--color-surface-inset)] min-w-0 transition-[width] duration-400 ease-in-out'}
          >
            <Routes>
              {/* My Space — default authenticated home */}
              <Route path="/" element={<MySpacePage />} />
              {/* Workspace — always present */}
              <Route path="/workspace" element={<WorkspacePage />} />
              <Route path="/observability" element={<LegacyObservabilityRedirect />} />
              {/* AI Chat — wired to AG-UI streaming endpoint */}
              <Route path="/ai/chat" element={<AiChatRoute agentId={activeChatAgentId} agents={agents} onSelectAgent={handleSelectChatAgent} />} />
              <Route path="/ai/chat/:agentId" element={<AiChatRoute agentId={activeChatAgentId} agents={agents} onSelectAgent={handleSelectChatAgent} />} />
              {/* Module-contributed routes */}
              {routes.map((route) => (
                <Route
                  key={route.path}
                  path={route.path}
                  element={createElement(route.element)}
                />
              ))}
              {/* Setup routes */}
              <Route path="/setup/journey" element={<SetupJourneyPage />} />
              <Route path="/setup/tenant" element={<TenantSetupWizardPage />} />
              <Route path="/setup-guides" element={<SetupGuidesLandingPage />} />
              <Route path="/setup-guides/:slug" element={<SetupGuidePage />} />
              {/* Fallback */}
              <Route path="*" element={<PlaceholderPage title="Page Not Found" />} />
            </Routes>
          </main>
          {showAiChat && (
            <AiChatPanel
              onClose={handleAiChatToggle}
              onExpand={() => {
                // Restore sidebar before navigating away
                if (preChatSidebarState.current !== null) {
                  setSidebarCollapsed(preChatSidebarState.current);
                  preChatSidebarState.current = null;
                }
                setShowAiChat(false);
                navigate('/ai/chat');
              }}
            />
          )}
        </div>
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

function LegacyObservabilityRedirect() {
  const location = useLocation();
  return <Navigate replace to={`/admin/observability${location.search}`} />;
}

function AiChatRoute({
  agentId,
  agents,
  onSelectAgent,
}: {
  agentId: string;
  agents: AiAgentSelectorItem[];
  onSelectAgent: (agentId: string) => void;
}) {
  const params = useParams<{ agentId?: string }>();

  return <AiChatMock key={params.agentId ?? '__orchestrator__'} agentId={params.agentId ?? agentId} agents={agents} onSelectAgent={onSelectAgent} />;
}

function BootstrapStatusUnavailablePage({ message }: { message: string }) {
  return (
    <div className="flex items-center justify-center min-h-screen w-screen overflow-auto bg-[var(--color-background)] px-6">
      <div className="max-w-[32rem] rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] p-8 shadow-sm">
        <p className="text-sm font-semibold text-[var(--color-brand-primary)]">Setup Status Unavailable</p>
        <h1 className="mt-2 text-2xl font-bold text-[var(--color-text-primary)]">We could not determine first-run status</h1>
        <p className="mt-3 text-sm leading-relaxed text-[var(--color-text-secondary)]">{message}</p>
        <div className="mt-6 flex flex-col gap-3 sm:flex-row">
          <button
            type="button"
            onClick={() => window.location.reload()}
            className="rounded-md bg-[var(--color-brand-primary)] px-4 py-2 text-sm font-medium text-white hover:opacity-90"
          >
            Retry
          </button>
          <button
            type="button"
            onClick={() => window.location.assign('/setup')}
            className="rounded-md border border-[var(--color-border)] px-4 py-2 text-sm font-medium text-[var(--color-text-primary)] hover:bg-[var(--color-surface-inset)]"
          >
            Open setup page
          </button>
        </div>
      </div>
    </div>
  );
}

function AuthenticatedApp() {
  const [needsSetup, setNeedsSetup] = useState<boolean | null>(null);
  const [tenantNeedsSetup, setTenantNeedsSetup] = useState<boolean | null>(null);
  const [bootstrapStatusError, setBootstrapStatusError] = useState<string | null>(null);
  const { isLoading: authLoading, isAuthenticated, accessToken, getAccessToken } = useAuth();

  useEffect(() => {
    const checkSetup = async () => {
      // Ensure API layer has the latest token getter before any calls
      setAccessTokenGetter(getAccessToken);

      setBootstrapStatusError(null);

      // Retry bootstrap status check up to 3 times on transient failure.
      let status: Awaited<ReturnType<typeof bootstrapService.status>> | null = null;
      let lastStatusError = 'Unable to determine setup status right now.';
      for (let attempt = 0; attempt < 3; attempt++) {
        try {
          status = await bootstrapService.status(attempt > 0);
          break;
        } catch (error) {
          if (error && typeof error === 'object' && 'userMessage' in error) {
            lastStatusError = String((error as { userMessage?: string }).userMessage ?? lastStatusError);
          }
          if (attempt < 2) {
            await new Promise(r => setTimeout(r, 1000 * (attempt + 1)));
          }
        }
      }

      if (!status) {
        console.warn('Bootstrap status check failed after retries');
        setBootstrapStatusError(lastStatusError);
        setNeedsSetup(false);
        setTenantNeedsSetup(false);
        return;
      }

      setNeedsSetup(status.tenantCount === 0);

      if (status.tenantCount === 0) {
        setTenantNeedsSetup(false);
        return;
      }

      if (authLoading) {
        setTenantNeedsSetup(false);
        return;
      }
        
      // Only check tenant setup if user is authenticated and tenants exist
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
              setTenantNeedsSetup(false);
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
    };

    checkSetup();
  }, [authLoading, isAuthenticated, accessToken, getAccessToken]);

  if (needsSetup === null || tenantNeedsSetup === null) {
    return (
      <LoadingScreen
        phase={needsSetup === null ? 'loading-workspace' : 'hydrating-ledger'}
      />
    );
  }

  if (bootstrapStatusError) {
    return <BootstrapStatusUnavailablePage message={bootstrapStatusError} />;
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
          <div className="flex-1 min-w-0 flex flex-col overflow-hidden">
            <AuthenticatedApp />
            <Toaster richColors position="top-right" />
          </div>
        </AuthProvider>
      </ThemeProvider>
    </BrowserRouter>
  );
}

export default App;
