import { Navigate, useLocation } from 'react-router-dom';

import { useAuth } from '@/auth';
import { LoadingScreen } from '@/components/layout';
import { useTenantBootstrap } from '@/hooks/useTenantBootstrap';

/**
 * Gate that sits between {@link ProtectedRoute} and the application
 * shell, ensuring a tenant is resolved before any tenant-scoped UI
 * renders. Authentication is assumed (this should be composed inside
 * ProtectedRoute).
 *
 * Resolution rules — see {@link useTenantBootstrap}:
 *  - 1 membership: auto-select, render children.
 *  - 2+ memberships with cached selection still valid: render children.
 *  - 2+ memberships, no valid cache: bounce to /select-organization.
 *  - 0 memberships or error: render the no-access screen inline. (We
 *    don't redirect to /login because the user *is* authenticated;
 *    re-prompting Auth0 would just sign them in again with the same
 *    empty-membership result.)
 */
export function TenantResolutionGate({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const location = useLocation();
  const { state, refetch } = useTenantBootstrap(isAuthenticated && !authLoading);

  if (!isAuthenticated || authLoading) {
    // ProtectedRoute should have already handled this, but render a
    // loading state defensively rather than a bare empty page.
    return <LoadingScreen phase="authenticating" />;
  }

  if (state.kind === 'loading') {
    return <LoadingScreen phase="loading-workspace" />;
  }

  if (state.kind === 'picker') {
    return <Navigate to="/select-organization" state={{ from: location }} replace />;
  }

  if (state.kind === 'none') {
    return (
      <GateMessage
        title="No organizations available"
        body="Your account isn't a member of any active organization. If you were just invited, ask your administrator to confirm your access."
      />
    );
  }

  if (state.kind === 'error') {
    return (
      <GateMessage
        title="Couldn't load your organizations"
        body={state.message}
        actionLabel="Retry"
        onAction={refetch}
      />
    );
  }

  return <>{children}</>;
}

function GateMessage({
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
