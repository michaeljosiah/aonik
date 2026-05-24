import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  Loader2,
  Mail,
  MessageSquare,
  Plus,
  RefreshCw,
  Save,
  Send,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { communicationProviderSettingsService } from '@/services/communicationProviderSettingsService';
import { messagingService } from '@/services/messagingService';
import { cn } from '@/lib/utils';
import type {
  CommunicationProviderSettingsResponse,
  CommunicationProviderSettingsUpdateRequest,
  MessagingChannelHealth,
  MessagingHealth,
  SendCommunicationTestResponse,
} from '@/types';

type Channel = 'Email' | 'SMS';
type ProviderId = 'AzureCommunicationServices' | 'SendGrid' | 'Mailgun' | 'AmazonSes' | 'Twilio' | 'MessageBird' | 'Vonage';
type BadgeTone = 'success' | 'warning' | 'outline' | 'secondary';

interface ProviderDefinition {
  key: string;
  id: ProviderId;
  channel: Channel;
  name: string;
  description: string;
  logo: string;
  color: string;
  region: string;
  credentialSummary: string;
  implemented: boolean;
  setupKeys: string[];
}

interface CommunicationFormState {
  emailActiveProvider: string;
  emailAzureConnectionString: string;
  emailAzureFromAddress: string;
  emailHasAzureConnectionString: boolean;
  smsActiveProvider: string;
  smsAzureConnectionString: string;
  smsAzureFromPhoneNumber: string;
  smsHasAzureConnectionString: boolean;
}

const providers: ProviderDefinition[] = [
  {
    key: 'Email:AzureCommunicationServices',
    id: 'AzureCommunicationServices',
    channel: 'Email',
    name: 'Azure Communication Services',
    description: 'Transactional email through Azure Email Communication Services.',
    logo: 'A',
    color: '#0078d4',
    region: 'Global',
    credentialSummary: 'Connection string + verified sender address',
    implemented: true,
    setupKeys: [
      'Communication.Email.Provider',
      'Communication.Email.AzureCommunicationServices.ConnectionString',
      'Communication.Email.AzureCommunicationServices.FromAddress',
    ],
  },
  {
    key: 'Email:SendGrid',
    id: 'SendGrid',
    channel: 'Email',
    name: 'SendGrid',
    description: 'Email API provider for templates, sender authentication, and analytics.',
    logo: 'S',
    color: '#1a82e2',
    region: 'Global',
    credentialSummary: 'API key + verified sender address',
    implemented: false,
    setupKeys: ['Communication.Email.SendGrid.ApiKey', 'Communication.Email.SendGrid.FromAddress'],
  },
  {
    key: 'Email:Mailgun',
    id: 'Mailgun',
    channel: 'Email',
    name: 'Mailgun',
    description: 'Email delivery with domain-level routing and webhooks.',
    logo: 'M',
    color: '#f06b66',
    region: 'Global',
    credentialSummary: 'API key + domain + sender address',
    implemented: false,
    setupKeys: ['Communication.Email.Mailgun.ApiKey', 'Communication.Email.Mailgun.Domain', 'Communication.Email.Mailgun.FromAddress'],
  },
  {
    key: 'Email:AmazonSes',
    id: 'AmazonSes',
    channel: 'Email',
    name: 'Amazon SES',
    description: 'AWS-native transactional email with region-specific sending identities.',
    logo: 'S',
    color: '#ff9900',
    region: 'Global',
    credentialSummary: 'Access key + secret + sender address',
    implemented: false,
    setupKeys: ['Communication.Email.AmazonSes.Region', 'Communication.Email.AmazonSes.AccessKeyId', 'Communication.Email.AmazonSes.FromAddress'],
  },
  {
    key: 'SMS:AzureCommunicationServices',
    id: 'AzureCommunicationServices',
    channel: 'SMS',
    name: 'Azure Communication Services',
    description: 'SMS delivery through Azure phone numbers and short codes.',
    logo: 'A',
    color: '#0078d4',
    region: 'Global',
    credentialSummary: 'Connection string + provisioned phone number',
    implemented: true,
    setupKeys: [
      'Communication.Sms.Provider',
      'Communication.Sms.AzureCommunicationServices.ConnectionString',
      'Communication.Sms.AzureCommunicationServices.FromPhoneNumber',
    ],
  },
  {
    key: 'SMS:Twilio',
    id: 'Twilio',
    channel: 'SMS',
    name: 'Twilio',
    description: 'Programmable SMS with phone number pools and messaging services.',
    logo: 'T',
    color: '#f22f46',
    region: 'Global',
    credentialSummary: 'Account SID + auth token + from number',
    implemented: false,
    setupKeys: ['Communication.Sms.Twilio.AccountSid', 'Communication.Sms.Twilio.AuthToken', 'Communication.Sms.Twilio.FromPhoneNumber'],
  },
  {
    key: 'SMS:MessageBird',
    id: 'MessageBird',
    channel: 'SMS',
    name: 'MessageBird',
    description: 'SMS routing with international reach and sender profiles.',
    logo: 'B',
    color: '#2481d7',
    region: 'Global',
    credentialSummary: 'Access key + originator',
    implemented: false,
    setupKeys: ['Communication.Sms.MessageBird.AccessKey', 'Communication.Sms.MessageBird.Originator'],
  },
  {
    key: 'SMS:Vonage',
    id: 'Vonage',
    channel: 'SMS',
    name: 'Vonage',
    description: 'SMS provider for international delivery and programmable messaging.',
    logo: 'V',
    color: '#871fff',
    region: 'Global',
    credentialSummary: 'API key + API secret + from number',
    implemented: false,
    setupKeys: ['Communication.Sms.Vonage.ApiKey', 'Communication.Sms.Vonage.ApiSecret', 'Communication.Sms.Vonage.FromPhoneNumber'],
  },
];

