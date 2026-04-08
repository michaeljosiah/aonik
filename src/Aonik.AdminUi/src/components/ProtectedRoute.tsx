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
          <div className="text-center p-8 bg-[var(--color-surface)] rounded-md shadow-lg max-w-[28rem]">
            <svg 
              className="w-16 h-16 mx-auto mb-4 text-[var(--color-warning)]" 
              fill="none" 
              viewBox="0 0 24 24" 
              stroke="currentColor"
            >
              <path 
                strokeLinecap="round" 
                strokeLinejoin="round" 
                strokeWidth={2} 
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" 
              />
            </svg>
            <h2 className="text-xl font-semibold text-[var(--color-text-primary)] mb-2">
              Access Denied
            </h2>
            <p className="text-[var(--color-text-secondary)] mb-4">
              You don't have permission to access this page. You may need additional permissions or a different user role.
            </p>
            <div className="bg-[var(--color-background)] p-3 rounded-md text-sm text-[var(--color-text-tertiary)] mb-4">
              <p><strong>Required role:</strong> {requiredRoles.join(' or ')}</p>
            </div>
            <p className="text-sm text-[var(--color-text-tertiary)]">
              If you believe you should have access, please contact your system administrator to request the appropriate permissions.
            </p>
            <button
              onClick={() => window.history.back()}
              className="mt-6 px-4 py-2 bg-[var(--color-brand-primary)] text-white rounded-md hover:opacity-90 transition-opacity"
            >
              Go Back
            </button>
          </div>
        </div>
      );
    }
  }

  return <>{children}</>;
}
