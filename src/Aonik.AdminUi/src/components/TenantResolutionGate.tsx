import { Navigate, useLocation } from 'react-router-dom';

import { useAuth } from '@/auth';
import { LoadingScreen } from '@/components/layout';
import { Button } from '@/components/ui/button';
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
    <div className="min-h-screen w-full flex items-center justify-center bg-background px-6">
      <div className="w-full max-w-[26rem] rounded-lg border bg-card p-6 text-center">
        <h1 className="text-lg font-semibold mb-2 text-foreground">{title}</h1>
        <p className="text-sm mb-4 text-muted-foreground">{body}</p>
        {actionLabel && onAction && (
          <Button type="button" onClick={onAction}>
            {actionLabel}
          </Button>
        )}
      </div>
    </div>
  );
}