function resolveUserMessage(error: unknown, fallback: string): string {
  const message =
    error && typeof error === 'object' && 'userMessage' in error
      ? String((error as { userMessage?: string }).userMessage ?? '')
      : '';
  return message || fallback;
}

function toInputValue(value?: string | null): string {
  return value ?? '';
}

function toTrimmed(value: string): string {
  return value.trim();
}

function buildFormState(snapshot: CommunicationProviderSettingsResponse): CommunicationFormState {
  return {
    emailActiveProvider: snapshot.email.activeProvider,
    emailAzureConnectionString: '',
    emailAzureFromAddress: toInputValue(snapshot.email.azureCommunicationServices?.fromAddress),
    emailHasAzureConnectionString: snapshot.email.azureCommunicationServices?.hasConnectionString ?? false,
    smsActiveProvider: snapshot.sms.activeProvider,
    smsAzureConnectionString: '',
    smsAzureFromPhoneNumber: toInputValue(snapshot.sms.azureCommunicationServices?.fromPhoneNumber),
    smsHasAzureConnectionString: snapshot.sms.azureCommunicationServices?.hasConnectionString ?? false,
  };
}

function getChannelHealth(health: MessagingHealth | null, channel: Channel): MessagingChannelHealth | undefined {
  if (!health) return undefined;
  return channel === 'Email' ? health.email : health.sms;
}

function getActiveProvider(settings: CommunicationProviderSettingsResponse | null, channel: Channel): string {
  if (!settings) return 'AzureCommunicationServices';
  return channel === 'Email' ? settings.email.activeProvider : settings.sms.activeProvider;
}

function getSecretConfigured(formState: CommunicationFormState | null, channel: Channel): boolean {
  if (!formState) return false;
  return channel === 'Email'
    ? formState.emailHasAzureConnectionString
    : formState.smsHasAzureConnectionString;
}

function getProviderStatus(
  provider: ProviderDefinition,
  settings: CommunicationProviderSettingsResponse | null,
  health: MessagingHealth | null,
): { label: string; tone: BadgeTone } {
  if (!provider.implemented) {
    return { label: 'Planned', tone: 'outline' };
  }

  const isActive = getActiveProvider(settings, provider.channel) === provider.id;
  if (!isActive) {
    return { label: 'Available', tone: 'secondary' };
  }

  const channelHealth = getChannelHealth(health, provider.channel);
  if (channelHealth?.configured) {
    return { label: 'Active', tone: 'success' };
  }

  return { label: 'Setup required', tone: 'warning' };
}

