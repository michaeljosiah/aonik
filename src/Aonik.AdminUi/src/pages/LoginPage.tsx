import { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { ArrowRight, AlertCircle, Building2 } from 'lucide-react';
import { useAuth, getAuthProvider } from '@/auth';
import { tenantService } from '@/services/tenantService';
import type { TenantListItemForLogin } from '@/types';
import { getSelectedTenant, setSelectedTenant } from '@/lib/tenantContext';
import { isTenantScopedHostname } from '@/lib/tenantRouting';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated, isLoading, login, authError } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoggingIn, setIsLoggingIn] = useState(false);
  
  // Tenant selection state
  const [tenants, setTenants] = useState<TenantListItemForLogin[]>([]);
  const [selectedTenantId, setSelectedTenantId] = useState<string>('');
  const [isLoadingTenants, setIsLoadingTenants] = useState(false);
  const [showTenantSelector, setShowTenantSelector] = useState(false);

  // Check if URL is tenant-scoped (has subdomain)
  useEffect(() => {
    const hostname = window.location.hostname;
    const isTenantScoped = isTenantScopedHostname(hostname);
    
    setShowTenantSelector(!isTenantScoped);
  }, []);

  // Load tenants for dropdown
  useEffect(() => {
    if (!showTenantSelector) return;
    
    const loadTenants = async () => {
      setIsLoadingTenants(true);
      try {
        const response = await tenantService.listForLogin();
        setTenants(response.tenants);

        const previous = getSelectedTenant();
        if (previous?.tenantId) {
          const exists = response.tenants.some(t => t.tenantId === previous.tenantId);
          if (exists) {
            setSelectedTenantId(previous.tenantId);
            return;
          }
        }

        // Auto-select first tenant if available
        if (response.tenants.length > 0) {
          setSelectedTenantId(response.tenants[0].tenantId);
        }
      } catch (err) {
        console.error('Failed to load tenants:', err);
        // Don't block login if tenant list fails
      } finally {
        setIsLoadingTenants(false);
      }
    };
    
    loadTenants();
  }, [showTenantSelector]);

  useEffect(() => {
    if (authError) {
      const message = authError.message || 'Authentication failed. Please check Auth0 logs.';
      setError(message);
      setIsLoggingIn(false);
      console.error('Auth error:', authError);
    }
  }, [authError]);

  const provider = getAuthProvider();
  const query = new URLSearchParams(location.search);
  const returnTo = query.get('returnTo');
  const reason = query.get('reason');

  const from =
    (returnTo && returnTo.startsWith('/') && !returnTo.startsWith('/login') ? returnTo : null) ??
    (location.state as { from?: { pathname: string } })?.from?.pathname ??
    '/';

  useEffect(() => {
    if (!reason) return;

    if (reason === 'session-expired') {
      setNotice('Your session expired. Please sign in again.');
      return;
    }

    if (reason === 'tenant-missing') {
      setNotice('Select an organization to continue.');
      return;
    }
  }, [reason]);

  // Redirect if already authenticated
  useEffect(() => {
    // If we were explicitly redirected here due to an expired session or missing tenant context,
    // do not auto-bounce back to the protected route; that causes an auth redirect loop.
    if (reason === 'session-expired' || reason === 'tenant-missing') return;

    if (isAuthenticated && !isLoading) {
      navigate(from, { replace: true });
    }
  }, [isAuthenticated, isLoading, navigate, from, reason]);

  const handleLogin = async () => {
    // Validate tenant selection if showing selector
    if (showTenantSelector && !selectedTenantId && tenants.length > 0) {
      setError('Please select a tenant to continue.');
      return;
    }
    
    setError(null);
    setIsLoggingIn(true);
    
    try {
      // Store selected tenant for post-login context
      if (selectedTenantId) {
        const selected = tenants.find(t => t.tenantId === selectedTenantId);
        setSelectedTenant({
          tenantId: selectedTenantId,
          name: selected?.name,
          subdomain: selected?.subdomain,
          environment: selected?.environment,
        });
      }

      // If the user is already authenticated and we're only here to re-select tenant context,
      // avoid forcing an IdP roundtrip.
      if (isAuthenticated && reason === 'tenant-missing') {
        navigate(from, { replace: true });
        return;
      }
      await login();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to sign in. Please try again.');
      setIsLoggingIn(false);
    }
  };

  // Show loading while checking auth state
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

  return (
    <div className="flex min-h-screen w-full">
      {/* Left side - Branding */}
        <div className="hidden lg:flex lg:flex-col lg:justify-center w-1/2 bg-[var(--color-brand-primary)] relative overflow-hidden p-16 max-w-[50vw]">
        {/* Decorative circles */}
        <div className="absolute -top-32 -left-32 w-96 h-96 rounded-full bg-white/5" />
        <div className="absolute top-1/4 right-0 w-64 h-64 rounded-full bg-white/5" />
        <div className="absolute bottom-0 left-1/4 w-80 h-80 rounded-full bg-white/5" />
        
        {/* Accent dots */}
        <div className="absolute top-20 right-20 w-4 h-4 rounded-full bg-[var(--color-brand-secondary)]" />
        <div className="absolute bottom-40 left-20 w-3 h-3 rounded-full bg-[var(--color-brand-secondary)]" />

        {/* Content */}
        <div className="relative z-10 text-white">
          {/* Logo */}
          <div className="mb-12">
            <h1 className="text-4xl font-bold">
              Aonik<span className="text-[var(--color-brand-secondary)]">.</span>
            </h1>
          </div>

          {/* Tagline */}
          <h2 className="text-3xl font-semibold mb-4 leading-tight">
            AI-native financial<br />infrastructure
          </h2>
          <p className="text-lg text-white/80 mb-8 max-w-[32rem]">
            Power modern payments, billing, and financial intelligence with AI agents that assist with reconciliation, forecasting, and insights.
          </p>

          {/* Feature highlights */}
          <div className="flex flex-col gap-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-md bg-white/10 flex items-center justify-center">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
              </div>
              <span className="text-white/90">Intelligent reconciliation & anomaly detection</span>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-md bg-white/10 flex items-center justify-center">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                </svg>
              </div>
              <span className="text-white/90">Cash flow forecasting & spend insights</span>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-md bg-white/10 flex items-center justify-center">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                </svg>
              </div>
              <span className="text-white/90">Explainable, auditable AI you can trust</span>
            </div>
          </div>
        </div>
      </div>

      {/* Right side - Login */}
      <div className="flex-1 flex items-center justify-center p-8 bg-[var(--color-background)]">
        <div className="w-full max-w-[28rem] min-w-0">
          {/* Mobile logo */}
          <div className="lg:hidden text-center mb-8">
            <h1 className="text-3xl font-bold text-[var(--color-text-primary)]">
              Aonik<span className="text-[var(--color-brand-secondary)]">.</span>
            </h1>
          </div>

          <Card className="shadow-lg border-none">
            <CardContent className="p-8">
              {/* Header */}
              <div className="text-center mb-8">
                <h2 className="text-2xl font-bold text-[var(--color-text-primary)] mb-2">
                  Welcome back
                </h2>
                <p className="text-[var(--color-text-secondary)]">
                  Sign in to continue to your workspace
                </p>
              </div>

              {/* Error message */}
              {error && (
                <div className="flex items-center gap-2 p-3 mb-6 bg-[var(--color-error-light)] rounded-md border border-[var(--color-error)]/20">
                  <AlertCircle className="w-5 h-5 text-[var(--color-error)] flex-shrink-0" />
                  <p className="text-sm text-[var(--color-error)]">{error}</p>
                </div>
              )}

              {/* Notice message */}
              {notice && !error && (
                <div className="flex items-center gap-2 p-3 mb-6 bg-[var(--color-brand-primary-light)] rounded-md border border-[var(--color-brand-primary)]/20">
                  <AlertCircle className="w-5 h-5 text-[var(--color-brand-primary)] flex-shrink-0" />
                  <p className="text-sm text-[var(--color-brand-primary)]">{notice}</p>
                </div>
              )}

              {/* Tenant selector - only shown when URL is not tenant-scoped */}
              {showTenantSelector && (
                <div className="mb-6">
                  <label className="flex items-center gap-2 text-sm font-medium text-[var(--color-text-primary)] mb-2">
                    <Building2 className="w-4 h-4" />
                    Select Organization
                  </label>
                  {isLoadingTenants ? (
                    <div className="flex items-center justify-center py-3 px-4 border border-[var(--color-border)] rounded-md bg-[var(--color-surface-inset)]">
                      <div className="w-4 h-4 border-2 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin mr-2" />
                      <span className="text-sm text-[var(--color-text-secondary)]">Loading organizations...</span>
                    </div>
                  ) : tenants.length === 0 ? (
                    <div className="py-3 px-4 border border-[var(--color-border)] rounded-md bg-[var(--color-surface-inset)] text-center">
                      <p className="text-sm text-[var(--color-text-tertiary)]">No organizations available</p>
                    </div>
                  ) : (
                    <Select
                      value={selectedTenantId || undefined}
                      onValueChange={(value) => setSelectedTenantId(value === '__clear__' ? '' : value)}
                    >
                      <SelectTrigger
                        aria-label="Select organization"
                        className="w-full px-4 py-3 border border-[var(--color-border)] rounded-md text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                      >
                        <SelectValue placeholder="Select an organization..." />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="__clear__">Select an organization...</SelectItem>
                        {tenants.map((tenant) => (
                          <SelectItem key={tenant.tenantId} value={tenant.tenantId}>
                            {tenant.name} {tenant.environment !== 'Prod' ? `(${tenant.environment})` : ''}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                  <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
                    Choose the organization you want to sign in to.
                  </p>
                </div>
              )}

              {/* Provider info */}
              <div className="p-3 mb-6 bg-[var(--color-brand-primary-light)] rounded-md border border-[var(--color-brand-primary)]/20">
                <p className="text-sm text-[var(--color-brand-primary)] text-center">
                  Signing in with {provider === 'azure-ad' ? 'Microsoft Entra ID' : 'Auth0'}
                </p>
              </div>

              {/* Sign in button */}
              <Button 
                onClick={handleLogin}
                className="w-full py-3 text-base"
                disabled={isLoggingIn || (showTenantSelector && isLoadingTenants)}
              >
                {isLoggingIn ? (
                  <>
                    <svg className="animate-spin w-5 h-5" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    Redirecting...
                  </>
                ) : (
                  <>
                    {provider === 'azure-ad' ? (
                      <svg className="w-5 h-5" viewBox="0 0 23 23">
                        <path fill="currentColor" d="M0 0h11v11H0zM12 0h11v11H12zM0 12h11v11H0zM12 12h11v11H12z"/>
                      </svg>
                    ) : (
                      <svg className="w-5 h-5" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M21.98 7.448L19.62 0H4.347L2.02 7.448c-1.352 4.312.03 9.206 3.815 12.015L12.007 24l6.157-4.552c3.755-2.81 5.182-7.688 3.815-12.015l-6.16 4.58 2.343 7.45-6.157-4.597-6.158 4.58 2.358-7.433-6.188-4.55 7.63-.045L12.008 0l2.356 7.404 7.615.044z"/>
                      </svg>
                    )}
                    Sign in with {provider === 'azure-ad' ? 'Microsoft' : 'Auth0'}
                    <ArrowRight className="w-5 h-5" />
                  </>
                )}
              </Button>

              {/* Alternative provider hint */}
              <p className="mt-6 text-center text-sm text-[var(--color-text-tertiary)]">
                Using a different identity provider?{' '}
                <a 
                  href="#" 
                  onClick={(e) => {
                    e.preventDefault();
                    alert(`To switch providers, set VITE_AUTH_PROVIDER to "${provider === 'azure-ad' ? 'auth0' : 'azure-ad'}" in your .env file`);
                  }}
                  className="text-[var(--color-brand-primary)] hover:underline"
                >
                  Learn more
                </a>
              </p>
            </CardContent>
          </Card>

          {/* Footer */}
          <p className="mt-8 text-center text-xs text-[var(--color-text-tertiary)]">
            By signing in, you agree to our{' '}
            <a href="#" className="underline hover:text-[var(--color-text-secondary)]">Terms of Service</a>
            {' '}and{' '}
            <a href="#" className="underline hover:text-[var(--color-text-secondary)]">Privacy Policy</a>
          </p>
        </div>
      </div>
    </div>
  );
}
