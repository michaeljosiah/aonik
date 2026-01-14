import { AlertCircle, RefreshCw, Settings, ExternalLink } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

export interface AuthErrorInfo {
  type: 'configuration' | 'provider' | 'network' | 'unknown';
  title: string;
  message: string;
  details?: string;
  provider?: string;
}

interface AuthErrorProps {
  error: AuthErrorInfo;
  onRetry?: () => void;
}

export function AuthError({ error, onRetry }: AuthErrorProps) {
  const getIcon = () => {
    switch (error.type) {
      case 'configuration':
        return <Settings className="w-12 h-12 text-amber-500" />;
      case 'network':
        return <RefreshCw className="w-12 h-12 text-blue-500" />;
      default:
        return <AlertCircle className="w-12 h-12 text-red-500" />;
    }
  };

  const getBackgroundColor = () => {
    switch (error.type) {
      case 'configuration':
        return '#FFFBEB'; // amber-50
      case 'network':
        return '#EFF6FF'; // blue-50
      default:
        return '#FEF2F2'; // red-50
    }
  };

  const getBorderColor = () => {
    switch (error.type) {
      case 'configuration':
        return '#FDE68A'; // amber-200
      case 'network':
        return '#BFDBFE'; // blue-200
      default:
        return '#FECACA'; // red-200
    }
  };

  return (
    <div 
      style={{ 
        display: 'flex', 
        alignItems: 'center', 
        justifyContent: 'center', 
        minHeight: '100vh', 
        backgroundColor: '#F8F9FA',
        padding: '24px'
      }}
    >
      <Card style={{ maxWidth: '500px', width: '100%', border: 'none', boxShadow: '0 10px 15px -3px rgb(0 0 0 / 0.1)' }}>
        <CardContent style={{ padding: '32px' }}>
          {/* Icon */}
          <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '24px' }}>
            <div 
              style={{ 
                width: '80px', 
                height: '80px', 
                borderRadius: '50%', 
                backgroundColor: getBackgroundColor(),
                border: `2px solid ${getBorderColor()}`,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center'
              }}
            >
              {getIcon()}
            </div>
          </div>

          {/* Title */}
          <h1 style={{ 
            fontSize: '24px', 
            fontWeight: 'bold', 
            color: '#1A1A1A', 
            textAlign: 'center',
            marginBottom: '8px'
          }}>
            {error.title}
          </h1>

          {/* Message */}
          <p style={{ 
            fontSize: '15px', 
            color: '#6B7280', 
            textAlign: 'center',
            marginBottom: '24px',
            lineHeight: 1.6
          }}>
            {error.message}
          </p>

          {/* Details box */}
          {error.details && (
            <div 
              style={{ 
                backgroundColor: '#F3F4F6',
                borderRadius: '8px',
                padding: '16px',
                marginBottom: '24px',
                fontFamily: 'monospace',
                fontSize: '13px',
                color: '#4B5563',
                overflowX: 'auto',
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-word'
              }}
            >
              {error.details}
            </div>
          )}

          {/* Configuration help for config errors */}
          {error.type === 'configuration' && (
            <div 
              style={{ 
                backgroundColor: '#F0FDFA',
                border: '1px solid #99F6E4',
                borderRadius: '8px',
                padding: '16px',
                marginBottom: '24px'
              }}
            >
              <p style={{ fontSize: '14px', fontWeight: '600', color: '#0F766E', marginBottom: '8px' }}>
                How to fix this:
              </p>
              <ol style={{ fontSize: '13px', color: '#0F766E', margin: 0, paddingLeft: '20px', lineHeight: 1.8 }}>
                <li>Copy <code style={{ backgroundColor: '#CCFBF1', padding: '2px 6px', borderRadius: '4px' }}>.env.example</code> to <code style={{ backgroundColor: '#CCFBF1', padding: '2px 6px', borderRadius: '4px' }}>.env.local</code></li>
                <li>Fill in your {error.provider || 'identity provider'} credentials</li>
                <li>Restart the development server</li>
              </ol>
            </div>
          )}

          {/* Actions */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {onRetry && (
              <Button 
                onClick={onRetry}
                style={{ width: '100%', padding: '12px 16px' }}
              >
                <RefreshCw style={{ width: '18px', height: '18px', marginRight: '8px' }} />
                Try Again
              </Button>
            )}
            
            {error.type === 'configuration' && (
              <a
                href="https://github.com/michaeljosiah/aonik#authentication-setup"
                target="_blank"
                rel="noopener noreferrer"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: '8px',
                  padding: '12px 16px',
                  borderRadius: '8px',
                  border: '1px solid #E5E7EB',
                  backgroundColor: 'white',
                  color: '#1A1A1A',
                  textDecoration: 'none',
                  fontSize: '14px',
                  fontWeight: '500'
                }}
              >
                <ExternalLink style={{ width: '18px', height: '18px' }} />
                View Documentation
              </a>
            )}
          </div>

          {/* Footer */}
          <p style={{ 
            marginTop: '24px', 
            textAlign: 'center', 
            fontSize: '12px', 
            color: '#9CA3AF' 
          }}>
            If this problem persists, please contact your administrator.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}

// Helper to create common error types
export const AuthErrors = {
  missingConfig: (provider: string, missingFields: string[]): AuthErrorInfo => ({
    type: 'configuration',
    title: 'Authentication Not Configured',
    message: `The ${provider} authentication provider is not properly configured. Please set up the required environment variables.`,
    details: `Missing configuration:\n${missingFields.map(f => `  - ${f}`).join('\n')}`,
    provider,
  }),

  invalidProvider: (provider: string): AuthErrorInfo => ({
    type: 'configuration',
    title: 'Invalid Auth Provider',
    message: `The authentication provider "${provider}" is not recognized. Please use "azure-ad" or "auth0".`,
    details: `VITE_AUTH_PROVIDER="${provider}"\n\nValid options:\n  - azure-ad\n  - auth0`,
  }),

  initializationFailed: (provider: string, errorMessage: string): AuthErrorInfo => ({
    type: 'provider',
    title: 'Authentication Failed to Initialize',
    message: `There was a problem starting the ${provider} authentication service.`,
    details: errorMessage,
    provider,
  }),

  networkError: (): AuthErrorInfo => ({
    type: 'network',
    title: 'Connection Problem',
    message: 'Unable to connect to the authentication service. Please check your internet connection and try again.',
  }),

  unknown: (errorMessage?: string): AuthErrorInfo => ({
    type: 'unknown',
    title: 'Something Went Wrong',
    message: 'An unexpected error occurred during authentication.',
    details: errorMessage,
  }),
};
