import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { AlertCircle, RefreshCw, Save, TestTube2 } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { paymentGatewaysService } from '@/services/paymentGatewaysService';
import { cn } from '@/lib/utils';
import type { PaymentGatewayProviderResponse, PaymentGatewaySettingsUpdateRequest } from '@/types';

interface GatewayFormState {
  providerCode: string;
  enabled: boolean;
  baseUrl: string;
  idpTokenUrl: string;
  clientId: string;
  defaultTransferPurpose: string;
  clientSecret: string;
  encryptionKey: string;
  signingSecret: string;
  hasClientSecret: boolean;
  hasEncryptionKey: boolean;
  hasSigningSecret: boolean;
  secretSource: string;
}

function toInputValue(value?: string | null): string {
  return value ?? '';
}

function toTrimmed(value: string): string {
  return value.trim();
}

function resolveUserMessage(error: unknown, fallback: string): string {
  const message = error && typeof error === 'object' && 'userMessage' in error
    ? String((error as { userMessage?: string }).userMessage ?? '')
    : '';
  return message || fallback;
}

function buildFormState(provider: PaymentGatewayProviderResponse): GatewayFormState {
  return {
    providerCode: provider.providerCode,
    enabled: provider.enabled,
    baseUrl: toInputValue(provider.baseUrl),
    idpTokenUrl: toInputValue(provider.idpTokenUrl),
    clientId: toInputValue(provider.clientId),
    defaultTransferPurpose: toInputValue(provider.defaultTransferPurpose),
    clientSecret: '',
    encryptionKey: '',
    signingSecret: '',
    hasClientSecret: provider.hasClientSecret,
    hasEncryptionKey: provider.hasEncryptionKey,
    hasSigningSecret: provider.hasSigningSecret,
    secretSource: provider.secretSource,
  };
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
        {code ? <p className="mt-1 font-mono text-[10.5px] text-[var(--color-text-tertiary)]">{code}</p> : null}
        {help ? <p className="mt-1.5 text-[11.5px] leading-5 text-[var(--color-text-tertiary)]">{help}</p> : null}
      </div>
      <div>{children}</div>
    </div>
  );
}

function SecretBadge({ configured, pending }: { configured: boolean; pending: boolean }) {
  if (pending) {
    return <Badge variant="warning">Pending save</Badge>;
  }
  return <Badge variant={configured ? 'success' : 'outline'}>{configured ? 'Configured' : 'Not set'}</Badge>;
}