function Kpi({ label, value, tone }: { label: string; value: string; tone?: BadgeTone }) {
  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <p className="text-[11px] font-medium text-[var(--color-text-tertiary)]">{label}</p>
      <div className="mt-1 flex items-baseline gap-2">
        <p className="text-lg font-semibold text-[var(--color-text-primary)]">{value}</p>
        {tone ? <Badge variant={tone} className="text-[10px]">{tone === 'success' ? 'OK' : 'Check'}</Badge> : null}
      </div>
    </div>
  );
}

function SettingsSection({ title, description, children, action }: { title: string; description?: string; children: ReactNode; action?: ReactNode }) {
  return (
    <section className="mb-4 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]">
      <div className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] px-5 py-4">
        <div>
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</h2>
          {description ? <p className="mt-1 max-w-2xl text-xs leading-5 text-[var(--color-text-secondary)]">{description}</p> : null}
        </div>
        {action}
      </div>
      <div className="space-y-4 p-5">{children}</div>
    </section>
  );
}

function Field({ label, code, help, children }: { label: string; code?: string; help?: string; children: ReactNode }) {
  return (
    <div className="grid gap-3 lg:grid-cols-[260px_minmax(0,1fr)] lg:gap-6">
      <div>
        <p className="text-[13px] font-medium text-[var(--color-text-primary)]">{label}</p>
        {code ? <p className="mt-1 break-all font-mono text-[10.5px] text-[var(--color-text-tertiary)]">{code}</p> : null}
        {help ? <p className="mt-1.5 text-[11.5px] leading-5 text-[var(--color-text-tertiary)]">{help}</p> : null}
      </div>
      <div>{children}</div>
    </div>
  );
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
      <Badge variant="success" className="gap-1">
        <CheckCircle2 className="h-3 w-3" /> Configured
      </Badge>
    );
  }
  return (
    <Badge variant="warning" className="gap-1">
      <AlertTriangle className="h-3 w-3" /> Not configured
    </Badge>
  );
}

function ProviderSetupPreview({ provider }: { provider: ProviderDefinition }) {
  return (
    <div className="rounded-lg border border-dashed border-[var(--color-border)] bg-[var(--color-surface-inset)] p-4">
      <div className="mb-3 flex items-start gap-2 text-xs text-[var(--color-text-secondary)]">
        <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0 text-[var(--color-warning)]" />
        <span>
          The {provider.name} connector is not implemented in the API yet. The settings shape is reserved so this provider can be enabled without changing the page layout.
        </span>
      </div>
      <div className="rounded border border-[var(--color-border-light)] bg-[var(--color-surface)] p-3 font-mono text-[12px] leading-relaxed">
        {provider.setupKeys.map((key) => (
          <div key={key}>{key}=&lt;...&gt;</div>
        ))}
      </div>
    </div>
  );
}

