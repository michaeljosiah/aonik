// SettingsCommunicationPage — Spec-029-style viewer for the outbound
// messaging configuration. Email and SMS are independent channels:
// each can run on its own provider (today only Azure Communication
// Services is wired, but the page is shaped so SendGrid for email +
// Twilio for SMS slot in without rework).
//
//   • Two channel cards (Email, SMS), each owning its own active
//     provider, masked credential, and live health badge
//   • Test-send panel lets the operator hit either channel with a
//     one-off dispatch and see the result inline
//   • Writes are not currently supported — the backend update endpoint
//     returns 400 with a "configuration-managed; use environment
//     variables" message that we surface verbatim

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  Loader2,
  Mail,
  MessageSquare,
  RefreshCw,
  Send,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { communicationProviderSettingsService } from '@/services/communicationProviderSettingsService';
import { messagingService } from '@/services/messagingService';
import { cn } from '@/lib/utils';
import type {
  CommunicationProviderSettingsResponse,
  MessagingChannelHealth,
  MessagingHealth,
  SendCommunicationTestResponse,
} from '@/types';

function resolveUserMessage(error: unknown, fallback: string): string {
  const message =
    error && typeof error === 'object' && 'userMessage' in error
      ? String((error as { userMessage?: string }).userMessage ?? '')
      : '';
  return message || fallback;
}

function HealthBadge({ health }: { health: MessagingChannelHealth | undefined }) {
  if (!health) {
    return (
      <Badge variant="outline" className="gap-1">
        <Loader2 className="h-3 w-3 animate-spin" /> Checking
      </Badge>
    );
  }
  if (health.configured) {
    return (
      <Badge className="gap-1 bg-[var(--color-success-light)] text-[var(--color-success)] border-[var(--color-success)]">
        <CheckCircle2 className="h-3 w-3" /> Configured · {health.provider}
      </Badge>
    );
  }
  return (
    <Badge
      variant="outline"
      className="gap-1 border-[var(--color-warning)] bg-[var(--color-warning-light)] text-[var(--color-warning)]"
    >
      <AlertTriangle className="h-3 w-3" /> Not configured
    </Badge>
  );
}

function ProviderRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-2">
      <span className="text-xs text-[var(--color-text-tertiary)]">{label}</span>
      <Badge variant="outline" className="font-mono text-[11px]">
        {value}
      </Badge>
    </div>
  );
}

function ReadOnlyRow({
  label,
  keyName,
  value,
  monospace = false,
}: {
  label: string;
  keyName: string;
  value: string;
  monospace?: boolean;
}) {
  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between gap-3">
        <Label>{label}</Label>
        <span className="font-mono text-[11px] text-[var(--color-text-tertiary)]">{keyName}</span>
      </div>
      <Input
        readOnly
        value={value}
        className={cn(
          'cursor-not-allowed bg-[var(--color-surface-inset)]',
          monospace && 'font-mono text-[12px]',
        )}
      />
    </div>
  );
}

