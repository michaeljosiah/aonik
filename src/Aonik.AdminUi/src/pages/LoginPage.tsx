import { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { ArrowRight, AlertCircle } from 'lucide-react';
import { useAuth, getAuthProvider } from '@/auth';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated, isLoading, login } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const [isLoggingIn, setIsLoggingIn] = useState(false);

  const provider = getAuthProvider();
  const from = (location.state as { from?: { pathname: string } })?.from?.pathname || '/';

  // Redirect if already authenticated
  useEffect(() => {
    if (isAuthenticated && !isLoading) {
      navigate(from, { replace: true });
    }
  }, [isAuthenticated, isLoading, navigate, from]);

  const handleLogin = async () => {
    setError(null);
    setIsLoggingIn(true);
    try {
      await login();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to sign in. Please try again.');
      setIsLoggingIn(false);
    }
  };

  // Show loading while checking auth state
  if (isLoading) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '100vh', backgroundColor: '#F8F9FA' }}>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '16px' }}>
          <div style={{ width: '32px', height: '32px', border: '4px solid #0D7377', borderTopColor: 'transparent', borderRadius: '50%', animation: 'spin 1s linear infinite' }} />
          <p style={{ fontSize: '14px', color: '#6B7280' }}>Loading...</p>
        </div>
        <style>{`
          @keyframes spin {
            from { transform: rotate(0deg); }
            to { transform: rotate(360deg); }
          }
        `}</style>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', minHeight: '100vh', width: '100%' }}>
      {/* Left side - Branding */}
      <div 
        style={{ 
          display: 'none',
          width: '50%',
          backgroundColor: '#0D7377',
          position: 'relative',
          overflow: 'hidden',
          padding: '64px'
        }}
        className="lg:!flex lg:!flex-col lg:!justify-center"
      >
        {/* Decorative circles */}
        <div style={{ position: 'absolute', top: '-128px', left: '-128px', width: '384px', height: '384px', borderRadius: '50%', backgroundColor: 'rgba(255,255,255,0.05)' }} />
        <div style={{ position: 'absolute', top: '25%', right: '0', width: '256px', height: '256px', borderRadius: '50%', backgroundColor: 'rgba(255,255,255,0.05)' }} />
        <div style={{ position: 'absolute', bottom: '0', left: '25%', width: '320px', height: '320px', borderRadius: '50%', backgroundColor: 'rgba(255,255,255,0.05)' }} />
        
        {/* Accent dots */}
        <div style={{ position: 'absolute', top: '80px', right: '80px', width: '16px', height: '16px', borderRadius: '50%', backgroundColor: '#E8A838' }} />
        <div style={{ position: 'absolute', bottom: '160px', left: '80px', width: '12px', height: '12px', borderRadius: '50%', backgroundColor: '#E8A838' }} />

        {/* Content */}
        <div style={{ position: 'relative', zIndex: 10, color: 'white' }}>
          {/* Logo */}
          <div style={{ marginBottom: '48px' }}>
            <h1 style={{ fontSize: '36px', fontWeight: 'bold', margin: 0 }}>
              Aonik<span style={{ color: '#E8A838' }}>.</span>
            </h1>
          </div>

          {/* Tagline */}
          <h2 style={{ fontSize: '30px', fontWeight: '600', marginBottom: '16px', lineHeight: 1.2 }}>
            AI-native financial<br />infrastructure
          </h2>
          <p style={{ fontSize: '18px', color: 'rgba(255,255,255,0.8)', marginBottom: '32px', maxWidth: '400px' }}>
            Power modern payments, billing, and financial intelligence with AI agents that assist with reconciliation, forecasting, and insights.
          </p>

          {/* Feature highlights */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <div style={{ width: '40px', height: '40px', borderRadius: '8px', backgroundColor: 'rgba(255,255,255,0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <svg style={{ width: '20px', height: '20px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
              </div>
              <span style={{ color: 'rgba(255,255,255,0.9)' }}>Intelligent reconciliation & anomaly detection</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <div style={{ width: '40px', height: '40px', borderRadius: '8px', backgroundColor: 'rgba(255,255,255,0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <svg style={{ width: '20px', height: '20px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                </svg>
              </div>
              <span style={{ color: 'rgba(255,255,255,0.9)' }}>Cash flow forecasting & spend insights</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <div style={{ width: '40px', height: '40px', borderRadius: '8px', backgroundColor: 'rgba(255,255,255,0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <svg style={{ width: '20px', height: '20px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                </svg>
              </div>
              <span style={{ color: 'rgba(255,255,255,0.9)' }}>Explainable, auditable AI you can trust</span>
            </div>
          </div>
        </div>
      </div>

      {/* Right side - Login */}
      <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '32px', backgroundColor: '#F8F9FA' }}>
        <div style={{ width: '100%', maxWidth: '420px' }}>
          {/* Mobile logo */}
          <div className="lg:hidden" style={{ textAlign: 'center', marginBottom: '32px' }}>
            <h1 style={{ fontSize: '28px', fontWeight: 'bold', color: '#1A1A1A', margin: 0 }}>
              Aonik<span style={{ color: '#E8A838' }}>.</span>
            </h1>
          </div>

          <Card style={{ boxShadow: '0 10px 15px -3px rgb(0 0 0 / 0.1)', border: 'none' }}>
            <CardContent style={{ padding: '32px' }}>
              {/* Header */}
              <div style={{ textAlign: 'center', marginBottom: '32px' }}>
                <h2 style={{ fontSize: '24px', fontWeight: 'bold', color: '#1A1A1A', marginBottom: '8px' }}>
                  Welcome back
                </h2>
                <p style={{ color: '#6B7280', margin: 0 }}>
                  Sign in to continue to your workspace
                </p>
              </div>

              {/* Error message */}
              {error && (
                <div style={{ 
                  display: 'flex', 
                  alignItems: 'center', 
                  gap: '8px', 
                  padding: '12px', 
                  marginBottom: '24px',
                  backgroundColor: '#FEF2F2', 
                  borderRadius: '8px',
                  border: '1px solid #FECACA'
                }}>
                  <AlertCircle style={{ width: '20px', height: '20px', color: '#EF4444', flexShrink: 0 }} />
                  <p style={{ fontSize: '14px', color: '#991B1B', margin: 0 }}>{error}</p>
                </div>
              )}

              {/* Provider info */}
              <div style={{ 
                padding: '12px 16px', 
                marginBottom: '24px',
                backgroundColor: '#F0FDFA', 
                borderRadius: '8px',
                border: '1px solid #99F6E4'
              }}>
                <p style={{ fontSize: '13px', color: '#0F766E', margin: 0, textAlign: 'center' }}>
                  Signing in with {provider === 'azure-ad' ? 'Microsoft Entra ID' : 'Auth0'}
                </p>
              </div>

              {/* Sign in button */}
              <Button 
                onClick={handleLogin}
                style={{ 
                  width: '100%', 
                  padding: '14px 16px', 
                  fontSize: '16px', 
                  display: 'flex', 
                  alignItems: 'center', 
                  justifyContent: 'center', 
                  gap: '8px' 
                }}
                disabled={isLoggingIn}
              >
                {isLoggingIn ? (
                  <>
                    <svg style={{ animation: 'spin 1s linear infinite', width: '20px', height: '20px' }} xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                      <circle style={{ opacity: 0.25 }} cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                      <path style={{ opacity: 0.75 }} fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    Redirecting...
                  </>
                ) : (
                  <>
                    {provider === 'azure-ad' ? (
                      <svg style={{ width: '20px', height: '20px' }} viewBox="0 0 23 23">
                        <path fill="currentColor" d="M0 0h11v11H0zM12 0h11v11H12zM0 12h11v11H0zM12 12h11v11H12z"/>
                      </svg>
                    ) : (
                      <svg style={{ width: '20px', height: '20px' }} viewBox="0 0 24 24" fill="currentColor">
                        <path d="M21.98 7.448L19.62 0H4.347L2.02 7.448c-1.352 4.312.03 9.206 3.815 12.015L12.007 24l6.157-4.552c3.755-2.81 5.182-7.688 3.815-12.015l-6.16 4.58 2.343 7.45-6.157-4.597-6.158 4.58 2.358-7.433-6.188-4.55 7.63-.045L12.008 0l2.356 7.404 7.615.044z"/>
                      </svg>
                    )}
                    Sign in with {provider === 'azure-ad' ? 'Microsoft' : 'Auth0'}
                    <ArrowRight style={{ width: '20px', height: '20px' }} />
                  </>
                )}
              </Button>

              {/* Alternative provider hint */}
              <p style={{ marginTop: '24px', textAlign: 'center', fontSize: '13px', color: '#9CA3AF' }}>
                Using a different identity provider?{' '}
                <a 
                  href="#" 
                  onClick={(e) => {
                    e.preventDefault();
                    alert(`To switch providers, set VITE_AUTH_PROVIDER to "${provider === 'azure-ad' ? 'auth0' : 'azure-ad'}" in your .env file`);
                  }}
                  style={{ color: '#0D7377', textDecoration: 'none' }}
                >
                  Learn more
                </a>
              </p>
            </CardContent>
          </Card>

          {/* Footer */}
          <p style={{ marginTop: '32px', textAlign: 'center', fontSize: '12px', color: '#9CA3AF' }}>
            By signing in, you agree to our{' '}
            <a href="#" style={{ textDecoration: 'underline' }}>Terms of Service</a>
            {' '}and{' '}
            <a href="#" style={{ textDecoration: 'underline' }}>Privacy Policy</a>
          </p>
        </div>
      </div>

      {/* Keyframes */}
      <style>{`
        @keyframes spin {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
}
