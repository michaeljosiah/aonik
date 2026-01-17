import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '@/auth';

interface ProtectedRouteProps {
  children: React.ReactNode;
  requiredRoles?: string[];
}

export function ProtectedRoute({ children, requiredRoles }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, user } = useAuth();
  const location = useLocation();

  // Show loading state while checking authentication
  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-[var(--color-background)]">
        <div className="flex flex-col items-center gap-4">
          <div className="w-8 h-8 border-4 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
          <p className="text-sm text-[var(--color-text-secondary)]">Loading...</p>
        </div>
      </div>
    );
  }

  // Redirect to login if not authenticated
  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  // Check for required roles if specified
  if (requiredRoles && requiredRoles.length > 0) {
    const userRoles = user?.roles || [];
    const hasRequiredRole = requiredRoles.some((role) => userRoles.includes(role));

    if (!hasRequiredRole) {
      return (
        <div className="flex items-center justify-center min-h-screen bg-[var(--color-background)]">
          <div className="text-center p-8 bg-[var(--color-surface)] rounded-lg shadow-lg max-w-md">
            <h2 className="text-xl font-semibold text-[var(--color-text-primary)] mb-2">
              Access Denied
            </h2>
            <p className="text-[var(--color-text-secondary)] mb-4">
              You don't have permission to access this page.
            </p>
            <p className="text-sm text-[var(--color-text-tertiary)]">
              Required role: {requiredRoles.join(' or ')}
            </p>
          </div>
        </div>
      );
    }
  }

  return <>{children}</>;
}
