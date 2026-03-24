import { useEffect, useMemo, useState } from 'react';
import { AlertCircle, CheckCircle2, Circle, ExternalLink, RefreshCw, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAuth, getAuthProvider } from '@/auth';
import { bootstrapService } from '@/services/bootstrapService';
import { clearSelectedTenant, setSelectedTenant } from '@/lib/tenantContext';
import type { BootstrapTenantResult } from '@/types';

interface SetupState {
  loading: boolean;
  bootstrapState: 'ready' | 'completed' | 'disabled' | 'misconfigured' | null;
  bootstrapEnabled: boolean;
  setupSecretConfigured: boolean;
  tenantCount: number | null;
  canBootstrap: boolean;
  message: string | null;
  error: string | null;
}

const initialState: SetupState = {
  loading: true,
  bootstrapState: null,
  bootstrapEnabled: false,
  setupSecretConfigured: false,
  tenantCount: null,
  canBootstrap: false,
  message: null,
  error: null,
};

export function SetupWizardPage() {
  const { isAuthenticated } = useAuth();
  const provider = getAuthProvider();
  const [state, setState] = useState<SetupState>(initialState);
  const [bootstrapResult, setBootstrapResult] = useState<BootstrapTenantResult | null>(null);
  const [isBootstrapping, setIsBootstrapping] = useState(false);
  const [setupSecret, setSetupSecret] = useState('');
  const [ownerEmail, setOwnerEmail] = useState('');
  const [ownerDisplayName, setOwnerDisplayName] = useState('');

  const bootstrapStateLabel = useMemo(() => {
    if (state.bootstrapState === 'ready') return 'Ready';
    if (state.bootstrapState === 'completed') return 'Completed';
    if (state.bootstrapState === 'disabled') return 'Disabled';
    if (state.bootstrapState === 'misconfigured') return 'Misconfigured';
    return 'Unknown';
  }, [state.bootstrapState]);

  const providerName = useMemo(() => {
    if (provider === 'azure-ad') return 'Microsoft Entra ID';
    if (provider === 'auth0') return 'Auth0';
    return 'Mock';
  }, [provider]);

  const ownerEmailLooksValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(ownerEmail.trim());

  const loadSetupState = async (forceRefresh = false) => {
    setState((prev) => ({ ...prev, loading: true, error: null }));
    try {
      const status = await bootstrapService.status(forceRefresh);
      if (status.tenantCount === 0) {
        clearSelectedTenant();
      }
      setState({
        loading: false,
        bootstrapState: status.state,
        bootstrapEnabled: status.bootstrapEnabled,
        setupSecretConfigured: status.setupSecretConfigured,
        tenantCount: status.tenantCount,
        canBootstrap: status.canBootstrap,
        message: status.message ?? null,
        error: null,
      });
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setState({
        loading: false,
        bootstrapState: null,
        bootstrapEnabled: false,
        setupSecretConfigured: false,
        tenantCount: null,
        canBootstrap: false,
        message: null,
        error: message || 'Unable to read setup configuration. Check API connectivity and bootstrap settings.',
      });
    }
  };

  useEffect(() => {
    void loadSetupState();
  }, []);

  const handleBootstrap = async () => {
    if (!setupSecret.trim()) {
      setState((prev) => ({ ...prev, error: 'Enter the install code before continuing.' }));
      return;
    }

    if (!ownerEmailLooksValid) {
      setState((prev) => ({ ...prev, error: 'Enter a valid owner email address before continuing.' }));
      return;
    }

    setIsBootstrapping(true);
    setState((prev) => ({ ...prev, error: null }));
    try {
      const result = await bootstrapService.bootstrap({
        setupSecret: setupSecret.trim(),
        ownerEmail: ownerEmail.trim(),
        ownerDisplayName: ownerDisplayName.trim() || null,
      });
      setBootstrapResult(result);
      setSelectedTenant({
        tenantId: result.tenantId,
        name: result.tenantName,
      });

      const nextPath = isAuthenticated
        ? '/setup/tenant'
        : `/login?returnTo=${encodeURIComponent('/setup/tenant')}`;
      window.location.replace(nextPath);
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setState((prev) => ({
        ...prev,
        error: message || 'Bootstrap failed. Check the install code and bootstrap configuration, then try again.',
      }));
    } finally {
      setIsBootstrapping(false);
    }
  };

  const handleGoToMySpace = () => {
    window.location.href = '/';
  };

  const handleContinueToSignIn = () => {
    window.location.href = `/login?returnTo=${encodeURIComponent('/setup/tenant')}`;
  };

  const tenantExists = (state.tenantCount ?? 0) > 0;
  const canBootstrap = !tenantExists && state.canBootstrap;

  return (
    <div className="flex-1 h-full overflow-auto bg-[var(--color-background)]">
      <div className="w-full max-w-[1400px] mx-auto px-8 py-12 lg:px-12">
        <div className="flex flex-col gap-2 mb-10">
          <p className="text-sm font-semibold text-[var(--color-brand-primary)]">Initial Setup</p>
          <h1 className="text-3xl font-bold text-[var(--color-text-primary)]">Welcome to the Future of Finance</h1>
          <p className="text-[var(--color-text-secondary)] max-w-[52rem] leading-relaxed">
            Step into AI-powered financial operations. This wizard will get your Aonik platform running with intelligent automation, 
            smart insights, and seamless money movement at your fingertips.
          </p>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          <Card className="lg:col-span-2">
            <CardHeader>
              <CardTitle>Setup Checklist</CardTitle>
              <CardDescription>Use the one-time install code to create the first tenant and owner profile.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-5">
              <SetupStep
                title="Review bootstrap availability"
                status={tenantExists ? 'complete' : state.canBootstrap ? 'complete' : state.bootstrapState === 'misconfigured' || state.bootstrapState === 'disabled' ? 'warning' : 'locked'}
                description={
                  state.message
                    ?? 'Bootstrap status is loading.'
                }
                inlineAction={
                  !tenantExists && (
                    <Button variant="outline" size="sm" onClick={() => void loadSetupState(true)}>
                      <RefreshCw className="w-4 h-4 mr-2" />
                      Re-check config
                    </Button>
                  )
                }
              />

              <SetupStep
                title="Enter the install code"
                status={tenantExists ? 'complete' : setupSecret.trim() ? 'complete' : state.canBootstrap ? 'pending' : 'locked'}
                description={
                  tenantExists
                    ? 'The initial tenant already exists, so the install code is no longer needed.'
                    : 'Only system owners who know the install code can run first-time bootstrap.'
                }
              >
                {!tenantExists && (
                  <div className="w-full max-w-[36rem] space-y-2">
                    <Label htmlFor="setup-secret">Install code</Label>
                    <Input
                      id="setup-secret"
                      className="h-11 rounded-md"
                      type="password"
                      autoComplete="off"
                      value={setupSecret}
                      onChange={(event) => {
                        setSetupSecret(event.target.value);
                        if (state.error) {
                          setState((prev) => ({ ...prev, error: null }));
                        }
                      }}
                      placeholder="Paste the one-time install code"
                    />
                    <p className="text-xs text-[var(--color-text-tertiary)]">
                      This must match the current `BOOTSTRAP_SETUP_SECRET` configured for the API.
                    </p>
                  </div>
                )}
              </SetupStep>

              <SetupStep
                title="Define the initial owner"
                status={tenantExists ? 'complete' : ownerEmailLooksValid ? 'complete' : state.canBootstrap ? 'pending' : 'locked'}
                description={
                  tenantExists
                    ? 'A tenant already exists. Setup is complete.'
                    : `Provide the email that will be linked to the first ${providerName} sign-in after bootstrap.`
                }
              >
                {!tenantExists && (
                  <div className="grid w-full max-w-[44rem] gap-4 md:grid-cols-2">
                    <div className="space-y-2 md:col-span-2">
                      <Label htmlFor="owner-email">Owner email</Label>
                      <Input
                        id="owner-email"
                        className="h-11 rounded-md"
                        type="email"
                        autoComplete="email"
                        value={ownerEmail}
                        onChange={(event) => {
                          setOwnerEmail(event.target.value);
                          if (state.error) {
                            setState((prev) => ({ ...prev, error: null }));
                          }
                        }}
                        placeholder="owner@example.com"
                      />
                    </div>
                    <div className="space-y-2 md:col-span-2">
                      <Label htmlFor="owner-display-name">Owner display name</Label>
                      <Input
                        id="owner-display-name"
                        className="h-11 rounded-md"
                        value={ownerDisplayName}
                        onChange={(event) => {
                          setOwnerDisplayName(event.target.value);
                          if (state.error) {
                            setState((prev) => ({ ...prev, error: null }));
                          }
                        }}
                        placeholder="Optional display name for the owner"
                      />
                    </div>
                  </div>
                )}
              </SetupStep>

              <SetupStep
                title="Create the first tenant"
                status={tenantExists ? 'complete' : canBootstrap && ownerEmailLooksValid && setupSecret.trim() ? 'pending' : 'locked'}
                description={
                  tenantExists
                    ? 'Bootstrap already completed. Sign in to continue tenant setup.'
                    : 'Bootstrap will create the initial tenant, seed roles, and prepare the owner profile for identity linking.'
                }
              >
                {!tenantExists && (
                  <div className="flex w-full flex-col gap-3 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4 sm:flex-row sm:items-center sm:justify-between">
                    <div className="text-sm text-[var(--color-text-secondary)]">
                      When both fields are complete, run bootstrap to create the tenant and continue into guided setup.
                    </div>
                    <Button
                      onClick={handleBootstrap}
                      disabled={!canBootstrap || !ownerEmailLooksValid || !setupSecret.trim() || isBootstrapping}
                      className="w-full sm:w-auto"
                    >
                      {isBootstrapping ? 'Bootstrapping...' : 'Run bootstrap'}
                    </Button>
                  </div>
                )}
              </SetupStep>
            </CardContent>
            <CardFooter className="flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between">
              <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                <ShieldCheck className="w-4 h-4" />
                Bootstrap creates a pending owner profile first, then links it to {providerName} on the next sign-in.
              </div>
              <Button variant="outline" size="sm" onClick={() => void loadSetupState(true)} disabled={state.loading}>
                <RefreshCw className={`w-4 h-4 mr-2 ${state.loading ? 'animate-spin' : ''}`} />
                Refresh status
              </Button>
            </CardFooter>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Setup Status</CardTitle>
              <CardDescription>Live configuration and bootstrap results.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <StatusRow label="Bootstrap state" value={bootstrapStateLabel} />
              <StatusRow label="Bootstrap enabled" value={state.bootstrapEnabled ? 'Yes' : 'No'} />
              <StatusRow label="Install code configured" value={state.setupSecretConfigured ? 'Yes' : 'No'} />
              <StatusRow label="Tenants" value={state.tenantCount === null ? 'Unknown' : `${state.tenantCount}`} />
              <StatusRow label="Ready to bootstrap" value={state.canBootstrap ? 'Yes' : 'No'} />

              {bootstrapResult && (
                <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4 text-sm text-[var(--color-text-secondary)]">
                  <p className="font-semibold text-[var(--color-text-primary)] mb-2">Bootstrap complete</p>
                  <p>Tenant: {bootstrapResult.tenantName}</p>
                  <p>Tenant ID: {bootstrapResult.tenantId}</p>
                  <p>User ID: {bootstrapResult.userId}</p>
                  <p>Owner email: {bootstrapResult.ownerEmail}</p>
                </div>
              )}

              {state.error && (
                <div className="flex gap-2 rounded-md border border-[var(--color-error)]/20 bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
                  <AlertCircle className="w-4 h-4 mt-0.5" />
                  <span>{state.error}</span>
                </div>
              )}
            </CardContent>
            <CardFooter className="flex flex-col gap-2">
              {tenantExists ? (
                isAuthenticated ? (
                  <Button variant="secondary" onClick={handleGoToMySpace} className="w-full">
                    Go to My Space
                  </Button>
                ) : (
                  <Button variant="secondary" onClick={handleContinueToSignIn} className="w-full">
                    Continue to sign in
                  </Button>
                )
              ) : (
                <Button variant="secondary" onClick={handleGoToMySpace} disabled className="w-full">
                  Go to My Space
                </Button>
              )}
              <a
                className="inline-flex items-center justify-center text-sm text-[var(--color-brand-primary)] hover:underline"
                href="/setup-guides"
                target="_blank"
                rel="noreferrer"
              >
                View getting started guide
                <ExternalLink className="w-4 h-4 ml-1" />
              </a>
            </CardFooter>
          </Card>
        </div>
      </div>
    </div>
  );
}

