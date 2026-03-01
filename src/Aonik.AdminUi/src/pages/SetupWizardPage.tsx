import { useEffect, useMemo, useState } from 'react';
import { AlertCircle, CheckCircle2, Circle, ExternalLink, RefreshCw, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuth, getAuthProvider } from '@/auth';
import { bootstrapService } from '@/services/bootstrapService';
import { clearSelectedTenant, setSelectedTenant } from '@/lib/tenantContext';
import type { BootstrapTenantResult } from '@/types';

interface SetupState {
  loading: boolean;
  platformAdminConfigured: boolean;
  tenantCount: number | null;
  isCurrentUserAllowed: boolean;
  canBootstrap: boolean;
  apiIsAuthenticated: boolean;
  apiAuthorizationHeaderPresent: boolean;
  apiBearerTokenLooksJwt: boolean;
  apiAuthFailureReason: string | null;
  resolvedUserEmail: string | null;
  error: string | null;
}

const initialState: SetupState = {
  loading: true,
  platformAdminConfigured: false,
  tenantCount: null,
  isCurrentUserAllowed: false,
  canBootstrap: false,
  apiIsAuthenticated: false,
  apiAuthorizationHeaderPresent: false,
  apiBearerTokenLooksJwt: false,
  apiAuthFailureReason: null,
  resolvedUserEmail: null,
  error: null,
};