export function SettingsCommunicationPage() {
  const [settings, setSettings] = useState<CommunicationProviderSettingsResponse | null>(null);
  const [formState, setFormState] = useState<CommunicationFormState | null>(null);
  const [health, setHealth] = useState<MessagingHealth | null>(null);
  const [selectedKey, setSelectedKey] = useState('Email:AzureCommunicationServices');
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [testRecipient, setTestRecipient] = useState('');
  const [testBusy, setTestBusy] = useState(false);
  const [testResult, setTestResult] = useState<SendCommunicationTestResponse | null>(null);

  const selectedProvider = useMemo(
    () => providers.find((item) => item.key === selectedKey) ?? providers[0],
    [selectedKey],
  );
  const selectedHealth = getChannelHealth(health, selectedProvider.channel);
  const selectedStatus = getProviderStatus(selectedProvider, settings, health);
  const selectedIsActive = getActiveProvider(settings, selectedProvider.channel) === selectedProvider.id;
  const selectedSecretConfigured = getSecretConfigured(formState, selectedProvider.channel);

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    setError(null);
    try {
      const [settingsResult, healthResult] = await Promise.all([
        communicationProviderSettingsService.get(),
        messagingService.health(),
      ]);
      setSettings(settingsResult);
      setFormState(buildFormState(settingsResult));
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
    void load();
  }, [load]);

  const updateField = <K extends keyof CommunicationFormState>(key: K, value: CommunicationFormState[K]) => {
    setFormState((prev) => {
      if (!prev) return prev;
      return { ...prev, [key]: value };
    });
  };

  const handleRefresh = async () => {
    setRefreshing(true);
    await load(true);
  };

  const handleActivateSelected = () => {
    if (!formState || !selectedProvider.implemented) return;

    if (selectedProvider.channel === 'Email') {
      updateField('emailActiveProvider', selectedProvider.id);
    } else {
      updateField('smsActiveProvider', selectedProvider.id);
    }
  };

  const handleSave = async () => {
    if (!formState || !selectedProvider.implemented) return;

    setSaving(true);
    setError(null);

    const emailConnectionString = toTrimmed(formState.emailAzureConnectionString);
    const smsConnectionString = toTrimmed(formState.smsAzureConnectionString);

    const request: CommunicationProviderSettingsUpdateRequest = selectedProvider.channel === 'Email'
      ? {
          email: {
            activeProvider: formState.emailActiveProvider || selectedProvider.id,
            azureCommunicationServices: {
              connectionString: emailConnectionString.length > 0 ? emailConnectionString : null,
              fromAddress: toTrimmed(formState.emailAzureFromAddress),
            },
          },
        }
      : {
          sms: {
            activeProvider: formState.smsActiveProvider || selectedProvider.id,
            azureCommunicationServices: {
              connectionString: smsConnectionString.length > 0 ? smsConnectionString : null,
              fromPhoneNumber: toTrimmed(formState.smsAzureFromPhoneNumber),
            },
          },
        };

    try {
      const updated = await communicationProviderSettingsService.update(request);
      setSettings(updated);
      setFormState(buildFormState(updated));
      toast.success(`${selectedProvider.channel} provider settings saved.`);
      await load(true);
    } catch (err: unknown) {
      const message = resolveUserMessage(err, 'Failed to save communication settings.');
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const handleSendTest = async () => {
    if (!testRecipient.trim() || testBusy) return;
    setTestBusy(true);
    setTestResult(null);
    try {
      const result = await communicationProviderSettingsService.sendTest({
        channel: selectedProvider.channel,
        recipient: testRecipient.trim(),
        subject: selectedProvider.channel === 'Email' ? 'Aonik test email' : null,
        body: null,
      });
      setTestResult(result);
      if (result.sent) {
        toast.success(`${selectedProvider.channel} test sent successfully.`);
      } else {
        toast.error(`${selectedProvider.channel} test failed: ${result.errorMessage ?? 'Unknown error.'}`);
      }
    } catch (err: unknown) {
      const message = resolveUserMessage(err, 'Failed to send test message.');
      setTestResult({
        sent: false,
        channel: selectedProvider.channel,
        provider: 'Unknown',
        errorMessage: message,
      });
      toast.error(message);
    } finally {
      setTestBusy(false);
    }
  };

  function renderProviderRows(channel: Channel) {
    return providers.filter((provider) => provider.channel === channel).map((provider) => {
      const active = provider.key === selectedKey;
      const status = getProviderStatus(provider, settings, health);
      return (
        <button
          key={provider.key}
          type="button"
          onClick={() => {
            setSelectedKey(provider.key);
            setTestResult(null);
          }}
          className={cn(
            'flex items-center gap-2.5 rounded-[10px] border p-3 text-left transition-colors',
            active
              ? 'border-[var(--color-brand-primary)] bg-[var(--color-surface)]'
              : 'border-transparent hover:bg-[var(--color-surface)]',
          )}
        >
          <span className="grid h-8 w-8 flex-none place-items-center rounded-md text-[13px] font-bold text-white" style={{ backgroundColor: provider.color }}>
            {provider.logo}
          </span>
          <span className="min-w-0 flex-1">
            <span className="flex items-center justify-between gap-1.5">
              <span className="truncate text-[13px] font-semibold text-[var(--color-text-primary)]">{provider.name}</span>
              <Badge variant={status.tone} className="gap-1 text-[10px]">
                <span className="h-1.5 w-1.5 rounded-full bg-current" />
                {status.label}
              </Badge>
            </span>
            <span className="mt-0.5 block truncate text-[11px] text-[var(--color-text-secondary)]">{provider.description}</span>
          </span>
        </button>
      );
    });
  }

  if (initialLoad) {
    return <PageLoadingScreen message="Loading communication settings" />;
  }

  return (
    <div className="flex h-full min-h-0 flex-col lg:flex-row">
      <aside className="w-full flex-none overflow-auto border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-[18px] lg:w-80 lg:border-b-0 lg:border-r">
        <h1 className="text-[17px] font-semibold text-[var(--color-text-primary)]">Communication</h1>
        <p className="mt-1 mb-4 text-[12.5px] leading-5 text-[var(--color-text-secondary)]">
          Configure outbound email and SMS providers independently.
        </p>
        <Button
          size="sm"
          className="mb-4 w-full justify-center gap-1.5"
          variant="outline"
          onClick={() => toast.info('Provider registry is fixed until new communication connectors are implemented.')}
        >
          <Plus className="h-3 w-3" />
          Add provider
        </Button>

        <div className="space-y-4">
          <div>
            <div className="mb-2 flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">
              <Mail className="h-3 w-3" /> Email providers
            </div>
            <div className="flex flex-col gap-1.5">{renderProviderRows('Email')}</div>
          </div>
          <div>
            <div className="mb-2 flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">
              <MessageSquare className="h-3 w-3" /> SMS providers
            </div>
            <div className="flex flex-col gap-1.5">{renderProviderRows('SMS')}</div>
          </div>
        </div>
      </aside>

      <main className="min-w-0 flex-1 overflow-auto px-5 py-5 lg:px-8 lg:py-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">
              Settings · Communication · {selectedProvider.channel} · {selectedProvider.region}
            </p>
            <h2 className="text-2xl font-bold text-[var(--color-text-primary)]">{selectedProvider.name}</h2>
            <p className="max-w-3xl text-[var(--color-text-secondary)]">{selectedProvider.description}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" size="sm" onClick={handleRefresh} disabled={refreshing || loading || saving} className="gap-1.5">
              <RefreshCw className={cn('h-3 w-3', refreshing && 'animate-spin')} /> Refresh
            </Button>
            <Button variant="outline" size="sm" onClick={handleActivateSelected} disabled={!formState || !selectedProvider.implemented || selectedIsActive || saving}>
              Activate
            </Button>
            <Button size="sm" onClick={() => void handleSave()} disabled={!formState || !selectedProvider.implemented || saving} className="gap-1.5">
              {saving ? <Loader2 className="h-3 w-3 animate-spin" /> : <Save className="h-3 w-3" />}
              Save
            </Button>
          </div>
        </div>

        {error && (
          <Card className="mt-5 border-[var(--color-error)] bg-[var(--color-error-light)]">
            <CardContent className="flex items-center gap-3 p-4 text-[var(--color-error)]">
              <AlertCircle className="h-5 w-5 shrink-0" />
              <span className="flex-1 text-sm">{error}</span>
              <Button variant="ghost" size="sm" onClick={() => void handleRefresh()}>
                Retry
              </Button>
            </CardContent>
          </Card>
        )}

        <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <Kpi label="Channel" value={selectedProvider.channel} />
          <Kpi label="Provider status" value={selectedStatus.label} tone={selectedStatus.tone} />
          <Kpi label="Secret" value={selectedSecretConfigured ? 'Configured' : 'Missing'} tone={selectedSecretConfigured ? 'success' : 'warning'} />
          <Kpi label="Runtime health" value={selectedHealth?.provider ?? 'Checking'} tone={selectedHealth?.configured ? 'success' : 'warning'} />
        </div>

        <div className="mt-4">
          <SettingsSection
            title="Provider configuration"
            description={`Credential and sender setup for ${selectedProvider.name}. Email and SMS keep separate active providers and separate credential keys.`}
            action={<HealthBadge health={selectedHealth} />}
          >
            {!selectedProvider.implemented ? (
              <ProviderSetupPreview provider={selectedProvider} />
            ) : selectedProvider.channel === 'Email' ? (
              <>
                <Field label="Active provider" code="Communication.Email.Provider" help="Controls which provider dispatches outbound email.">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={selectedIsActive ? 'success' : 'secondary'}>{formState?.emailActiveProvider ?? 'AzureCommunicationServices'}</Badge>
                    {!selectedIsActive ? <span className="text-xs text-[var(--color-text-tertiary)]">Click Activate, then Save to make this provider active.</span> : null}
                  </div>
                </Field>
                <Field
                  label="Connection string"
                  code="Communication.Email.AzureCommunicationServices.ConnectionString"
                  help="Write-only secret. Leave empty to keep the current connection string."
                >
                  <Input
                    type="password"
                    value={formState?.emailAzureConnectionString ?? ''}
                    placeholder={formState?.emailHasAzureConnectionString ? 'Configured - leave empty to keep existing value' : 'Paste ACS connection string'}
                    onChange={(event) => updateField('emailAzureConnectionString', event.target.value)}
                    className="font-mono text-xs"
                    disabled={saving}
                  />
                </Field>
                <Field label="From address" code="Communication.Email.AzureCommunicationServices.FromAddress" help="Must be a verified sender address in Azure Communication Services.">
                  <Input
                    type="email"
                    value={formState?.emailAzureFromAddress ?? ''}
                    placeholder="noreply@yourdomain.com"
                    onChange={(event) => updateField('emailAzureFromAddress', event.target.value)}
                    disabled={saving}
                  />
                </Field>
              </>
            ) : (
              <>
                <Field label="Active provider" code="Communication.Sms.Provider" help="Controls which provider dispatches outbound SMS.">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={selectedIsActive ? 'success' : 'secondary'}>{formState?.smsActiveProvider ?? 'AzureCommunicationServices'}</Badge>
                    {!selectedIsActive ? <span className="text-xs text-[var(--color-text-tertiary)]">Click Activate, then Save to make this provider active.</span> : null}
                  </div>
                </Field>
                <Field
                  label="Connection string"
                  code="Communication.Sms.AzureCommunicationServices.ConnectionString"
                  help="Write-only secret. Leave empty to keep the current connection string."
                >
                  <Input
                    type="password"
                    value={formState?.smsAzureConnectionString ?? ''}
                    placeholder={formState?.smsHasAzureConnectionString ? 'Configured - leave empty to keep existing value' : 'Paste ACS connection string'}
                    onChange={(event) => updateField('smsAzureConnectionString', event.target.value)}
                    className="font-mono text-xs"
                    disabled={saving}
                  />
                </Field>
                <Field label="From phone number" code="Communication.Sms.AzureCommunicationServices.FromPhoneNumber" help="Use an Azure-provisioned sender phone number in E.164 format.">
                  <Input
                    type="tel"
                    value={formState?.smsAzureFromPhoneNumber ?? ''}
                    placeholder="+447900000000"
                    onChange={(event) => updateField('smsAzureFromPhoneNumber', event.target.value)}
                    disabled={saving}
                  />
                </Field>
              </>
            )}

            {selectedHealth && !selectedHealth.configured && selectedProvider.implemented && (
              <div className="flex items-start gap-2 rounded border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-3 py-2 text-xs text-[var(--color-warning)]">
                <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                <span>{selectedHealth.reason ?? `${selectedProvider.channel} provider is not configured.`}</span>
              </div>
            )}
          </SettingsSection>

          <SettingsSection
            title="Send a test message"
            description={`Dispatches a one-off ${selectedProvider.channel.toLowerCase()} through the active ${selectedProvider.channel} provider. This verifies runtime credentials, not only saved settings.`}
            action={<Badge variant={selectedIsActive ? 'success' : 'warning'}>{selectedIsActive ? 'Selected provider is active' : 'Different provider active'}</Badge>}
          >
            <div className="grid gap-3 lg:grid-cols-[260px_minmax(0,1fr)] lg:gap-6">
              <div>
                <p className="text-[13px] font-medium text-[var(--color-text-primary)]">Recipient</p>
                <p className="mt-1.5 text-[11.5px] leading-5 text-[var(--color-text-tertiary)]">
                  {selectedProvider.channel === 'Email' ? 'Use an email address you can check.' : 'Use an E.164 phone number.'}
                </p>
              </div>
              <div className="space-y-3">
                <div className="flex gap-2">
                  <Input
                    id="test-recipient"
                    type={selectedProvider.channel === 'Email' ? 'email' : 'tel'}
                    value={testRecipient}
                    onChange={(event) => setTestRecipient(event.target.value)}
                    placeholder={selectedProvider.channel === 'Email' ? 'you@example.com' : '+447900000000'}
                    disabled={testBusy}
                  />
                  <Button onClick={() => void handleSendTest()} disabled={!testRecipient.trim() || testBusy || !selectedProvider.implemented} className="gap-1.5">
                    {testBusy ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Send className="h-3.5 w-3.5" />}
                    {testBusy ? 'Sending...' : 'Send'}
                  </Button>
                </div>
                {selectedHealth && !selectedHealth.configured ? (
                  <p className="text-xs text-[var(--color-warning)]">Channel is not configured. The test send is expected to fail until credentials are set.</p>
                ) : null}
                {testResult && (
                  <div
                    className={cn(
                      'flex items-start gap-2 rounded border px-3 py-2 text-xs',
                      testResult.sent
                        ? 'border-[var(--color-success)] bg-[var(--color-success-light)] text-[var(--color-success)]'
                        : 'border-[var(--color-error)] bg-[var(--color-error-light)] text-[var(--color-error)]',
                    )}
                  >
                    {testResult.sent ? <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 shrink-0" /> : <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />}
                    <span>
                      {testResult.sent
                        ? `Sent via ${testResult.provider}.`
                        : `Failed (${testResult.provider}): ${testResult.errorMessage ?? 'unknown error'}`}
                    </span>
                  </div>
                )}
              </div>
            </div>
          </SettingsSection>

          <SettingsSection title="Routing model" description="Email and SMS are separate channels. Each can use a different provider, credential set, and sender identity.">
            <div className="grid gap-3 md:grid-cols-2">
              <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4">
                <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-[var(--color-text-primary)]">
                  <Mail className="h-4 w-4" /> Email
                </div>
                <p className="text-xs leading-5 text-[var(--color-text-secondary)]">
                  Active provider: <span className="font-mono text-[var(--color-text-primary)]">{settings?.email.activeProvider ?? 'AzureCommunicationServices'}</span>
                </p>
                <p className="mt-2 text-xs leading-5 text-[var(--color-text-tertiary)]">
                  Used for invitations, password reset, verification, and transactional notifications.
                </p>
              </div>
              <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4">
                <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-[var(--color-text-primary)]">
                  <MessageSquare className="h-4 w-4" /> SMS
                </div>
                <p className="text-xs leading-5 text-[var(--color-text-secondary)]">
                  Active provider: <span className="font-mono text-[var(--color-text-primary)]">{settings?.sms.activeProvider ?? 'AzureCommunicationServices'}</span>
                </p>
                <p className="mt-2 text-xs leading-5 text-[var(--color-text-tertiary)]">
                  Used for phone verification OTPs and future SMS notification flows.
                </p>
              </div>
            </div>
          </SettingsSection>

          <SettingsSection
            title="Template authoring"
            description="Notification templates currently render with Fluid/Liquid variables. MJML should be treated as an email-only authoring layer, not as the runtime format for all channels."
            action={<Badge variant="outline">Recommendation</Badge>}
          >
            <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4 text-xs leading-5 text-[var(--color-text-secondary)]">
              Support MJML for email templates when we add a template format field. The safe path is: store Email templates as Liquid HTML or Liquid MJML, compile MJML to responsive HTML during preview/send, then render Liquid variables with the existing Fluid renderer. SMS should remain plain Liquid text.
            </div>
          </SettingsSection>
        </div>
      </main>
    </div>
  );
}