export function SettingsPaymentGatewaysPage() {
  const [formState, setFormState] = useState<GatewayFormState | null>(null);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadSettings = async () => {
    setLoading(true);
    setError(null);
    try {
      const snapshot = await paymentGatewaysService.get();
      const flutterwave = snapshot.providers.find((provider) => provider.providerCode === 'Flutterwave') ?? snapshot.providers[0];
      if (!flutterwave) {
        throw new Error('No payment gateway providers were returned.');
      }
      setFormState(buildFormState(flutterwave));
    } catch (err: unknown) {
      setError(resolveUserMessage(err, 'Failed to load payment gateway settings.'));
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  };

  useEffect(() => {
    void loadSettings();
  }, []);

  const updateField = <K extends keyof GatewayFormState>(key: K, value: GatewayFormState[K]) => {
    setFormState((prev) => prev ? { ...prev, [key]: value } : prev);
  };

  const handleSave = async () => {
    if (!formState) return;
    setSaving(true);
    setError(null);
    const request: PaymentGatewaySettingsUpdateRequest = {
      providers: [
        {
          providerCode: formState.providerCode,
          enabled: formState.enabled,
          baseUrl: toTrimmed(formState.baseUrl),
          idpTokenUrl: toTrimmed(formState.idpTokenUrl),
          clientId: toTrimmed(formState.clientId),
          defaultTransferPurpose: toTrimmed(formState.defaultTransferPurpose),
          clientSecret: toTrimmed(formState.clientSecret) || null,
          encryptionKey: toTrimmed(formState.encryptionKey) || null,
          signingSecret: toTrimmed(formState.signingSecret) || null,
        },
      ],
    };

    try {
      const snapshot = await paymentGatewaysService.update(request);
      const flutterwave = snapshot.providers.find((provider) => provider.providerCode === formState.providerCode) ?? snapshot.providers[0];
      setFormState(buildFormState(flutterwave));
      toast.success('Payment gateway settings saved.');
    } catch (err: unknown) {
      const message = resolveUserMessage(err, 'Failed to save payment gateway settings.');
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const handleTest = async () => {
    if (!formState) return;
    setTesting(true);
    try {
      const result = await paymentGatewaysService.test({ providerCode: formState.providerCode });
      if (result.succeeded) {
        toast.success('Flutterwave credentials are valid.');
      } else {
        toast.error(result.errorMessage ?? 'Flutterwave test failed.');
      }
    } catch (err: unknown) {
      toast.error(resolveUserMessage(err, 'Failed to test payment gateway.'));
    } finally {
      setTesting(false);
    }
  };

  if (initialLoad && loading) {
    return <PageLoadingScreen message="Loading payment gateway settings" />;
  }

  return (
    <div className="flex h-full min-h-0">
      <aside className="w-80 flex-none overflow-auto border-r border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-[18px]">
        <h1 className="text-[17px] font-semibold text-[var(--color-text-primary)]">Payment gateways</h1>
        <p className="mt-1 mb-4 text-[12.5px] leading-5 text-[var(--color-text-secondary)]">
          Configure provider credentials and runtime connection settings.
        </p>
        <div className="flex flex-col gap-1.5">
          <button
            type="button"
            className="flex items-center gap-2.5 rounded-[10px] border border-[var(--color-brand-primary)] bg-[var(--color-surface)] p-3 text-left"
          >
            <span className="grid h-8 w-8 flex-none place-items-center rounded-md bg-[#f5a623] text-[13px] font-bold text-white">F</span>
            <span className="min-w-0 flex-1">
              <span className="flex items-center justify-between gap-1.5">
                <span className="truncate text-[13px] font-semibold text-[var(--color-text-primary)]">Flutterwave</span>
                <Badge variant={formState?.enabled ? 'success' : 'outline'} className="gap-1 text-[10px]">
                  <span className="h-1.5 w-1.5 rounded-full bg-current" />{formState?.enabled ? 'Enabled' : 'Disabled'}
                </Badge>
              </span>
              <span className="mt-0.5 block truncate text-[11px] text-[var(--color-text-secondary)]">Transfers · recipients · webhooks</span>
            </span>
          </button>
        </div>
      </aside>

      <main className="min-w-0 flex-1 overflow-auto px-8 py-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">Settings · Gateways · Flutterwave</p>
            <h2 className="text-2xl font-bold text-[var(--color-text-primary)]">Flutterwave</h2>
            <p className="text-[var(--color-text-secondary)]">Runtime-configured v4 payout connector for African corridors.</p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" className="gap-1.5" onClick={() => void loadSettings()} disabled={loading || saving}>
              <RefreshCw className={cn('h-3 w-3', loading && 'animate-spin')} />Refresh
            </Button>
            <Button variant="outline" size="sm" className="gap-1.5" onClick={() => void handleTest()} disabled={!formState || testing}>
              <TestTube2 className="h-3 w-3" />{testing ? 'Testing...' : 'Test connection'}
            </Button>
            <Button size="sm" className="gap-1.5" onClick={() => void handleSave()} disabled={!formState || saving}>
              <Save className="h-3 w-3" />{saving ? 'Saving...' : 'Save'}
            </Button>
          </div>
        </div>

        {error ? (
          <div className="mt-4 flex items-start gap-2 rounded-xl border border-[var(--color-danger)]/30 bg-[var(--color-danger)]/5 p-3 text-sm text-[var(--color-danger)]">
            <AlertCircle className="mt-0.5 h-4 w-4" />
            <span>{error}</span>
          </div>
        ) : null}

        {formState ? (
          <div className="mt-4">
            <SettingsSection
              title="Credentials"
              description="Secrets are write-only: leave fields blank to keep existing values. The API never returns secret values."
              action={<Badge variant="success">Encrypted at rest</Badge>}
            >
              <Field label="Enabled" code="Finance.Partners.Flutterwave.Enabled" help="Controls whether Flutterwave can be used by runtime connector calls.">
                <button
                  type="button"
                  onClick={() => updateField('enabled', !formState.enabled)}
                  className={cn(
                    'inline-flex rounded-lg p-1 text-xs font-medium',
                    formState.enabled ? 'bg-[var(--color-success)]/10 text-[var(--color-success)]' : 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]',
                  )}
                >
                  <span className="rounded-md bg-[var(--color-surface)] px-4 py-1.5 shadow-sm">{formState.enabled ? 'Enabled' : 'Disabled'}</span>
                </button>
              </Field>
              <Field label="Base URL" code="Finance.Partners.Flutterwave.BaseUrl"><Input value={formState.baseUrl} onChange={(event) => updateField('baseUrl', event.target.value)} /></Field>
              <Field label="IdP token URL" code="Finance.Partners.Flutterwave.IdpTokenUrl"><Input value={formState.idpTokenUrl} onChange={(event) => updateField('idpTokenUrl', event.target.value)} /></Field>
              <Field label="Client ID" code="Finance.Partners.Flutterwave.ClientId"><Input value={formState.clientId} onChange={(event) => updateField('clientId', event.target.value)} /></Field>
              <Field label="Client secret" code="Finance.Partners.Flutterwave.ClientSecret">
                <div className="flex items-center gap-2">
                  <Input type="password" value={formState.clientSecret} placeholder="Leave blank to keep existing secret" onChange={(event) => updateField('clientSecret', event.target.value)} />
                  <SecretBadge configured={formState.hasClientSecret} pending={formState.clientSecret.trim().length > 0} />
                </div>
              </Field>
              <Field label="Encryption key" code="Finance.Partners.Flutterwave.EncryptionKey" help="Captured for card-collection phase; payouts do not currently use this key.">
                <div className="flex items-center gap-2">
                  <Input type="password" value={formState.encryptionKey} placeholder="Leave blank to keep existing key" onChange={(event) => updateField('encryptionKey', event.target.value)} />
                  <SecretBadge configured={formState.hasEncryptionKey} pending={formState.encryptionKey.trim().length > 0} />
                </div>
              </Field>
              <Field label="Webhook signing secret" code="Finance.Partners.Webhooks.Flutterwave.SigningSecret">
                <div className="flex items-center gap-2">
                  <Input type="password" value={formState.signingSecret} placeholder="Leave blank to keep existing secret" onChange={(event) => updateField('signingSecret', event.target.value)} />
                  <SecretBadge configured={formState.hasSigningSecret} pending={formState.signingSecret.trim().length > 0} />
                </div>
              </Field>
              <Field label="Default transfer purpose" code="Finance.Partners.Flutterwave.DefaultTransferPurpose"><Input value={formState.defaultTransferPurpose} onChange={(event) => updateField('defaultTransferPurpose', event.target.value)} /></Field>
              <Field label="Secret source" help="Shows whether the client secret is stored in the database settings store, falls back to appsettings/environment, or is missing.">
                <Badge variant={formState.secretSource === 'None' ? 'outline' : 'secondary'}>{formState.secretSource}</Badge>
              </Field>
            </SettingsSection>
          </div>
        ) : null}
      </main>
    </div>
  );
}