export function SetupWizardPage() {
  const { isAuthenticated, isLoading: authLoading, login, logout, user, accessToken, getAccessToken } = useAuth();
  const provider = getAuthProvider();
  const [state, setState] = useState<SetupState>(initialState);
  const [bootstrapResult, setBootstrapResult] = useState<BootstrapTenantResult | null>(null);
  const [isBootstrapping, setIsBootstrapping] = useState(false);

  const platformAdminStatus = state.platformAdminConfigured ? 'Configured' : 'Missing';
  const frontendEmail = user?.email?.trim() ?? null;
  const resolvedUserEmail = state.resolvedUserEmail?.trim() ?? null;
  const doEmailsMatch =
    frontendEmail !== null
    && resolvedUserEmail !== null
    && frontendEmail.localeCompare(resolvedUserEmail, undefined, { sensitivity: 'accent' }) === 0;

  const providerName = useMemo(() => {
    if (provider === 'azure-ad') return 'Microsoft Entra ID';
    if (provider === 'auth0') return 'Auth0';
    return 'Mock';
  }, [provider]);

  const loadSetupState = async () => {
    setState((prev) => ({ ...prev, loading: true, error: null }));
    try {
      const token = accessToken ?? (isAuthenticated ? await getAccessToken() : null);
      const status = await bootstrapService.status(true, token);
      if (status.tenantCount === 0) {
        clearSelectedTenant();
      }
        setState({
          loading: false,
          platformAdminConfigured: status.platformAdminEmailsConfigured,
          tenantCount: status.tenantCount,
          isCurrentUserAllowed: status.isCurrentUserAllowed,
          canBootstrap: status.canBootstrap ?? (status.tenantCount === 0 && status.isCurrentUserAllowed),
          apiIsAuthenticated: status.isAuthenticated ?? false,
          apiAuthorizationHeaderPresent: status.authorizationHeaderPresent ?? false,
          apiBearerTokenLooksJwt: status.bearerTokenLooksJwt ?? false,
          apiAuthFailureReason: status.authFailureReason ?? null,
          resolvedUserEmail: status.resolvedUserEmail ?? null,
          error: null,
        });

    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
        setState({
          loading: false,
          platformAdminConfigured: false,
          tenantCount: null,
          isCurrentUserAllowed: false,
          canBootstrap: false,
          apiIsAuthenticated: false,
          apiAuthorizationHeaderPresent: false,
          apiBearerTokenLooksJwt: false,
          apiAuthFailureReason: null,
          resolvedUserEmail: null,
          error: message || 'Unable to read setup configuration. Check API connectivity and admin access.',
        });

    }
  };

  useEffect(() => {
    if (!authLoading) {
      loadSetupState();
    }
  }, [authLoading]);

  const handleLogin = async () => {
    await login();
  };

  const handleBootstrap = async () => {
    setIsBootstrapping(true);
    try {
      const token = accessToken ?? (isAuthenticated ? await getAccessToken() : null);
      const result = await bootstrapService.bootstrap(token);
      setBootstrapResult(result);
      setSelectedTenant({
        tenantId: result.tenantId,
        name: result.tenantName,
      });

      window.location.replace('/setup/tenant');
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setState((prev) => ({
        ...prev,
        error: message || 'Bootstrap failed. Check PlatformAdmin access and try again.',
      }));
    } finally {
      setIsBootstrapping(false);
    }
  };

  const handleLogout = async () => {
    await logout();
  };

  const handleGoToDashboard = () => {
    window.location.href = '/';
  };

  const tenantExists = (state.tenantCount ?? 0) > 0;
  const canBootstrap = !tenantExists && state.canBootstrap;

  return (
    <div className="min-h-screen bg-[var(--color-background)]">
      <div className="max-w-5xl mx-auto px-6 py-10">
        <div className="flex flex-col gap-2 mb-8">
          <p className="text-sm font-semibold text-[var(--color-brand-primary)]">Initial Setup</p>
          <h1 className="text-3xl font-bold text-[var(--color-text-primary)]">Welcome to the Future of Finance</h1>
          <p className="text-[var(--color-text-secondary)] max-w-[42rem] leading-relaxed">
            Step into AI-powered financial operations. This wizard will get your Aonik platform running with intelligent automation, 
            smart insights, and seamless money movement at your fingertips.
          </p>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <Card className="lg:col-span-2">
            <CardHeader>
              <CardTitle>Setup Checklist</CardTitle>
              <CardDescription>Complete each step to unlock the bootstrap action.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-5">
              <SetupStep
                title="Review bootstrap access"
                status={state.platformAdminConfigured ? 'complete' : 'warning'}
                description={
                  state.platformAdminConfigured
                    ? 'PlatformAdmin.AdminEmails is configured for bootstrap email checks.'
                    : `Optional in Development. In non-Development environments, bootstrap requires PlatformAdmin access claims.`
                }
                action={
                  !state.platformAdminConfigured && (
                    <Button variant="outline" size="sm" onClick={loadSetupState}>
                      <RefreshCw className="w-4 h-4 mr-2" />
                      Re-check config
                    </Button>
                  )
                }
              />

              <SetupStep
                title="Sign in with your identity provider"
                status={
                  isAuthenticated
                    ? state.isCurrentUserAllowed
                      ? 'complete'
                      : 'warning'
                    : 'pending'
                }
                description={
                  isAuthenticated
                    ? state.isCurrentUserAllowed
                      ? `Signed in as ${user?.email ?? 'authenticated user'} via ${providerName}.`
                      : !state.apiIsAuthenticated
                        ? !state.apiAuthorizationHeaderPresent
                          ? `Signed in as ${user?.email ?? 'authenticated user'}, but no Bearer token was sent to /bootstrap/status.`
                          : !state.apiBearerTokenLooksJwt
                            ? `Signed in as ${user?.email ?? 'authenticated user'}, but the access token is not a JWT. Check VITE_AUTH0_AUDIENCE in Admin UI env.`
                            : `Signed in as ${user?.email ?? 'authenticated user'}, but API token validation failed${state.apiAuthFailureReason ? `: ${state.apiAuthFailureReason}.` : ' (issuer/audience/provider mismatch).'}`
                        : !state.resolvedUserEmail
                          ? `Signed in as ${user?.email ?? 'authenticated user'}, but the API token did not include an email claim.`
                          : doEmailsMatch
                            ? `Signed in as ${user?.email ?? 'authenticated user'}, but this account is not authorized to bootstrap in this environment.`
                            : `Signed in as ${user?.email ?? 'authenticated user'}, but the API resolved your token email as ${state.resolvedUserEmail}.`
                    : `Log in with ${providerName}.`
                }
                action={
                  isAuthenticated ? (
                    <Button variant="outline" size="sm" onClick={handleLogout}>
                      Sign out
                    </Button>
                  ) : (
                    <Button variant="secondary" size="sm" onClick={handleLogin}>
                      Sign in
                    </Button>
                  )
                }
              />

              <SetupStep
                title="Create the first tenant"
                status={tenantExists ? 'complete' : canBootstrap ? 'pending' : 'locked'}
                description={
                  tenantExists
                    ? 'A tenant already exists. Setup is complete.'
                    : canBootstrap
                      ? 'Run bootstrap to create the first tenant and admin role.'
                      : 'Sign in with an authorized account to bootstrap the first tenant.'
                }
                action={
                  !tenantExists && (
                    <Button onClick={handleBootstrap} disabled={!canBootstrap || isBootstrapping}>
                      {isBootstrapping ? 'Bootstrapping...' : 'Run bootstrap'}
                    </Button>
                  )
                }
              />
            </CardContent>
            <CardFooter className="flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between">
              <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                <ShieldCheck className="w-4 h-4" />
                The initial platform admin is created from your authenticated identity.
              </div>
              <Button variant="outline" size="sm" onClick={loadSetupState} disabled={state.loading}>
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
              <StatusRow label="Platform admin emails" value={platformAdminStatus} />
              <StatusRow label="Tenants" value={state.tenantCount === null ? 'Unknown' : `${state.tenantCount}`} />
              <StatusRow label="API auth state" value={state.apiIsAuthenticated ? 'Authenticated' : 'Anonymous'} />
              <StatusRow label="Bearer header" value={state.apiAuthorizationHeaderPresent ? 'Present' : 'Missing'} />
              <StatusRow label="Token format" value={state.apiBearerTokenLooksJwt ? 'JWT' : 'Opaque/Missing'} />
              <StatusRow label="Auth failure" value={state.apiAuthFailureReason || 'None'} />

              {bootstrapResult && (
                <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4 text-sm text-[var(--color-text-secondary)]">
                  <p className="font-semibold text-[var(--color-text-primary)] mb-2">Bootstrap complete</p>
                  <p>Tenant: {bootstrapResult.tenantName}</p>
                  <p>Tenant ID: {bootstrapResult.tenantId}</p>
                  <p>User ID: {bootstrapResult.userId}</p>
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
              <Button variant="secondary" onClick={handleGoToDashboard} disabled={!tenantExists} className="w-full">
                Go to dashboard
              </Button>
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
  action,
}: {
  title: string;
  description: string;
  status: 'complete' | 'pending' | 'warning' | 'locked';
  action?: React.ReactNode;
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
      <div className="flex items-start gap-3">
        <div className={`flex h-9 w-9 items-center justify-center rounded-full ${config.bg}`}>
          <Icon className={`h-4 w-4 ${config.text}`} />
        </div>
        <div className="flex-1">
          <p className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</p>
          <p className="text-sm text-[var(--color-text-secondary)]">{description}</p>
        </div>
        {action ? <div className="shrink-0">{action}</div> : null}
      </div>
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