export function SettingsCommunicationPage() {
  const [settings, setSettings] = useState<CommunicationProviderSettingsResponse | null>(null);
  const [health, setHealth] = useState<MessagingHealth | null>(null);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Test-send state — kept simple, a single panel that switches
  // between Email / SMS rather than two parallel forms.
  const [testChannel, setTestChannel] = useState<'Email' | 'SMS'>('Email');
  const [testRecipient, setTestRecipient] = useState('');
  const [testBusy, setTestBusy] = useState(false);
  const [testResult, setTestResult] = useState<SendCommunicationTestResponse | null>(null);

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    setError(null);
    try {
      const [settingsResult, healthResult] = await Promise.all([
        communicationProviderSettingsService.get(),
        messagingService.health(),
      ]);
      setSettings(settingsResult);
      setHealth(healthResult);
    } catch (err: unknown) {
      setError(resolveUserMessage(err, 'Failed to load communication settings.'));
    } finally {
      setLoading(false);
      setInitialLoad(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const handleRefresh = async () => {
    setRefreshing(true);
    await load(true);
  };

  const channelHealth = useMemo(() => {
    if (!health) return undefined;
    return testChannel === 'Email' ? health.email : health.sms;
  }, [health, testChannel]);

  const handleSendTest = async () => {
    if (!testRecipient.trim() || testBusy) return;
    setTestBusy(true);
    setTestResult(null);
    try {
      const result = await communicationProviderSettingsService.sendTest({
        channel: testChannel,
        recipient: testRecipient.trim(),
        subject: testChannel === 'Email' ? 'Aonik test email' : null,
        body: null,
      });
      setTestResult(result);
      if (result.sent) {
        toast.success(`${testChannel} test sent successfully.`);
      } else {
        toast.error(`${testChannel} test failed: ${result.errorMessage ?? 'Unknown error.'}`);
      }
    } catch (err: unknown) {
      const message = resolveUserMessage(err, 'Failed to send test message.');
      setTestResult({
        sent: false,
        channel: testChannel,
        provider: 'Unknown',
        errorMessage: message,
      });
      toast.error(message);
    } finally {
      setTestBusy(false);
    }
  };

  if (initialLoad) {
    return <PageLoadingScreen message="Loading communication settings" />;
  }

  const emailAzure = settings?.email.azureCommunicationServices ?? null;
  const smsAzure = settings?.sms.azureCommunicationServices ?? null;

  return (
    <div className="h-full overflow-auto px-8 py-6">
      {/* Page header */}
      <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">
            Settings · Platform
          </p>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Communication</h1>
          <p className="max-w-3xl text-[var(--color-text-secondary)]">
            Outbound email and SMS configuration for invitations, notifications, and verification
            flows. Email and SMS are configured independently — each can run on its own provider.
            Settings are managed via environment variables; this page shows the current state and
            lets you verify it with a test send.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={handleRefresh} disabled={refreshing || loading}>
          <RefreshCw className={cn('h-3.5 w-3.5', refreshing && 'animate-spin')} />
          Refresh
        </Button>
      </div>

      {/* Error */}
      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="flex items-center gap-3 p-4 text-[var(--color-error)]">
            <AlertCircle className="h-5 w-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={handleRefresh} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Email channel */}
      {settings && (
        <Card className="mb-4">
          <CardHeader>
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-start gap-2">
                <Mail className="mt-0.5 h-4 w-4 text-[var(--color-text-secondary)]" />
                <div>
                  <CardTitle className="text-base">Email</CardTitle>
                  <CardDescription className="mt-1">
                    Used for invite emails, password reset, and other transactional messages.
                  </CardDescription>
                </div>
              </div>
              <HealthBadge health={health?.email} />
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <ProviderRow label="Active provider" value={settings.email.activeProvider} />
            {emailAzure && settings.email.activeProvider === 'AzureCommunicationServices' && (
              <>
                <ReadOnlyRow
                  label="Connection string"
                  keyName="Communication:Email:AzureCommunicationServices:ConnectionString"
                  value={
                    emailAzure.hasConnectionString
                      ? '••••••••••••••••••••  (configured)'
                      : '(not set — outbound email will fail)'
                  }
                  monospace
                />
                <ReadOnlyRow
                  label="From address"
                  keyName="Communication:Email:AzureCommunicationServices:FromAddress"
                  value={emailAzure.fromAddress ?? '(not set)'}
                />
              </>
            )}
            {health?.email && !health.email.configured && (
              <div className="flex items-start gap-2 rounded border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-3 py-2 text-xs text-[var(--color-warning)]">
                <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                <span>{health.email.reason ?? 'Email provider is not configured.'}</span>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* SMS channel */}
      {settings && (
        <Card className="mb-4">
          <CardHeader>
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-start gap-2">
                <MessageSquare className="mt-0.5 h-4 w-4 text-[var(--color-text-secondary)]" />
                <div>
                  <CardTitle className="text-base">SMS</CardTitle>
                  <CardDescription className="mt-1">
                    Used for phone verification OTPs and any future SMS notifications.
                  </CardDescription>
                </div>
              </div>
              <HealthBadge health={health?.sms} />
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <ProviderRow label="Active provider" value={settings.sms.activeProvider} />
            {smsAzure && settings.sms.activeProvider === 'AzureCommunicationServices' && (
              <>
                <ReadOnlyRow
                  label="Connection string"
                  keyName="Communication:Sms:AzureCommunicationServices:ConnectionString"
                  value={
                    smsAzure.hasConnectionString
                      ? '••••••••••••••••••••  (configured)'
                      : '(not set — outbound SMS will fail)'
                  }
                  monospace
                />
                <ReadOnlyRow
                  label="From phone number"
                  keyName="Communication:Sms:AzureCommunicationServices:FromPhoneNumber"
                  value={smsAzure.fromPhoneNumber ?? '(not set)'}
                />
              </>
            )}
            {health?.sms && !health.sms.configured && (
              <div className="flex items-start gap-2 rounded border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-3 py-2 text-xs text-[var(--color-warning)]">
                <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                <span>{health.sms.reason ?? 'SMS provider is not configured.'}</span>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Test send */}
      <Card className="mb-4">
        <CardHeader>
          <CardTitle className="text-base">Send a test message</CardTitle>
          <CardDescription className="mt-1">
            Dispatches a one-off message via the active provider for the selected channel.
            Result appears below — the request succeeds even on delivery failure so the error
            is rendered inline.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex gap-2">
            <Button
              variant={testChannel === 'Email' ? 'default' : 'outline'}
              size="sm"
              onClick={() => setTestChannel('Email')}
              disabled={testBusy}
            >
              <Mail className="h-3.5 w-3.5" /> Email
            </Button>
            <Button
              variant={testChannel === 'SMS' ? 'default' : 'outline'}
              size="sm"
              onClick={() => setTestChannel('SMS')}
              disabled={testBusy}
            >
              <MessageSquare className="h-3.5 w-3.5" /> SMS
            </Button>
            {channelHealth && !channelHealth.configured && (
              <span className="ml-2 self-center text-xs text-[var(--color-warning)]">
                Channel not configured — test will fail.
              </span>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="test-recipient">
              {testChannel === 'Email' ? 'Recipient email' : 'Recipient phone (E.164)'}
            </Label>
            <Input
              id="test-recipient"
              type={testChannel === 'Email' ? 'email' : 'tel'}
              value={testRecipient}
              onChange={(e) => setTestRecipient(e.target.value)}
              placeholder={testChannel === 'Email' ? 'you@example.com' : '+447900000000'}
              disabled={testBusy}
            />
          </div>
          <Button onClick={handleSendTest} disabled={!testRecipient.trim() || testBusy}>
            {testBusy ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin" /> Sending…
              </>
            ) : (
              <>
                <Send className="h-3.5 w-3.5" /> Send test
              </>
            )}
          </Button>
          {testResult && (
            <div
              className={cn(
                'flex items-start gap-2 rounded border px-3 py-2 text-xs',
                testResult.sent
                  ? 'border-[var(--color-success)] bg-[var(--color-success-light)] text-[var(--color-success)]'
                  : 'border-[var(--color-danger)] bg-[var(--color-error-light)] text-[var(--color-danger)]',
              )}
            >
              {testResult.sent ? (
                <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              ) : (
                <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              )}
              <span>
                {testResult.sent
                  ? `Sent via ${testResult.provider}.`
                  : `Failed (${testResult.provider}): ${testResult.errorMessage ?? 'unknown error'}`}
              </span>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Docs / operator guidance */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Configuring providers</CardTitle>
          <CardDescription className="mt-1">
            All outbound-messaging configuration lives in app settings / environment variables.
            Email and SMS keys are separate — set each channel independently and restart the API
            for changes to take effect.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3 font-mono text-[12px] leading-relaxed">
            <div className="text-[var(--color-text-tertiary)]"># Email channel</div>
            <div>Communication:Email:Provider=AzureCommunicationServices</div>
            <div>Communication:Email:AzureCommunicationServices:ConnectionString=&lt;...&gt;</div>
            <div>Communication:Email:AzureCommunicationServices:FromAddress=noreply@yourdomain.com</div>
            <div className="mt-2 text-[var(--color-text-tertiary)]"># SMS channel</div>
            <div>Communication:Sms:Provider=AzureCommunicationServices</div>
            <div>Communication:Sms:AzureCommunicationServices:ConnectionString=&lt;...&gt;</div>
            <div>Communication:Sms:AzureCommunicationServices:FromPhoneNumber=+44XXXXXXXXXX</div>
          </div>
          <p className="mt-3 text-xs text-[var(--color-text-tertiary)]">
            Multi-provider support (SendGrid / Mailgun for email; Twilio / MessageBird for SMS) is
            on the roadmap. Today only Azure Communication Services is implemented in code — when
            a second provider lands, this page will gain a provider dropdown per channel.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
