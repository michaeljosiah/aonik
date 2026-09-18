import { useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { ArrowRight, Building2 } from 'lucide-react';

import { useAuth } from '@/auth';
import { LoadingScreen } from '@/components/layout';
import { setSelectedTenant } from '@/lib/tenantContext';
import { useTenantBootstrap } from '@/hooks/useTenantBootstrap';
import { invalidateModuleManifest } from '@/modules/manifestCache';
import type { MyTenantSummary } from '@/types';

/**
 * Post-auth organization picker. Reached when the authenticated identity
 * has memberships in two or more tenants and there's no still-valid
 * selection in localStorage.
 *
 * The page reuses {@link useTenantBootstrap} so it shares the in-flight
 * cache with the upstream resolution gate — no second network call.
 */
export function OrganizationPickerPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const { state, refetch } = useTenantBootstrap(isAuthenticated && !authLoading);

  const from =
    (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/';

  // If resolution ends up auto-selecting (single tenant) or revives a
  // cached choice, bounce back out of the picker.
  useEffect(() => {
    if (state.kind === 'ready') {
      navigate(from, { replace: true });
    }
  }, [state, navigate, from]);

  const choose = (tenant: MyTenantSummary) => {
    setSelectedTenant({
      tenantId: tenant.tenantId,
      name: tenant.name,
      subdomain: tenant.subdomain,
      environment: tenant.environment,
    });
    // The module manifest is tenant-scoped; drop anything cached before.
    invalidateModuleManifest();
    navigate(from, { replace: true });
  };

  if (!isAuthenticated || authLoading) {
    return <LoadingScreen phase="authenticating" />;
  }

  if (state.kind === 'loading') {
    return <LoadingScreen phase="loading-workspace" />;
  }

  if (state.kind === 'error') {
    return <PickerMessage title="Couldn't load your organizations" body={state.message} actionLabel="Retry" onAction={refetch} />;
  }

  if (state.kind === 'none') {
    return (
      <PickerMessage
        title="No organizations available"
        body="Your account isn't a member of any active organization yet. If you were just invited, ask your administrator to confirm your access."
      />
    );
  }

  if (state.kind === 'ready') {
    // Already resolved; the effect above will navigate away on the next tick.
    return <LoadingScreen phase="loading-workspace" />;
  }

  const tenants = state.tenants;

  return (
    <div
      className="min-h-screen w-full flex items-center justify-center px-6 py-10"
      style={{ background: 'var(--color-background)' }}
    >
      <div className="w-full max-w-[28rem]">
        <header className="mb-6 text-center">
          <h1
            className="text-2xl font-semibold"
            style={{ color: 'var(--color-text-primary)' }}
          >
            Choose an organization
          </h1>
          <p
            className="mt-2 text-sm"
            style={{ color: 'var(--color-text-secondary)' }}
          >
            Your account has access to {tenants.length} organizations. Pick the one you want to work in.
          </p>
        </header>

        <ul className="space-y-2">
          {tenants.map((t) => (
            <li key={t.tenantId}>
              <button
                type="button"
                onClick={() => choose(t)}
                className="w-full flex items-center gap-3 px-4 py-3 rounded-md text-left transition-colors"
                style={{
                  background: 'var(--color-surface)',
                  border: '1px solid var(--color-border)',
                  color: 'var(--color-text-primary)',
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = 'var(--color-surface-inset)';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = 'var(--color-surface)';
                }}
              >
                <span
                  className="shrink-0 rounded-md p-2"
                  style={{ background: 'var(--color-surface-inset)' }}
                  aria-hidden
                >
                  <Building2 size={18} />
                </span>
                <span className="flex-1 min-w-0">
                  <span className="block font-medium truncate">{t.name}</span>
                  <span
                    className="block text-xs truncate"
                    style={{ color: 'var(--color-text-tertiary)' }}
                  >
                    {t.environment}
                    {t.subdomain ? ` · ${t.subdomain}` : ''}
                  </span>
                </span>
                <ArrowRight size={16} aria-hidden style={{ color: 'var(--color-text-tertiary)' }} />
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

function PickerMessage({
  title,
  body,
  actionLabel,
  onAction,
}: {
  title: string;
  body: string;
  actionLabel?: string;
  onAction?: () => void;
}) {
  return (
    <div
      className="min-h-screen w-full flex items-center justify-center px-6"
      style={{ background: 'var(--color-background)' }}
    >
      <div
        className="w-full max-w-[26rem] rounded-md p-6 text-center"
        style={{
          background: 'var(--color-surface)',
          border: '1px solid var(--color-border)',
        }}
      >
        <h1
          className="text-lg font-semibold mb-2"
          style={{ color: 'var(--color-text-primary)' }}
        >
          {title}
        </h1>
        <p
          className="text-sm mb-4"
          style={{ color: 'var(--color-text-secondary)' }}
        >
          {body}
        </p>
        {actionLabel && onAction && (
          <button
            type="button"
            onClick={onAction}
            className="px-4 py-2 rounded-md text-sm font-medium transition-opacity"
            style={{
              background: 'var(--color-brand-primary)',
              color: 'white',
            }}
            onMouseEnter={(e) => (e.currentTarget.style.opacity = '0.9')}
            onMouseLeave={(e) => (e.currentTarget.style.opacity = '1')}
          >
            {actionLabel}
          </button>
        )}
      </div>
    </div>
  );
}
