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

const iconStyles: Record<string, { icon: string; bg: string; border: string }> = {
  configuration: {
    icon: 'text-[var(--color-warning)]',
    bg: 'bg-[color-mix(in_srgb,var(--color-warning)_10%,transparent)]',
    border: 'border-[color-mix(in_srgb,var(--color-warning)_30%,transparent)]',
  },
  network: {
    icon: 'text-[var(--color-info)]',
    bg: 'bg-[color-mix(in_srgb,var(--color-info)_10%,transparent)]',
    border: 'border-[color-mix(in_srgb,var(--color-info)_30%,transparent)]',
  },
  provider: {
    icon: 'text-[var(--color-danger)]',
    bg: 'bg-[color-mix(in_srgb,var(--color-danger)_10%,transparent)]',
    border: 'border-[color-mix(in_srgb,var(--color-danger)_30%,transparent)]',
  },
  unknown: {
    icon: 'text-[var(--color-danger)]',
    bg: 'bg-[color-mix(in_srgb,var(--color-danger)_10%,transparent)]',
    border: 'border-[color-mix(in_srgb,var(--color-danger)_30%,transparent)]',
  },
};

export function AuthError({ error, onRetry }: AuthErrorProps) {
  const style = iconStyles[error.type] ?? iconStyles.unknown;

  const getIcon = () => {
    switch (error.type) {
      case 'configuration':
        return <Settings className={`w-12 h-12 ${style.icon}`} />;
      case 'network':
        return <RefreshCw className={`w-12 h-12 ${style.icon}`} />;
      default:
        return <AlertCircle className={`w-12 h-12 ${style.icon}`} />;
    }
  };

  return (
    <div className="flex items-center justify-center min-h-screen bg-[var(--color-gray-100)] p-6">
      <Card className="max-w-[500px] w-full border-none shadow-lg">
        <CardContent className="p-8">
          {/* Icon */}
          <div className="flex justify-center mb-6">
            <div className={`w-20 h-20 rounded-full ${style.bg} border-2 ${style.border} flex items-center justify-center`}>
              {getIcon()}
            </div>
          </div>

          {/* Title */}
          <h1 className="text-2xl font-bold text-[var(--color-text-heading)] text-center mb-2">
            {error.title}
          </h1>

          {/* Message */}
          <p className="text-[15px] text-[var(--color-text-secondary)] text-center mb-6 leading-relaxed">
            {error.message}
          </p>

          {/* Details box */}
          {error.details && (
            <div className="bg-[var(--color-gray-100)] rounded-lg p-4 mb-6 font-mono text-[13px] text-[var(--color-gray-600)] overflow-x-auto whitespace-pre-wrap break-words">
              {error.details}
            </div>
          )}

          {/* Configuration help for config errors */}
          {error.type === 'configuration' && (
            <div className="bg-[color-mix(in_srgb,var(--color-brand-primary)_8%,transparent)] border border-[color-mix(in_srgb,var(--color-brand-primary)_25%,transparent)] rounded-lg p-4 mb-6">
              <p className="text-sm font-semibold text-[var(--color-brand-primary)] mb-2">
                How to fix this:
              </p>
              <ol className="text-[13px] text-[var(--color-brand-primary)] m-0 pl-5 leading-[1.8]">
                <li>Copy <code className="bg-[color-mix(in_srgb,var(--color-brand-primary)_12%,transparent)] px-1.5 py-0.5 rounded">.env.example</code> to <code className="bg-[color-mix(in_srgb,var(--color-brand-primary)_12%,transparent)] px-1.5 py-0.5 rounded">.env.local</code></li>
                <li>Fill in your {error.provider || 'identity provider'} credentials</li>
                <li>Restart the development server</li>
              </ol>
            </div>
          )}

          {/* Actions */}
          <div className="flex flex-col gap-3">
            {onRetry && (
              <Button 
                onClick={onRetry}
                className="w-full py-3 px-4"
              >
                <RefreshCw className="w-[18px] h-[18px] mr-2" />
                Try Again
              </Button>
            )}
            
            {error.type === 'configuration' && (
              <a
                href="https://github.com/michaeljosiah/aonik#authentication-setup"
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center justify-center gap-2 py-3 px-4 rounded-lg border border-[var(--color-gray-200)] bg-[var(--color-surface)] text-[var(--color-text-heading)] no-underline text-sm font-medium hover:bg-[var(--color-gray-50)] transition-colors"
              >
                <ExternalLink className="w-[18px] h-[18px]" />
                View Documentation
              </a>
            )}
          </div>

          {/* Footer */}
          <p className="mt-6 text-center text-xs text-[var(--color-text-muted)]">
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