function SetupStep({
  title,
  description,
  status,
  inlineAction,
  children,
}: {
  title: string;
  description: string;
  status: 'complete' | 'pending' | 'warning' | 'locked';
  inlineAction?: React.ReactNode;
  children?: React.ReactNode;
}) {
  const statusConfig = {
    complete: {
      icon: CheckCircle2,
      bg: 'bg-[var(--color-success-light)]',
      text: 'text-[var(--color-success)]',
    },
    pending: {
      icon: Circle,
      bg: 'bg-[var(--color-info-light)]',
      text: 'text-[var(--color-info)]',
    },
    warning: {
      icon: AlertCircle,
      bg: 'bg-[var(--color-warning-light)]',
      text: 'text-[var(--color-warning)]',
    },
    locked: {
      icon: AlertCircle,
      bg: 'bg-[var(--color-surface-inset)]',
      text: 'text-[var(--color-text-tertiary)]',
    },
  } as const;

  const config = statusConfig[status];
  const Icon = config.icon;

  return (
    <div className="flex flex-col gap-3 rounded-md border border-[var(--color-border-light)] p-4">
      <div className="flex flex-col gap-4 md:flex-row md:items-start">
        <div className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${config.bg}`}>
          <Icon className={`h-4 w-4 ${config.text}`} />
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</p>
          <p className="text-sm text-[var(--color-text-secondary)]">{description}</p>
        </div>
        {inlineAction ? <div className="shrink-0 self-start">{inlineAction}</div> : null}
      </div>
      {children ? <div className="w-full md:pl-[3.25rem]">{children}</div> : null}
    </div>
  );
}

function StatusRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between text-sm">
      <span className="text-[var(--color-text-secondary)]">{label}</span>
      <span className="font-medium text-[var(--color-text-primary)]">{value}</span>
    </div>
  );
}
