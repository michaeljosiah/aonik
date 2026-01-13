import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Eye, EyeOff, Mail, Lock, ArrowRight } from 'lucide-react';

export function LoginPage() {
  const navigate = useNavigate();
  const [showPassword, setShowPassword] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    
    // Simulate login delay
    await new Promise(resolve => setTimeout(resolve, 1000));
    
    // For demo purposes, just navigate to dashboard
    navigate('/');
  };

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

      {/* Right side - Login Form */}
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

              {/* Form */}
              <form onSubmit={handleSubmit}>
                {/* Email field */}
                <div style={{ marginBottom: '20px' }}>
                  <label 
                    htmlFor="email" 
                    style={{ display: 'block', fontSize: '14px', fontWeight: '500', color: '#1A1A1A', marginBottom: '6px' }}
                  >
                    Email address
                  </label>
                  <div style={{ position: 'relative' }}>
                    <div style={{ position: 'absolute', top: '50%', left: '12px', transform: 'translateY(-50%)', pointerEvents: 'none' }}>
                      <Mail style={{ width: '20px', height: '20px', color: '#9CA3AF' }} />
                    </div>
                    <input
                      id="email"
                      type="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      placeholder="you@company.com"
                      style={{
                        display: 'block',
                        width: '100%',
                        paddingLeft: '44px',
                        paddingRight: '16px',
                        paddingTop: '12px',
                        paddingBottom: '12px',
                        borderRadius: '8px',
                        border: '1px solid #E5E7EB',
                        backgroundColor: 'white',
                        color: '#1A1A1A',
                        fontSize: '14px',
                        outline: 'none',
                        boxSizing: 'border-box'
                      }}
                      required
                    />
                  </div>
                </div>

                {/* Password field */}
                <div style={{ marginBottom: '20px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '6px' }}>
                    <label 
                      htmlFor="password" 
                      style={{ fontSize: '14px', fontWeight: '500', color: '#1A1A1A' }}
                    >
                      Password
                    </label>
                    <a 
                      href="#" 
                      style={{ fontSize: '14px', color: '#0D7377', textDecoration: 'none' }}
                    >
                      Forgot password?
                    </a>
                  </div>
                  <div style={{ position: 'relative' }}>
                    <div style={{ position: 'absolute', top: '50%', left: '12px', transform: 'translateY(-50%)', pointerEvents: 'none' }}>
                      <Lock style={{ width: '20px', height: '20px', color: '#9CA3AF' }} />
                    </div>
                    <input
                      id="password"
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      placeholder="Enter your password"
                      style={{
                        display: 'block',
                        width: '100%',
                        paddingLeft: '44px',
                        paddingRight: '48px',
                        paddingTop: '12px',
                        paddingBottom: '12px',
                        borderRadius: '8px',
                        border: '1px solid #E5E7EB',
                        backgroundColor: 'white',
                        color: '#1A1A1A',
                        fontSize: '14px',
                        outline: 'none',
                        boxSizing: 'border-box'
                      }}
                      required
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      style={{ 
                        position: 'absolute', 
                        top: '50%', 
                        right: '12px', 
                        transform: 'translateY(-50%)',
                        background: 'none',
                        border: 'none',
                        cursor: 'pointer',
                        padding: 0,
                        color: '#9CA3AF'
                      }}
                    >
                      {showPassword ? (
                        <EyeOff style={{ width: '20px', height: '20px' }} />
                      ) : (
                        <Eye style={{ width: '20px', height: '20px' }} />
                      )}
                    </button>
                  </div>
                </div>

                {/* Remember me */}
                <div style={{ display: 'flex', alignItems: 'center', marginBottom: '24px' }}>
                  <input
                    id="remember"
                    type="checkbox"
                    style={{ width: '16px', height: '16px', cursor: 'pointer' }}
                  />
                  <label 
                    htmlFor="remember" 
                    style={{ marginLeft: '8px', fontSize: '14px', color: '#6B7280', cursor: 'pointer' }}
                  >
                    Remember me for 30 days
                  </label>
                </div>

                {/* Submit button */}
                <Button 
                  type="submit" 
                  style={{ width: '100%', padding: '12px 16px', fontSize: '16px', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '8px' }}
                  disabled={isLoading}
                >
                  {isLoading ? (
                    <>
                      <svg style={{ animation: 'spin 1s linear infinite', width: '20px', height: '20px' }} xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                        <circle style={{ opacity: 0.25 }} cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                        <path style={{ opacity: 0.75 }} fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                      </svg>
                      Signing in...
                    </>
                  ) : (
                    <>
                      Sign in
                      <ArrowRight style={{ width: '20px', height: '20px' }} />
                    </>
                  )}
                </Button>
              </form>

              {/* Divider */}
              <div style={{ position: 'relative', margin: '24px 0' }}>
                <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center' }}>
                  <div style={{ width: '100%', borderTop: '1px solid #E5E7EB' }}></div>
                </div>
                <div style={{ position: 'relative', display: 'flex', justifyContent: 'center' }}>
                  <span style={{ padding: '0 16px', backgroundColor: 'white', color: '#9CA3AF', fontSize: '14px' }}>
                    Or continue with
                  </span>
                </div>
              </div>

              {/* Social login buttons */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <button 
                  type="button"
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '8px',
                    padding: '10px 16px',
                    borderRadius: '8px',
                    border: '1px solid #E5E7EB',
                    backgroundColor: 'white',
                    color: '#1A1A1A',
                    cursor: 'pointer',
                    fontSize: '14px',
                    fontWeight: '500'
                  }}
                >
                  <svg style={{ width: '20px', height: '20px' }} viewBox="0 0 24 24">
                    <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
                    <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
                    <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
                    <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
                  </svg>
                  Google
                </button>
                <button 
                  type="button"
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '8px',
                    padding: '10px 16px',
                    borderRadius: '8px',
                    border: '1px solid #E5E7EB',
                    backgroundColor: 'white',
                    color: '#1A1A1A',
                    cursor: 'pointer',
                    fontSize: '14px',
                    fontWeight: '500'
                  }}
                >
                  <svg style={{ width: '20px', height: '20px' }} fill="currentColor" viewBox="0 0 24 24">
                    <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"/>
                  </svg>
                  GitHub
                </button>
              </div>

              {/* Sign up link */}
              <p style={{ marginTop: '24px', textAlign: 'center', fontSize: '14px', color: '#6B7280' }}>
                Don't have an account?{' '}
                <a 
                  href="#" 
                  style={{ fontWeight: '500', color: '#0D7377', textDecoration: 'none' }}
                >
                  Request access
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

      {/* Add keyframes for spinner animation */}
      <style>{`
        @keyframes spin {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
}
